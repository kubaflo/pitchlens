using System.Text.Json;
using Microsoft.Extensions.AI;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class PersonalizedInsightTests
{
    [Fact]
    public async Task Typed_task_focus_changes_identity_prompt_tools_and_cache_without_filtering_opposition()
    {
        var match=DenseNarrationRecording.Create();
        var original=MomentEvidence.Capture(match,1,86,"e28");
        Assert.Equal(MomentRecording.Hash,original.Hash);
        var a=MomentEvidence.Capture(match,1,86,"e28",preferences:new(InsightTask.ExplainBuildUp,InsightScope.Club,"manchester-city"));
        var b=MomentEvidence.Capture(match,1,86,"e28",preferences:new(InsightTask.PlayerContribution,InsightScope.Player,match.Events.Single(e=>e.Id=="e28").ActorId));
        Assert.NotEqual(a.Hash,b.Hash);Assert.NotEqual(original.Hash,a.Hash);
        var sameFocusOtherTask=MomentEvidence.Capture(match,1,86,"e28",preferences:new(InsightTask.WhyThisMatters,InsightScope.Club,"manchester-city"));
        Assert.NotEqual(a.Hash,sameFocusOtherTask.Hash);
        Assert.Equal(original.Facts.Events.Select(e=>e.Id),a.Facts.Events.Select(e=>e.Id));
        Assert.Contains(a.Facts.Events,e=>e.Id=="e28");Assert.Equal(2,a.Facts.Context!.Score.Count);
        Assert.Contains("ExplainBuildUp",CopilotMomentSource.Prompt(a));Assert.Contains("manchester-city",CopilotMomentSource.Prompt(a));
        var source=new Source();var service=new MomentExplanationService(source);service.ChangeScene(1);
        await service.GenerateAsync(a,CancellationToken.None);Assert.NotNull(service.Current);
        Assert.True(service.TryReuseFresh(a));Assert.False(service.TryReuseFresh(b));
        Assert.False(service.TryReuseFresh(sameFocusOtherTask));
        await service.GenerateAsync(b,CancellationToken.None);Assert.Equal(2,source.Calls);
        Assert.Equal(b.Hash,service.Current!.Evidence.Hash);Assert.True(service.TryReuseFresh(b));
        Assert.Equal(2,service.Current.Ledger!.Length);Assert.Equal(2,service.Current.ToolCalls);
        Assert.Null(service.VisibleAt(1,85.99));Assert.Null(service.VisibleAt(2,86));
        service.ChangeScene(2);Assert.Null(service.Current);
    }
    [Fact]
    public async Task Invalid_subject_and_empty_player_contribution_never_spend_or_claim_a_tailored_answer()
    {
        var match=DenseNarrationRecording.Create();
        foreach(var request in new[]{new InsightPreferences((InsightTask)99),new(InsightTask.PlayerContribution),
            new(InsightTask.ExplainBuildUp,InsightScope.Player,"missing"),new(InsightTask.ExplainBuildUp,InsightScope.All,"arsenal")})
            Assert.Throws<ArgumentException>(()=>MomentEvidence.Capture(match,1,86,preferences:request));
        var window=MomentEvidence.Capture(match,1,86);
        var absent=match.Players.First(p=>!window.Facts.Events.Any(e=>e.Actor==p.Name||e.Receiver==p.Name));
        var limited=MomentEvidence.Capture(match,1,86,preferences:new(InsightTask.PlayerContribution,InsightScope.Player,absent.Id));
        Assert.False(limited.HasRequestedContribution);
        var source=new Source();var service=new MomentExplanationService(source);service.ChangeScene(1);
        await service.GenerateAsync(limited,CancellationToken.None);
        Assert.Equal(0,source.Calls);Assert.Null(service.Current);Assert.Contains("No observed involvement",service.Status);
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,mode:SessionMode.Test);
        native.SetInsightPreferences(new(InsightTask.ExplainBuildUp,InsightScope.Club,"arsenal"));
        native.Command("seek",86);native.TestExplain();
        Assert.Equal(InsightPreferences.Default,native.Explainer.Current!.Evidence.Preferences);
        Assert.False(native.Explainer.Current.Fresh);Assert.Equal(0,source.Calls);
    }
    [Fact]
    public async Task Invalid_ledger_or_failed_tailored_request_keeps_previous_answer_and_tools_are_allowlisted()
    {
        var source=new Source();var service=new MomentExplanationService(source);service.ChangeScene(1);
        var a=MomentEvidence.Capture(DenseNarrationRecording.Create(),1,86);
        var b=MomentEvidence.Capture(DenseNarrationRecording.Create(),1,86,preferences:new(InsightTask.ExplainBuildUp));
        await service.GenerateAsync(a,CancellationToken.None);var old=service.Current;
        source.BadLedger=true;await service.GenerateAsync(b,CancellationToken.None);
        Assert.Same(old,service.Current);Assert.Contains("ledger",service.Status);
        using var cancel=new CancellationTokenSource();var tools=new MomentTools(a,cancel);
        await tools.Index.InvokeAsync(new AIFunctionArguments(),CancellationToken.None);
        for(var i=0;i<5;i++)await tools.Event.InvokeAsync(new AIFunctionArguments{{"eventId","e28"}},CancellationToken.None);
        Assert.Equal(6,tools.Audit.Length);Assert.Equal(Enumerable.Range(1,6),tools.Audit.Select(r=>r.Sequence));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await tools.Index.InvokeAsync(new AIFunctionArguments(),CancellationToken.None));
        Assert.Equal(6,tools.Audit.Length);
        Assert.Equal(new[]{"observed_match","observed_event"},CopilotMomentSource.CreateConfig(tools).AvailableTools);
    }
    [Fact]
    public async Task Cancellation_of_a_new_task_is_atomic_and_cannot_publish_after_scene_change()
    {
        var source=new Source();var service=new MomentExplanationService(source);service.ChangeScene(1);
        var match=DenseNarrationRecording.Create();var a=MomentEvidence.Capture(match,1,86);
        await service.GenerateAsync(a,CancellationToken.None);var old=service.Current;
        source.Wait=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var b=MomentEvidence.Capture(match,1,86,preferences:new(InsightTask.ExplainBuildUp));
        var pending=service.GenerateAsync(b,CancellationToken.None);service.Cancel();await pending;
        Assert.Same(old,service.Current);Assert.False(service.Busy);Assert.Equal(2,source.Calls);
        var again=service.GenerateAsync(b,CancellationToken.None);service.ChangeScene(2);source.Wait.TrySetResult();await again;
        Assert.Null(service.Current);
    }
    [Theory]
    [InlineData(SessionMode.Live)]
    [InlineData(SessionMode.Test)]
    public async Task Tour_cannot_discard_a_current_fresh_scene_or_change_a_remembered_Live_choice(SessionMode mode)
    {
        var source=new Source();var writes=0;
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,mode:SessionMode.Live,saveMode:_=>writes++);
        await native.GenerateAsync();var current=native.Match;Assert.True(native.Fresh);
        if(mode==SessionMode.Test)await native.ChooseModeAsync(mode,false);
        var count=writes;var calls=source.Calls;native.Command("seek",120);
        Assert.False(native.CanStartGuidedDemo);Assert.Throws<InvalidOperationException>(native.StartGuidedDemo);
        Assert.Equal(count,writes);Assert.Equal(calls,source.Calls);Assert.Same(current,native.Match);
        Assert.Equal(120,native.Sample(true)!.Frame.Snapshot.Time);
    }
    internal sealed class Source:IMomentSource,IScenarioSource,IDirectorSource
    {
        public int Calls;public int ToolCalls{get;private set;}public IReadOnlyList<MomentReadAudit>? ReadLedger{get;private set;}
        public bool BadLedger;
        public TaskCompletionSource? Wait;
        public async Task<string> ExplainAsync(MomentEvidence e,CancellationToken token)
        {
            Calls++;using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token);var tools=new MomentTools(e,cancel);
            await tools.Index.InvokeAsync(new AIFunctionArguments(),token);
            await tools.Event.InvokeAsync(new AIFunctionArguments{{"eventId",e.FocusId}},token);
            if(Wait is not null)await Wait.Task.WaitAsync(token);
            ToolCalls=tools.Calls;ReadLedger=BadLedger?[new("shell",null,e.Cutoff,1)]:tools.Audit;
            return JsonSerializer.Serialize(new MomentAnswer(e.Hash,[new("The observed move reaches its finish.",[e.FocusId])],
                [new("The sequence suggests a useful connection.",[e.FocusId])]),MomentJsonContext.Default.MomentAnswer);
        }
        public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken t){Calls++;return Task.FromResult(VarietyTests.OneSided());}
        public Task<string> CreateAsync(DirectorEvidence e,IProgress<string>? p,CancellationToken t){Calls++;throw new InvalidOperationException("No Director request");}
    }
}
