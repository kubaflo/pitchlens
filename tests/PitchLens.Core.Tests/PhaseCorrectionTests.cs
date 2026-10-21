using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

// Controlled regressions, not the unavailable failed Live response.
public sealed class PhaseCorrectionTests
{
    private static AuthoredMatchDocument Baseline()=>AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
    private static AuthoredMatchDocument UnsupportedTurnover(AuthoredMatchDocument original)
    {
        var kinds=original.PhaseKinds.ToArray();
        var index=Enumerable.Range(0,10).First(n=>!original.Events.Any(e=>e.Time>n*30&&e.Time<=(n+1)*30&&e.Kind=="pass"&&e.Completed==false));
        kinds[index]="turnover";
        return original with{PhaseKinds=kinds};
    }
    private static string Json(AuthoredMatchDocument document)=>JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument);
    private static string Repair(AuthoredMatchDocument original)=>JsonSerializer.Serialize(
        new AuthoredPhaseCorrection(AuthoredPhaseRefinement.Tags(original)),PhaseCorrectionJsonContext.Default.AuthoredPhaseCorrection);
    [Fact]
    public async Task Unsupported_phase_is_strictly_red_then_one_tag_only_correction_preserves_all_paths_events_and_three_response_cap()
    {
        var valid=Baseline();var bad=UnsupportedTurnover(valid);
        var red=Assert.Throws<AuthoredPhaseValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
        var issue=Assert.Single(red.Issues);
        Assert.Equal("phase-1",issue.PhaseId);Assert.Equal(0,issue.Start);Assert.Equal(30,issue.End);
        Assert.Contains("phase-1 (0,30]s",red.Message);Assert.Contains("completed intercepted pass",red.Message);
        Assert.All(issue.Observed,e=>Assert.InRange(e.Time,double.Epsilon,30));
        Assert.DoesNotContain(issue.Observed,e=>e.Kind=="pass"&&e.Completed==false);
        Assert.DoesNotContain("e10@",red.Message); // first interception is at33s, outside phase1
        var paths=0;var shape=0;var phases=0;var narration=0;var messages=new Messages();
        var output=await AuthoredGenerationPipeline.RunAsync(
            _=>{paths++;return Task.FromResult(Json(bad));},
            (_,_,_)=>{shape++;throw new InvalidOperationException("Phase repair must not claim to change paths.");},
            (match,_)=>{narration++;return Task.FromResult(NarrationFixtures.Json(match));},
            messages,CancellationToken.None,refinePhases:(document,failure,_)=>
            {
                phases++;Assert.IsType<AuthoredPhaseValidationException>(failure);
                var prompt=AuthoredPhaseRefinement.Prompt(document,failure);
                Assert.Contains("COMPLETION",prompt);Assert.Contains("IMMUTABLE",prompt);Assert.Contains("phase-1",prompt);
                using var schema=JsonDocument.Parse(AuthoredPhaseRefinement.Schema);
                Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
                return Task.FromResult(Repair(valid));
            });
        Assert.Equal(1,paths);Assert.Equal(1,phases);Assert.Equal(0,shape);Assert.Equal(1,narration);
        var delivered=AuthoredMatchPlan.Document(output);
        Assert.Equal(valid.Events,delivered.Events);Assert.Equal(valid.PhaseKinds,delivered.PhaseKinds);
        Assert.Equal(valid.InitialOwnerId,delivered.InitialOwnerId);Assert.Equal(valid.Duration,delivered.Duration);
        foreach(var player in valid.Players)
            Assert.Equal(JsonSerializer.Serialize(player),JsonSerializer.Serialize(delivered.Players.Single(p=>p.Id==player.Id)));
        Assert.NotNull(AuthoredMatchPlan.Parse(output).Narrative);
        Assert.Contains(messages.Items,m=>m.Contains("phase labels")&&m.Contains("paths and events unchanged"));
        Assert.DoesNotContain(messages.Items,m=>m.Contains("AI refining rejected player paths"));
    }
    [Theory]
    [InlineData("liverpool","chelsea")]
    [InlineData("manchester-united","tottenham-hotspur")]
    public async Task Captured_fixture_and_every_original_identity_survive_tag_only_correction(string home,string away)
    {
        var fixture=FixtureCatalog.Create(home,away);var valid=FixtureTests.Control(fixture);var bad=UnsupportedTurnover(valid);
        var calls=0;
        var result=await AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>throw new InvalidOperationException("No path repair allowed."),
            (match,_)=>Task.FromResult(NarrationFixtures.Json(match)),null,CancellationToken.None,fixture:fixture,
            refinePhases:(document,_,_)=>{calls++;Assert.Equal(fixture,document.Fixture);return Task.FromResult(Repair(valid));});
        Assert.Equal(1,calls);
        var parsed=AuthoredMatchPlan.Parse(result,expectedFixture:fixture);
        Assert.Equal(new[]{home,away},parsed.Teams.Select(t=>t.Id));
        Assert.Equal(fixture.Players.Select(p=>p.Id),parsed.Players.Select(p=>p.Id));
        Assert.Equal(valid.Events,AuthoredMatchPlan.Document(result).Events);
    }
    [Fact]
    public async Task Multiple_unsupported_turnover_and_finish_tags_are_reported_and_corrected_in_the_same_single_slot()
    {
        var original=Baseline();var kinds=original.PhaseKinds.ToArray();kinds[0]="turnover";kinds[3]="finish";
        var bad=original with{PhaseKinds=kinds};
        var failure=Assert.Throws<AuthoredPhaseValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
        Assert.Equal(new[]{"phase-1","phase-4"},failure.Issues.Select(i=>i.PhaseId));
        Assert.Contains("phase-4 (90,120]s",failure.Message);
        var corrections=0;var narration=0;
        var output=await AuthoredGenerationPipeline.RunAsync(_=>Task.FromResult(Json(bad)),
            (_,_,_)=>throw new InvalidOperationException("No additional shape repair."),
            (match,_)=>{narration++;return Task.FromResult(NarrationFixtures.Json(match));},null,CancellationToken.None,
            refinePhases:(_,issues,_)=>{corrections++;Assert.Equal(2,issues.Issues.Count);return Task.FromResult(Repair(original));});
        Assert.Equal(1,corrections);Assert.Equal(1,narration);
        Assert.Equal(original.PhaseKinds,AuthoredMatchPlan.Document(output).PhaseKinds);
    }
    [Fact]
    public void Completion_boundaries_are_exclusive_start_inclusive_end_not_release_time_or_nearby_interception()
    {
        var kinds=Enumerable.Repeat("possession",10).ToArray();
        var events=Enumerable.Range(0,10).Select(i=>new MatchEvent("completed-"+i,(i+1)*30,0,EventKind.Pass,
            "a","b",true,i*30+29)).ToArray();
        kinds[0]="turnover";
        var crossing=events.ToArray();crossing[1]=new("interception",32,0,EventKind.Pass,"a","opponent",false,29);
        var failure=Assert.Throws<AuthoredPhaseValidationException>(()=>AuthoredPhaseEvidence.Validate(crossing,kinds));
        Assert.Equal("phase-1",Assert.Single(failure.Issues).PhaseId);
        Assert.DoesNotContain(failure.Issues[0].Observed,e=>e.Id=="interception");
        kinds[0]="possession";kinds[1]="turnover";AuthoredPhaseEvidence.Validate(crossing,kinds);
        var boundary=events.ToArray();boundary[0]=events[0] with{ReceiverId="opponent",Completed=false};
        failure=Assert.Throws<AuthoredPhaseValidationException>(()=>AuthoredPhaseEvidence.Validate(boundary,kinds));
        Assert.Equal("phase-2",Assert.Single(failure.Issues).PhaseId);
        kinds[0]="turnover";kinds[1]="possession";AuthoredPhaseEvidence.Validate(boundary,kinds);
        var goalEvents=events.Append(new MatchEvent("observed-goal",60,1,EventKind.Goal,"a")).ToArray();
        kinds[1]="finish";kinds[0]="possession";AuthoredPhaseEvidence.Validate(goalEvents,kinds);
        kinds[1]="possession";kinds[2]="finish";
        Assert.Equal("phase-3",Assert.Single(Assert.Throws<AuthoredPhaseValidationException>(
            ()=>AuthoredPhaseEvidence.Validate(goalEvents,kinds)).Issues).PhaseId);
    }
    [Fact]
    public void Correction_cannot_move_reorder_drop_duplicate_or_add_phase_ids_or_change_scenario_fields()
    {
        var original=Baseline();var tags=AuthoredPhaseRefinement.Tags(original);
        string With(PhaseTag[] phases)=>JsonSerializer.Serialize(new AuthoredPhaseCorrection(phases),PhaseCorrectionJsonContext.Default.AuthoredPhaseCorrection);
        var applied=AuthoredPhaseRefinement.Apply(original,With(tags));
        Assert.Same(original.Players,applied.Players);Assert.Same(original.Events,applied.Events);
        Assert.Same(original.Fixture,applied.Fixture);Assert.Equal(original.InitialOwnerId,applied.InitialOwnerId);
        foreach(var invalid in new[]{
            tags.Skip(1).ToArray(),tags.Append(tags[0]).ToArray(),
            tags.Select((p,i)=>i==0?p with{Id="phase-2"}:p).ToArray(),
            tags.Select((p,i)=>i==0?p with{Id="unknown"}:p).ToArray(),
            tags.Select((p,i)=>i==0?p with{Start=1}:p).ToArray(),
            tags.Select((p,i)=>i==0?p with{End=31}:p).ToArray(),
            tags.Reverse().ToArray(),tags.Select((p,i)=>i==0?p with{Kind="invented"}:p).ToArray()})
            Assert.Throws<ScenarioValidationException>(()=>AuthoredPhaseRefinement.Apply(original,With(invalid)));
        foreach(var field in new[]{"\"events\":[],","\"players\":[],","\"duration\":299,","\"initialOwnerId\":\"other\",","\"fixture\":{},","\"phaseKinds\":[],"})
            Assert.Throws<ScenarioValidationException>(()=>AuthoredPhaseRefinement.Apply(original,With(tags).Insert(1,field)));
        Assert.Throws<ScenarioValidationException>(()=>AuthoredPhaseRefinement.Apply(original,With(tags).Replace("\"start\":0","\"start\":0,\"start\":0",StringComparison.Ordinal)));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"phases\":null}")]
    [InlineData("{\"phases\":[null]}")]
    [InlineData("{\"phases\":[{\"id\":\"phase-1\",\"kind\":\"possession\"}]}")]
    [InlineData("not-json")]
    public async Task Malformed_phase_correction_is_terminal_before_narration_and_keeps_last_valid_match(string response)
    {
        var initial=DenseNarrationRecording.Create();var paths=0;var phases=0;var shape=0;var narration=0;
        var service=new GenerationService(new Pipeline(token=>AuthoredGenerationPipeline.RunAsync(
            _=>{paths++;return Task.FromResult(Json(UnsupportedTurnover(Baseline())));},
            (_,_,_)=>{shape++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},
            null,token,refinePhases:(_,_,_)=>{phases++;return Task.FromResult(response);})),initial);
        await service.GenerateAsync(null,CancellationToken.None);
        Assert.Equal(GenerationState.Failed,service.State);Assert.Same(initial,service.Current);
        Assert.Equal(1,paths);Assert.Equal(1,phases);Assert.Equal(0,shape);Assert.Equal(0,narration);
    }
    [Fact]
    public async Task Phase_then_shape_failure_spends_only_one_repair_and_never_calls_narration_or_shape_repair()
    {
        var rejected=AuthoredMatchPlan.Document(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","rejected-depth-paths.json")));
        Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(rejected,false));
        var bad=UnsupportedTurnover(rejected);var phase=0;var shape=0;var narration=0;
        await Assert.ThrowsAsync<AuthoredPathValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>{shape++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phase++;return Task.FromResult(Repair(rejected));}));
        Assert.Equal(1,phase);Assert.Equal(0,shape);Assert.Equal(0,narration);
    }
    [Fact]
    public async Task Shape_repair_cannot_introduce_phase_changes_or_spend_another_phase_repair()
    {
        var rejected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","rejected-depth-paths.json"));
        var corrected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-depth-correction.json"));
        var phase=0;var shape=0;var narration=0;
        var failure=await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(rejected),(document,_,_)=>{shape++;document.PhaseKinds[0]="finish";return Task.FromResult(corrected);},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phase++;throw new InvalidOperationException();}));
        Assert.Contains("immutable",failure.Message);
        Assert.Equal(0,phase);Assert.Equal(1,shape);Assert.Equal(0,narration);
    }
    [Fact]
    public async Task Invalid_retagging_and_mutated_adapter_remain_rejected_without_another_response()
    {
        var original=Baseline();var bad=UnsupportedTurnover(original);var phases=0;var narration=0;
        var failure=await Assert.ThrowsAsync<AuthoredPhaseValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>{narration++;throw new InvalidOperationException();},
            null,CancellationToken.None,refinePhases:(_,_,_)=>{phases++;return Task.FromResult(Repair(bad));}));
        Assert.Contains("phase-1",failure.Message);Assert.Equal(1,phases);Assert.Equal(0,narration);
        var mutated=await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            null,CancellationToken.None,refinePhases:(document,_,_)=>{
                document.Players[0].Frames[0][1]+=.1;return Task.FromResult(Repair(original));}));
        Assert.Contains("immutable",mutated.Message);
    }
    [Fact]
    public async Task Cancellation_timeout_and_auth_failure_do_not_start_a_hidden_extra_stage()
    {
        var original=Baseline();var bad=UnsupportedTurnover(original);using var token=new CancellationTokenSource();
        var narration=0;var shape=0;var phases=0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>{shape++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,token.Token,
            refinePhases:(_,_,_)=>{phases++;token.Cancel();return Task.FromResult(Repair(original));}));
        Assert.Equal(1,phases);Assert.Equal(0,narration);Assert.Equal(0,shape);
        phases=0;
        await Assert.ThrowsAsync<TimeoutException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
            null,CancellationToken.None,refinePhases:(_,_,_)=>{phases++;throw new TimeoutException();}));
        Assert.Equal(1,phases);
        phases=0;
        await Assert.ThrowsAsync<CopilotGenerationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>throw new CopilotGenerationException("Controlled auth failure"),(_,_,_)=>throw new InvalidOperationException(),
            (_,_)=>throw new InvalidOperationException(),null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        Assert.Equal(0,phases);
    }
    [Fact]
    public async Task Missing_phase_evidence_without_correction_adapter_is_terminal_and_empty_phases_are_not_repairable_by_tags()
    {
        var paths=0;var shape=0;var narration=0;
        await Assert.ThrowsAsync<AuthoredPhaseValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>{paths++;return Task.FromResult(Json(UnsupportedTurnover(Baseline())));},
            (_,_,_)=>{shape++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None));
        Assert.Equal(1,paths);Assert.Equal(0,shape);Assert.Equal(0,narration);
        var events=Enumerable.Range(1,9).Select(i=>new MatchEvent("action-"+i,i*30+1,0,EventKind.Carry,"a",StartTime:i*30)).ToArray();
        var failure=Assert.Throws<ScenarioValidationException>(()=>AuthoredPhaseEvidence.Validate(events,Enumerable.Repeat("possession",10).ToArray()));
        Assert.IsNotType<AuthoredPhaseValidationException>(failure);
        Assert.Contains("cannot invent events",failure.Message);
    }
    private sealed class Pipeline(Func<CancellationToken,Task<string>> run):IScenarioSource
    {public Task<string> GenerateAsync(IProgress<string>? progress,CancellationToken token)=>run(token);}
    private sealed class Messages:IProgress<string>
    {public List<string> Items{get;}=[];public void Report(string value)=>Items.Add(value);}
}
