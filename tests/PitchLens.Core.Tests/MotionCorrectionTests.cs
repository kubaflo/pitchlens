using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

// Deliberate offline perturbations of a saved fixture; never claimed as the failed Live payload.
public sealed class MotionCorrectionTests
{
    private static AuthoredMatchDocument Baseline()=>FixtureTests.Control(FixtureCatalog.Create("liverpool","chelsea")) with{Narration=null};
    private static AuthoredMatchDocument Perturb(AuthoredMatchDocument original,double dy=3,double dx=0)
        =>PitchLens.Tools.MotionWitnessInput.Perturb(original,"liverpool-kerkez",69,dy,dx);
    private static string Json(AuthoredMatchDocument document)=>JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument);
    private static string Correction(AuthoredMatchDocument original)=>JsonSerializer.Serialize(
        new AuthoredPathCorrection(original.Players),CorrectionJsonContext.Default.AuthoredPathCorrection);
    [Fact]
    public async Task Controlled_velocity_change_at69_is_red_with_exact_original_knots_then_one_paths_only_repair_is_green()
    {
        var original=Baseline();Assert.NotNull(AuthoredMatchPlan.Build(original,false));
        var bad=Perturb(original);
        var failure=Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
        var issue=Assert.Single(failure.MotionIssues,i=>i.PlayerId=="liverpool-kerkez"&&i.Time==69);
        Assert.Equal("velocity change m/s²",issue.Kind);Assert.Equal(6,issue.Actual,9);Assert.Equal(2.5,issue.Maximum);
        Assert.Equal(new[]{68d,69,70},issue.Frames.Select(f=>f.Time));Assert.Empty(failure.Issues);
        Assert.Contains("liverpool-kerkez",failure.Message);Assert.Contains("69s",failure.Message);
        // Remove just the injected abrupt deviation, retaining its extra sampling knots.
        Assert.NotNull(AuthoredMatchPlan.Build(Perturb(original,0),false));
        Assert.NotNull(AuthoredMatchPlan.Build(Perturb(original,1.249999),false));
        Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(Perturb(original,1.250001),false));
        var paths=0;var repair=0;var phases=0;var narration=0;var progress=new Messages();
        var output=await AuthoredGenerationPipeline.RunAsync(_=>{paths++;return Task.FromResult(Json(bad));},
            (document,measured,_)=>{
                repair++;Assert.Equal(69,measured.MotionIssues.First().Time);
                var prompt=AuthoredPathRefinement.Prompt(document,measured);
                Assert.Contains("ORIGINAL adjacent knots",prompt);Assert.Contains("2.5m/s²",prompt);
                Assert.Contains("events are IMMUTABLE",prompt);Assert.Contains("liverpool-kerkez",prompt);
                return Task.FromResult(Correction(original));
            },(match,_)=>{narration++;return Task.FromResult(NarrationFixtures.Json(match));},
            progress,CancellationToken.None,fixture:original.Fixture,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException("No second phase correction.");});
        var result=AuthoredMatchPlan.Document(output);
        Assert.Equal(1,paths);Assert.Equal(1,repair);Assert.Equal(0,phases);Assert.Equal(1,narration);
        Assert.Equal(original.Events,result.Events);Assert.Equal(original.PhaseKinds,result.PhaseKinds);
        Assert.Equal(original.InitialOwnerId,result.InitialOwnerId);Assert.Equal(original.Duration,result.Duration);
        foreach(var p in original.Players)Assert.Equal(JsonSerializer.Serialize(p),JsonSerializer.Serialize(result.Players.Single(r=>r.Id==p.Id)));
        Assert.NotNull(AuthoredMatchPlan.Parse(output).Narrative);
        Assert.Contains(progress.Items,m=>m.Contains("speed/velocity changes")&&m.Contains("events and phases unchanged"));
        Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(bad,false)); // Reinstating trigger remains red.
    }
    [Fact]
    public void Measured_speed_above_existing_eight_metre_limit_is_typed_but_threshold_and_nonfinite_rejection_stay_strict()
    {
        var failure=Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(Perturb(Baseline(),0,12),false));
        Assert.Contains(failure.MotionIssues,i=>i.Kind=="speed m/s"&&i.Actual>8&&i.Maximum==8);
        var original=Baseline();var invalid=Perturb(original,double.NaN);
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(invalid,false));
        var first=original.Players[0];
        var tiny=first.Frames.Take(1).Concat(new[]{new[]{1e-320,first.Frames[0][1]+1,first.Frames[0][2]}})
            .Concat(first.Frames.Skip(1)).ToArray();
        var overflow=original with{Players=original.Players.Select(p=>p.Id==first.Id?p with{Frames=tiny}:p).ToArray()};
        var numerical=Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(overflow,false));
        Assert.Contains("Nonfinite",numerical.Message);
    }
    [Fact]
    public async Task Failed_motion_repair_never_retries_or_requests_narration()
    {
        var bad=Perturb(Baseline());var repair=0;var phases=0;var narration=0;
        await Assert.ThrowsAsync<AuthoredPathValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(_,_,_)=>{repair++;return Task.FromResult(Correction(bad));},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        Assert.Equal(1,repair);Assert.Equal(0,phases);Assert.Equal(0,narration);
    }
    [Fact]
    public async Task Phase_then_motion_failure_cannot_use_a_second_repair_slot()
    {
        var original=Baseline();var bad=Perturb(original);var kinds=bad.PhaseKinds.ToArray();kinds[0]="turnover";
        var phase=0;var motion=0;var narration=0;
        var correction=JsonSerializer.Serialize(new AuthoredPhaseCorrection(AuthoredPhaseRefinement.Tags(original)),
            PhaseCorrectionJsonContext.Default.AuthoredPhaseCorrection);
        await Assert.ThrowsAsync<AuthoredPathValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad with{PhaseKinds=kinds})),(_,_,_)=>{motion++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phase++;return Task.FromResult(correction);}));
        Assert.Equal(1,phase);Assert.Equal(0,motion);Assert.Equal(0,narration);
    }
    [Theory]
    [InlineData("events")]
    [InlineData("phases")]
    [InlineData("fixture")]
    [InlineData("paths")]
    public async Task Motion_correction_adapter_cannot_mutate_original_scenario_or_trigger_another_repair(string field)
    {
        var bad=Perturb(Baseline());var repairs=0;var phases=0;
        var failure=await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad)),(document,_,_)=>{
                repairs++;
                if(field=="events")document.Events[0]=document.Events[0] with{Time=document.Events[0].Time+.1};
                if(field=="phases")document.PhaseKinds[0]="finish";
                if(field=="fixture")document.Fixture!.Players[0]=document.Fixture.Players[0] with{Name="Changed"};
                if(field=="paths")document.Players[0].Frames[0][1]+=.1;
                return Task.FromResult(Correction(Baseline()));
            },(_,_)=>throw new InvalidOperationException("No narration after mutation."),null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        Assert.Contains("immutable",failure.Message);Assert.Equal(1,repairs);Assert.Equal(0,phases);
    }
    [Fact]
    public async Task Malformed_paths_and_auth_errors_are_not_repair_eligibility()
    {
        var repairs=0;var phases=0;
        await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult("{\"players\":[]}"),(_,_,_)=>{repairs++;throw new InvalidOperationException();},
            (_,_)=>throw new InvalidOperationException(),null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        await Assert.ThrowsAsync<CopilotGenerationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>throw new CopilotGenerationException("Controlled authentication error"),(_,_,_)=>{repairs++;throw new InvalidOperationException();},
            (_,_)=>throw new InvalidOperationException(),null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        Assert.Equal(0,repairs);Assert.Equal(0,phases);
    }
    private sealed class Messages:IProgress<string>
    {public List<string> Items{get;}=[];public void Report(string message)=>Items.Add(message);}
}
