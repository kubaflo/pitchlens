using System.Text.Json;
using Microsoft.Extensions.AI;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class ExplanationTeamTests
{
    internal static MomentEvidence Evidence()=>MomentEvidence.Capture(DenseNarrationRecording.Create(),1,86,
        preferences:new(InsightTask.ExplainBuildUp,InsightScope.Club,"arsenal"));
    [Fact]
    public async Task Three_real_handoffs_connect_analyst_to_editor_to_exact_final_candidate_review_and_cache()
    {
        var runner=new Roles();var source=new ExplanationTeamSource(runner);var service=new MomentExplanationService(source);service.ChangeScene(1);
        var evidence=Evidence();await service.GenerateAsync(evidence,CancellationToken.None);
        Assert.NotNull(service.Current);Assert.Equal(3,runner.Inputs.Count);
        var a=runner.Inputs[0];var b=runner.Inputs[1];var c=runner.Inputs[2];
        Assert.Equal(a.Identity,b.Identity);Assert.Equal(b.Identity,c.Identity);
        Assert.Equal(evidence.Hash,a.Identity.FactsHash);Assert.Equal(InsightTask.ExplainBuildUp,c.Identity.Task);
        Assert.Equal(runner.AnalystJson,b.AnalysisJson);
        Assert.Equal(ExplanationTeamContract.Hash(runner.AnalystJson!),b.AnalysisHash);
        Assert.Equal(evidence.Json,b.FactsJson);Assert.NotNull(c.Candidate);
        Assert.Equal(ExplanationTeamContract.Hash(ExplanationTeamContract.AnswerJson(c.Candidate!)),c.CandidateHash);
        Assert.Equal(ExplanationTeamContract.Claims(c.Candidate!).Select(x=>x.Id),c.Claims!.Select(x=>x.Id));
        Assert.Equal(3,source.Collaboration!.Stages.Sum(x=>x.SessionsCreated));
        Assert.Equal(3,source.Collaboration.Stages.Sum(x=>x.SdkSends));Assert.Equal(6,source.ToolCalls);
        Assert.True(source.Collaboration.Approved);Assert.All(source.Collaboration.Stages,x=>Assert.Equal("Validated",x.Status));
        Assert.True(service.TryReuseFresh(evidence));Assert.Equal(3,runner.Inputs.Count);
        Assert.Equal(ExplanationTeamContract.Pipeline,service.Current!.PipelineVersion);
        var other=MomentEvidence.Capture(DenseNarrationRecording.Create(),1,86,preferences:new(InsightTask.WhyThisMatters));
        Assert.False(service.TryReuseFresh(other));Assert.Null(service.VisibleAt(1,85));Assert.Null(service.VisibleAt(2,86));
        var lead=service.Current.Answer.Fan[0].Text;
        runner.AnalysisText="The observed finish follows this captured sequence.";
        await service.GenerateAsync(evidence,CancellationToken.None);
        Assert.NotEqual(lead,service.Current!.Answer.Fan[0].Text);
        Assert.Equal(runner.AnalysisText,service.Current.Answer.Fan[0].Text);
    }
    [Theory]
    [InlineData("analyst-hash",1)]
    [InlineData("analyst-id",1)]
    [InlineData("analyst-unread",1)]
    [InlineData("analyst-stub",1)]
    [InlineData("editor-hash",2)]
    [InlineData("editor-ref",2)]
    [InlineData("editor-tools",2)]
    [InlineData("editor-stub",2)]
    [InlineData("review-hash",3)]
    [InlineData("review-subset",3)]
    [InlineData("review-duplicate",3)]
    [InlineData("review-reordered",3)]
    [InlineData("review-evidence",3)]
    [InlineData("review-reject",3)]
    [InlineData("review-task",3)]
    [InlineData("review-unread",3)]
    public async Task Corrupt_or_rejected_role_stops_team_at_exact_stage_and_preserves_old_answer(string mutation,int calls)
    {
        var runner=new Roles();var source=new ExplanationTeamSource(runner);var service=new MomentExplanationService(source);service.ChangeScene(1);
        var first=Evidence();await service.GenerateAsync(first,CancellationToken.None);var previous=service.Current;
        runner.Inputs.Clear();runner.Mutation=mutation;
        var next=MomentEvidence.Capture(DenseNarrationRecording.Create(),1,86,preferences:new(InsightTask.WhyThisMatters,InsightScope.Club,"arsenal"));
        await service.GenerateAsync(next,CancellationToken.None);
        Assert.Equal(calls,runner.Inputs.Count);Assert.Same(previous,service.Current);
        Assert.False(source.Collaboration!.Approved);Assert.NotNull(source.Collaboration.Failure);Assert.Contains("failed",service.Status);
        if(calls<3)Assert.DoesNotContain(runner.Inputs,x=>x.Role==nameof(ExplanationRole.EvidenceReviewer));
    }
    [Fact]
    public async Task Whole_service_deadline_and_cancellation_between_roles_stop_later_sessions()
    {
        var runner=new Roles{PauseRole=ExplanationRole.EvidenceAnalyst};
        var source=new ExplanationTeamSource(runner);var service=new MomentExplanationService(source,TimeSpan.FromMilliseconds(60));service.ChangeScene(1);
        await service.GenerateAsync(Evidence(),CancellationToken.None);
        Assert.Null(service.Current);Assert.Single(runner.Inputs);Assert.Contains("timed out",service.Status);
        runner=new Roles();source=new(runner);service=new(source);service.ChangeScene(1);
        using var cancellation=new CancellationTokenSource();
        runner.AfterRole=role=>{if(role==ExplanationRole.EvidenceAnalyst)cancellation.Cancel();};
        await service.GenerateAsync(Evidence(),cancellation.Token);
        Assert.Single(runner.Inputs);Assert.Null(service.Current);
    }
    [Fact]
    public async Task Scene_change_while_independent_reviewer_waits_cannot_publish_late_approval()
    {
        var runner=new Roles{PauseRole=ExplanationRole.EvidenceReviewer};var source=new ExplanationTeamSource(runner);
        var service=new MomentExplanationService(source);service.ChangeScene(1);
        var job=service.GenerateAsync(Evidence(),CancellationToken.None);
        await runner.Paused.Task;service.ChangeScene(2);await job;
        Assert.Null(service.Current);Assert.Null(service.Team);Assert.Equal(3,runner.Inputs.Count);
    }
    [Fact]
    public void Separate_role_configs_have_exact_GPT_different_instructions_and_zero_editor_tools()
    {
        using var cancellation=new CancellationTokenSource();var tools=new MomentTools(Evidence(),cancellation);
        var configs=Enum.GetValues<ExplanationRole>().Select(role=>CopilotExplanationRoleRunner.Config(role,tools)).ToArray();
        Assert.All(configs,c=>Assert.Equal("gpt-6.1-sol",c.Model));
        Assert.Equal(3,configs.Select(c=>c.SystemMessage!.Content).Distinct().Count());
        Assert.Empty(configs[1].AvailableTools!);
        Assert.Equal(new[]{"observed_match","observed_event"},configs[0].AvailableTools);
        Assert.Equal(new[]{"observed_match","observed_event"},configs[2].AvailableTools);
        foreach(var role in Enum.GetValues<ExplanationRole>())using(JsonDocument.Parse(ExplanationTeamContract.Schema(role))){}
    }
    [Fact]
    public async Task Test_mode_and_guided_recording_never_run_any_role_or_invent_team_approval()
    {
        var roles=new Roles();var team=new ExplanationTeamSource(roles);var other=new FixtureSelectionTests.CaptureSource();
        using var native=new PitchLens.App.MatchService(other,team,(_,_)=>throw new InvalidOperationException("No auth"),other,
            mode:PitchLens.App.SessionMode.Test);
        native.Command("seek",86);native.TestExplain();await native.ExplainCurrentMomentAsync();
        native.StartGuidedDemo();native.SetGuidedDemoStep(1);native.SetGuidedDemoStep(2);native.SetGuidedDemoStep(3);native.ExitGuidedDemo();
        Assert.Empty(roles.Inputs);Assert.Null(team.Collaboration);
        Assert.False(native.Explainer.Current!.Fresh);Assert.Null(native.Explainer.Current.Collaboration);
        Assert.Equal("single-v1",native.Explainer.Current.PipelineVersion);
        Assert.Contains("Single-agent recorded",native.Explainer.Status);
    }
    [Fact]
    public async Task An_old_single_agent_fresh_cache_cannot_masquerade_as_current_team_approval()
    {
        var source=new Versioned();var service=new MomentExplanationService(source);service.ChangeScene(1);
        var evidence=Evidence();await service.GenerateAsync(evidence,CancellationToken.None);
        Assert.NotNull(service.Current);Assert.True(service.TryReuseFresh(evidence));
        source.Team=true;Assert.False(service.TryReuseFresh(evidence));
        Assert.NotEqual(evidence.Hash,service.RequestKey(evidence));
    }
    [Fact]
    public async Task Successful_evidence_roles_can_use_six_each_but_never_share_an_unbounded_tool_budget()
    {
        var roles=new Roles{ExtraReads=3};var source=new ExplanationTeamSource(roles);
        var service=new MomentExplanationService(source);service.ChangeScene(1);await service.GenerateAsync(Evidence(),CancellationToken.None);
        Assert.NotNull(service.Current);Assert.Equal(12,source.ToolCalls);
        Assert.Equal(new[]{6,0,6},source.Collaboration!.Stages.Select(s=>s.ToolCalls));
    }
    [Fact]
    public async Task Busy_native_reopen_deduplicates_the_whole_team_and_completed_reopen_is_zero_calls()
    {
        var roles=new Roles{PauseRole=ExplanationRole.EvidenceReviewer};var team=new ExplanationTeamSource(roles);var other=new FixtureSelectionTests.CaptureSource();
        using var native=new PitchLens.App.MatchService(other,team,(_,_)=>throw new InvalidOperationException(),other);
        native.Command("seek",86);var first=native.ExplainCurrentMomentAsync();await roles.Paused.Task;
        var capture=native.ExplanationSnapshot;var second=native.ExplainCurrentMomentAsync();
        Assert.Equal(3,roles.Inputs.Count);Assert.Same(capture,native.ExplanationSnapshot);
        roles.Resume.SetResult();await first;await second;
        Assert.True(native.Explainer.Current!.Collaboration!.Approved);
        await native.ExplainCurrentMomentAsync();Assert.Equal(3,roles.Inputs.Count);Assert.True(native.Explainer.Current.Cached);
        await native.ChooseModeAsync(PitchLens.App.SessionMode.Test,false);native.TestExplain();
        Assert.Equal(3,roles.Inputs.Count);Assert.Null(native.Explainer.Current.Collaboration);Assert.False(native.Explainer.Current.Fresh);
    }
    [Theory]
    [InlineData(ExplanationRole.EvidenceAnalyst,1)]
    [InlineData(ExplanationRole.AudienceEditor,2)]
    public async Task Authentication_or_transport_role_failure_does_not_start_the_remaining_roles(ExplanationRole role,int count)
    {
        var runner=new Roles{FailRole=role};var source=new ExplanationTeamSource(runner);
        var service=new MomentExplanationService(source);service.ChangeScene(1);await service.GenerateAsync(Evidence(),CancellationToken.None);
        Assert.Null(service.Current);Assert.Equal(count,runner.Inputs.Count);Assert.False(source.Collaboration!.Approved);
        Assert.Contains("Controlled role transport",service.Status);
    }
    private sealed class Versioned:IMomentSource
    {
        private readonly PersonalizedInsightTests.Source old=new();
        public bool Team;
        public string PipelineVersion=>Team?ExplanationTeamContract.Pipeline:"single-v1";
        public Task<string> ExplainAsync(MomentEvidence e,CancellationToken token)=>old.ExplainAsync(e,token);
    }
    internal sealed class Roles:IExplanationRoleRunner
    {
        public List<TeamInput> Inputs=[];
        public string Mutation="";
        public string? AnalystJson;
        public string AnalysisText="The captured sequence reaches its observed finish.";
        public ExplanationRole? FailRole;
        public int ExtraReads;
        public ExplanationRole? PauseRole;
        public TaskCompletionSource Paused=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<ExplanationRole>? AfterRole;
        public AgentExecution? Execution{get;private set;}
        public async Task<string> RunAsync(TeamRoleRequest request,MomentEvidence evidence,TimeSpan remaining,IProgress<RequestProgress>? progress,CancellationToken token)
        {
            Assert.InRange(remaining.TotalSeconds,0,270);
            var input=JsonSerializer.Deserialize(request.InputJson,TeamJsonContext.Default.TeamInput)!;Inputs.Add(input);
            Assert.Equal(request.InputHash,ExplanationTeamContract.Hash(request.InputJson));
            var otherId=evidence.Facts.Events.First(e=>e.Id!=evidence.FocusId).Id;
            MomentReadAudit[] reads=[];
            if(request.Role!=ExplanationRole.AudienceEditor){
                using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);var tools=new MomentTools(evidence,cancellation);
                await tools.Index.InvokeAsync(new AIFunctionArguments(),token);
                await tools.Event.InvokeAsync(new AIFunctionArguments{{"eventId",evidence.FocusId}},token);reads=tools.Audit;
                if(request.Role==ExplanationRole.EvidenceAnalyst||Mutation!="review-unread")
                    await tools.Event.InvokeAsync(new AIFunctionArguments{{"eventId",otherId}},token);
                for(var i=0;i<ExtraReads;i++)await tools.Event.InvokeAsync(new AIFunctionArguments{{"eventId",evidence.FocusId}},token);
                reads=tools.Audit;
            }
            Execution=new(request.Role.ToString(),CopilotScenarioSource.Model,"Response received",1,1,1,reads.Length,request.InputHash,null,reads);
            if(FailRole==request.Role){Execution=Execution with{Responses=0};throw new CopilotGenerationException("Controlled role transport/authentication failure.");}
            if(Mutation=="editor-tools"&&request.Role==ExplanationRole.AudienceEditor)Execution=Execution with{ToolCalls=1};
            progress?.Report(new(RequestStage.Thinking,request.Role+" actual fake role callback",reads.Length));
            if(PauseRole==request.Role){Paused.TrySetResult();await Resume.Task.WaitAsync(token);}
            var header=Mutation=="review-task"&&request.Role==ExplanationRole.EvidenceReviewer?request.Identity with{Task=InsightTask.PlayerContribution}:request.Identity;
            string raw;
            if(request.Role==ExplanationRole.EvidenceAnalyst){
                var findings=Enumerable.Range(1,3).Select(i=>new TeamFinding("finding-"+i,"observation",AnalysisText,[evidence.FocusId])).ToArray();
                findings[0]=findings[0] with{EvidenceIds=[otherId]};
                if(Mutation=="analyst-id")findings[2]=findings[2] with{Id="finding-1"};
                if(Mutation=="analyst-stub")findings[2]=findings[2] with{Text="A"};
                if(Mutation=="analyst-unread")findings[0]=findings[0] with{EvidenceIds=[evidence.Facts.Events.First(e=>e.Id!=evidence.FocusId&&e.Id!=otherId).Id]};
                var packet=new AnalystPacket(header,request.Role.ToString(),Mutation=="analyst-hash"?"wrong":request.InputHash,findings);
                raw=AnalystJson=JsonSerializer.Serialize(packet,TeamJsonContext.Default.AnalystPacket);
            }else if(request.Role==ExplanationRole.AudienceEditor){
                Assert.NotNull(input.AnalysisJson);Assert.NotNull(input.FactsJson);
                var received=JsonSerializer.Deserialize(input.AnalysisJson!,TeamJsonContext.Default.AnalystPacket)!;
                var answer=new MomentAnswer(evidence.Hash,[new(received.Findings[1].Text,[evidence.FocusId,otherId])],
                    [new("The sequence suggests a useful attacking connection.",[evidence.FocusId])]);
                if(Mutation=="editor-ref")answer=answer with{Fan=[new("The earlier action supports this sequence.",[evidence.Facts.Events.First(e=>e.Id!=evidence.FocusId&&e.Id!=otherId).Id]),answer.Fan[0]]};
                if(Mutation=="editor-stub")answer=answer with{Fan=[new("A",[evidence.FocusId])]};
                raw=JsonSerializer.Serialize(new EditorPacket(header,request.Role.ToString(),Mutation=="editor-hash"?"wrong":request.InputHash,answer),TeamJsonContext.Default.EditorPacket);
            }else{
                var checks=input.Claims!.Select(c=>new ReviewCheck(c.Id,c.EvidenceIds,Mutation!="review-reject","Captured evidence supports this bounded statement.")).ToArray();
                if(Mutation=="review-subset")checks=checks[..^1];
                if(Mutation=="review-duplicate")checks[1]=checks[0];
                if(Mutation=="review-reordered")Array.Reverse(checks);
                if(Mutation=="review-evidence")checks[0]=checks[0] with{EvidenceIds=["unknown"]};
                raw=JsonSerializer.Serialize(new ReviewerPacket(header,request.Role.ToString(),request.InputHash,
                    Mutation=="review-hash"?"wrong":input.CandidateHash!,checks,Mutation!="review-reject"),TeamJsonContext.Default.ReviewerPacket);
            }
            AfterRole?.Invoke(request.Role);return raw;
        }
    }
}
