using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;
using PitchLens.Tools;

namespace PitchLens.Core.Tests;

public sealed class ActivityCorrectionTests
{
    private static AuthoredMatchDocument Original()=>AuthoredMatchPlan.Document(AuthoredMatchRecording.Json()) with{Commentary=null,Narration=null};
    private static string Json(AuthoredMatchDocument d)=>JsonSerializer.Serialize(d,AuthoredJsonContext.Default.AuthoredMatchDocument);
    private static string Correction(AuthoredMatchDocument d)=>JsonSerializer.Serialize(new AuthoredPathCorrection(d.Players),CorrectionJsonContext.Default.AuthoredPathCorrection);
    [Theory]
    [InlineData(2,3,8,false)]
    [InlineData(2,4,8,true)]
    [InlineData(1,5,8,false)]
    [InlineData(2,4,7.999999,false)]
    public void Exact_rolling_window_requires_six_total_two_each_and_eight_metres_including_partial_segments(int home,int away,double travel,bool valid)
    {
        var original=AuthoredMatchPlan.Build(Original(),false);
        var selected=original.Teams.SelectMany((team,n)=>original.Players.Where(p=>p.TeamId==team.Id&&!original.IsKeeper(p.Id)).Take(n==0?home:away)).Select(p=>p.Id).ToHashSet();
        var players=original.Players.Select(p=>{
            if(original.IsKeeper(p.Id))return p;
            var a=p.Path.At(0);var distance=selected.Contains(p.Id)?travel*1.5:0;
            var b=new Position(a.X+(a.X<90?distance:-distance),a.Y);
            return p with{Path=new([new(0,a),new(200,a),new(230,b),new(300,b)])};
        }).ToArray();
        var match=new Match(300,original.Teams,players,original.Events,original.Ball,original.Possession);
        var measured=SustainedQuality.MeasureActivity(match,200);
        Assert.Equal(200,measured.Start);Assert.Equal(220,measured.End);Assert.Equal(8,measured.QualifyingDistanceMetres);
        Assert.Equal(valid,measured.Measures.All(m=>m.Actual>=m.Limit));
        Assert.All(measured.PlayerDistances!.Where(p=>selected.Contains(p.PlayerId)),p=>Assert.Equal(travel,p.Metres,7));
    }
    [Fact]
    public async Task Controlled_200_220_activity_red_trigger_removed_green_shared_correction_green_reinstated_red()
    {
        var original=Original();Assert.NotNull(AuthoredMatchPlan.Build(original,false));
        var bad=ActivityWitnessInput.FreezeAwayWindow(original);
        var rejected=Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
        var window=Assert.Single(rejected.QualityIssues,i=>i.Kind=="visible-movement"&&i.Start==200&&i.End==220);
        Assert.Equal(8,window.QualifyingDistanceMetres);Assert.Equal(20,window.PlayerIds.Length);
        Assert.Equal(6,window.Measures[0].Limit);
        Assert.All(window.Measures.Skip(1),m=>Assert.Equal(2,m.Limit));
        Assert.All(window.PlayerDistances!.Where(p=>p.TeamId=="manchester-city"),p=>Assert.Equal(0,p.Metres,8));
        Assert.Equal(0,window.Measures.Single(m=>m.SubjectId=="manchester-city").Actual);
        Assert.NotNull(AuthoredMatchPlan.Build(original,false)); // Remove the controlled trigger, not a local product fix.
        var initial=AuthoredMatchRecording.Create();var paths=0;var repairs=0;var phases=0;var narration=0;
        GenerationService? service=null;
        service=new GenerationService(new Pipeline(t=>AuthoredGenerationPipeline.RunAsync(
            _=>{paths++;return Task.FromResult(Json(bad));},
            (document,failure,_)=>{
                repairs++;Assert.Same(initial,service!.Current);
                Assert.Contains(failure.QualityIssues,i=>i.Kind=="visible-movement"&&i.Start==200&&i.End==220);
                Assert.Contains(">=6 TOTAL",AuthoredPathRefinement.Prompt(document,failure));
                return Task.FromResult(Correction(original));
            },(match,_)=>{narration++;return Task.FromResult(NarrationFixtures.Json(match));},null,t,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException("No second correction.");})),initial);
        await service.GenerateAsync(null,CancellationToken.None);
        Assert.Equal(GenerationState.Succeeded,service.State);
        Assert.Equal(1,paths);Assert.Equal(1,repairs);Assert.Equal(0,phases);Assert.Equal(1,narration);
        Assert.Equal(original.Events,service.Current.Events.Select(e=>new EventDocument(e.Id,e.Time,e.Order,e.Kind.ToString().ToLowerInvariant(),e.ActorId,e.ReceiverId,e.Completed,e.StartTime)));
        Assert.Throws<AuthoredPathValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
    }
    [Fact]
    public async Task Uncorrected_activity_is_terminal_after_one_repair_without_narration_and_reports_which_stage_failed()
    {
        var bad=ActivityWitnessInput.FreezeAwayWindow(Original());var repair=0;var narration=0;var initial=AuthoredMatchRecording.Create();
        var service=new GenerationService(new Pipeline(t=>AuthoredGenerationPipeline.RunAsync(_=>Task.FromResult(Json(bad)),
            (_,_,_)=>{repair++;return Task.FromResult(Correction(bad));},(_,_)=>{narration++;throw new InvalidOperationException();},null,t)),initial);
        await service.GenerateAsync(null,CancellationToken.None);
        Assert.Equal(1,repair);Assert.Equal(0,narration);Assert.Same(initial,service.Current);
        Assert.Contains("one shared AI path correction",service.Status);
        Assert.Contains("visible-movement",service.Status);
    }
    [Fact]
    public async Task Phase_correction_then_activity_failure_cannot_use_a_second_slot()
    {
        var original=Original();var bad=ActivityWitnessInput.FreezeAwayWindow(original);
        var kinds=bad.PhaseKinds.ToArray();kinds[0]=kinds[0]=="turnover"?"finish":"turnover";
        var phases=0;var paths=0;var narration=0;
        var correction=JsonSerializer.Serialize(new AuthoredPhaseCorrection(AuthoredPhaseRefinement.Tags(original)),PhaseCorrectionJsonContext.Default.AuthoredPhaseCorrection);
        var failure=await Assert.ThrowsAsync<AuthoredPathValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(Json(bad with{PhaseKinds=kinds})),(_,_,_)=>{paths++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;return Task.FromResult(correction);}));
        Assert.Equal(1,phases);Assert.Equal(0,paths);Assert.Equal(0,narration);
        Assert.Equal("after-phase-correction",failure.Data["PitchLens.ValidationStage"]);
    }
    [Theory]
    [InlineData("event")]
    [InlineData("nonfinite-json")]
    public async Task Malformed_input_with_activity_defect_stays_terminal_before_any_correction(string defect)
    {
        var bad=ActivityWitnessInput.FreezeAwayWindow(Original());
        var events=bad.Events.ToArray();events[0]=events[0] with{ActorId="invented"};
        var json=defect=="event"?Json(bad with{Events=events}):Json(bad).Replace("\"duration\":300","\"duration\":NaN",StringComparison.Ordinal);
        var repairs=0;var phases=0;var narration=0;
        var failure=await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(json),(_,_,_)=>{repairs++;throw new InvalidOperationException();},
            (_,_)=>{narration++;throw new InvalidOperationException();},null,CancellationToken.None,
            refinePhases:(_,_,_)=>{phases++;throw new InvalidOperationException();}));
        Assert.Equal(0,repairs);Assert.Equal(0,phases);Assert.Equal(0,narration);
        Assert.Equal("initial",failure.Data["PitchLens.ValidationStage"]);
    }
    private sealed class Pipeline(Func<CancellationToken,Task<string>> run):IScenarioSource
    {public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken t)=>run(t);}
}
