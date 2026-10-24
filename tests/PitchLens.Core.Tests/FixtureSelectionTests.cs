using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class FixtureSelectionTests
{
    [Fact]
    public void All_thirty_ordered_manual_pairs_are_exact_and_invalid_choices_never_become_random()
    {
        var pairs=0;
        foreach(var home in FixtureCatalog.Clubs)foreach(var away in FixtureCatalog.Clubs)
        {
            var choice=new FixtureChoice(FixtureChoiceKind.Manual,home.Id,away.Id);
            if(home.Id==away.Id){Assert.Throws<ArgumentException>(()=>choice.Capture());continue;}
            var fixture=choice.Capture()!;pairs++;
            Assert.Equal(home,fixture.Home);Assert.Equal(away,fixture.Away);
            Assert.Equal(22,fixture.Players.Length);Assert.Equal(11,fixture.Players.Count(p=>p.TeamId==home.Id));
            Assert.Equal(fixture,FixtureCatalog.RequireKnown(fixture) with{Players=fixture.Players});
        }
        Assert.Equal(30,pairs);Assert.Null(FixtureChoice.Random.Capture());
        foreach(var choice in new[]{new FixtureChoice((FixtureChoiceKind)9),new(FixtureChoiceKind.Manual,"unknown","chelsea"),
            new(FixtureChoiceKind.Manual,"liverpool","unknown"),new(FixtureChoiceKind.Random,"unknown",null)})
            Assert.Throws<ArgumentException>(()=>choice.Capture());
    }
    [Fact]
    public async Task Remembered_Live_waits_without_history_activity_or_SDK_on_start_activation_selection_and_cancel()
    {
        var source=new CaptureSource();var writes=0;
        var variety=new MatchVariety(saveSelection:_=>writes++);
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No auth"),source,
            readyDemo:false,mode:SessionMode.Live,variety:variety);
        await native.StartSessionAsync();await native.StartSessionAsync();
        Assert.True(native.FixturePickerOpen);Assert.Empty(source.Requests);Assert.Equal(0,writes);
        Assert.False(native.GenerationActivity.Snapshot.Active);Assert.Null(native.RequestedFixture);
        Assert.Throws<ArgumentException>(()=>native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"unknown","chelsea")));
        Assert.Throws<ArgumentException>(()=>native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"chelsea","chelsea")));
        Assert.Empty(source.Requests);Assert.Equal(0,writes);
        native.Suspend();native.Activate();
        native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"liverpool","chelsea"));
        native.SetFixtureChoice(FixtureChoice.Random);native.CancelFixturePicker();
        await native.StartSessionAsync();
        Assert.False(native.FixturePickerOpen);Assert.Empty(source.Requests);Assert.Equal(0,writes);
        await native.ChooseModeAsync(SessionMode.Live);
        Assert.True(native.FixturePickerOpen);Assert.Empty(source.Requests);Assert.Equal(0,writes);
    }
    [Theory]
    [InlineData("liverpool","chelsea")]
    [InlineData("manchester-united","tottenham-hotspur")]
    public async Task Submit_captures_exact_fixture_once_freezes_choice_and_preserves_current_game_on_cancel(string home,string away)
    {
        var source=new CaptureSource{Wait=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        var writes=0;var variety=new MatchVariety(saveSelection:_=>writes++);
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,variety:variety);
        native.Command("seek",86);native.LoadRecordedMoment();native.Command("play");
        var old=native.Match;var oldAnswer=native.Explainer.Current;var frame=native.Sample(true)!.Frame;
        native.OpenFixturePicker();Assert.True(native.Sample(true)!.Frame.Playing);
        native.SetFixtureChoice(new(FixtureChoiceKind.Manual,home,away));
        var job=native.SubmitFixtureAsync();var again=native.SubmitFixtureAsync();
        Assert.Single(source.Requests);Assert.Equal(1,writes);
        Assert.Equal(home,source.Requests[0].Fixture!.Home.Id);Assert.Equal(away,source.Requests[0].Fixture!.Away.Id);
        Assert.Equal(FixtureCatalog.Create(home,away).Players,source.Requests[0].Fixture!.Players);
        Assert.Throws<InvalidOperationException>(()=>native.SetFixtureChoice(FixtureChoice.Random));
        Assert.Same(old,native.Match);Assert.Same(oldAnswer,native.Explainer.Current);
        native.Cancel();await job;await again;
        Assert.Same(old,native.Match);Assert.Same(oldAnswer,native.Explainer.Current);
        Assert.Equal(frame.SelectedId,native.Sample(true)!.Frame.SelectedId);
        Assert.Equal(GenerationState.Cancelled,native.RequestState);
    }
    [Fact]
    public async Task Manual_rematch_is_admissible_but_semantic_duplicate_rejection_and_random_rotation_remain()
    {
        var fixture=FixtureCatalog.Create("liverpool","chelsea");var writes=0;
        var variety=new MatchVariety(previousPair:fixture.PairKey,saveSelection:_=>writes++);
        Assert.Equal(fixture.Id,variety.Next(fixture).Fixture!.Id);
        Assert.Equal(fixture.Id,variety.Next(fixture).Fixture!.Id);Assert.Equal(2,writes);
        Assert.NotEqual(fixture.PairKey,variety.Next().Fixture!.PairKey);
        var source=new CaptureSource();
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,variety:new MatchVariety());
        native.OpenFixturePicker();native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"liverpool","chelsea"));
        await native.SubmitFixtureAsync();Assert.Equal(GenerationState.Succeeded,native.RequestState);
        var current=native.Match;
        native.OpenFixturePicker();await native.SubmitFixtureAsync();
        Assert.Equal(GenerationState.Failed,native.RequestState);Assert.Same(current,native.Match);
        Assert.Contains("near-identical",native.Status);
    }
    [Fact]
    public async Task Test_picker_is_nonspending_and_cannot_submit_without_explicit_selection()
    {
        var source=new CaptureSource();
        using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,
            readyDemo:false,mode:SessionMode.Test);
        await native.StartSessionAsync();
        native.CancelFixturePicker();
        Assert.Throws<InvalidOperationException>(()=>{_ = native.SubmitFixtureAsync();});
        Assert.Throws<InvalidOperationException>(()=>native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"liverpool","chelsea")));
        native.OpenFixturePicker();
        native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"liverpool","chelsea"));
        Assert.Equal(SessionMode.Test,native.Mode);Assert.False(native.Fresh);Assert.Empty(source.Requests);
        native.CancelFixturePicker();
        Assert.Equal(new[]{"arsenal","manchester-city"},native.Match.Teams.Select(t=>t.Id));
        native.StartGuidedDemo();native.ExitGuidedDemo();Assert.Empty(source.Requests);
    }
    [Fact]
    public async Task Picker_and_submit_guards_reject_busy_connection_film_and_tour_without_mode_changes()
    {
        var source=new CaptureSource();
        var connection=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var native=new MatchService(source,source,(_,token)=>connection.Task.WaitAsync(token),source);
        native.OpenFixturePicker();
        var check=native.CheckConnectionAsync();
        Assert.False(native.CanChooseFixture);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        Assert.Throws<InvalidOperationException>(()=>{_ = native.SubmitFixtureAsync();});
        native.CancelConnection();await check;
        native.CancelFixturePicker();
        await native.ChooseModeAsync(SessionMode.Test,false);
        native.Command("seek",300);native.PreviewRecordedDirector();
        Assert.False(native.CanChooseFixture);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        native.FilmCommand("exit");native.StartGuidedDemo();
        Assert.False(native.CanChooseFixture);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        native.ExitGuidedDemo();Assert.True(native.CanChooseFixture);
        native.OpenFixturePicker();source.Wait=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request=native.SubmitFixtureAsync();
        Assert.False(native.CanChooseFixture);Assert.Equal(SessionMode.Live,native.Mode);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        Assert.Throws<InvalidOperationException>(()=>native.SetFixtureChoice(FixtureChoice.Random));
        var repeated=native.SubmitFixtureAsync();Assert.Single(source.Requests);
        native.Cancel();await request;await repeated;
        Assert.Equal(GenerationState.Cancelled,native.RequestState);Assert.False(native.Fresh);
        Assert.True(native.CanChooseFixture);Assert.False(native.GuidedDemoActive);
    }
    [Fact]
    public async Task Picker_cannot_replace_busy_explanation_or_director_requests()
    {
        var source=new CaptureSource();var actions=new WaitingActions();
        using var native=new MatchService(source,actions,(_,_)=>Task.CompletedTask,actions);
        native.Command("seek",120);native.OpenFixturePicker();
        var explain=native.ExplainCurrentMomentAsync();
        Assert.True(native.Explainer.Busy);Assert.False(native.CanChooseFixture);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        Assert.Throws<InvalidOperationException>(()=>{_ = native.SubmitFixtureAsync();});
        native.Explainer.Cancel();await explain;
        Assert.True(native.CanChooseFixture);
        var director=native.DirectCurrentAsync();
        Assert.True(native.Director.Busy);Assert.False(native.CanChooseFixture);
        Assert.Throws<InvalidOperationException>(native.OpenFixturePicker);
        Assert.Throws<InvalidOperationException>(()=>{_ = native.SubmitFixtureAsync();});
        native.Director.Cancel();await director;
        Assert.True(native.CanChooseFixture);Assert.Empty(source.Requests);
    }
    [Fact]
    public async Task Invalid_manual_fixture_is_rejected_before_core_activity_or_history_and_capture_does_not_alias_input()
    {
        var source=new CaptureSource{Wait=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        var writes=0;var core=new GenerationService(source,AuthoredMatchRecording.Create(),variety:new MatchVariety(saveSelection:_=>writes++));
        var fixture=FixtureCatalog.Create("liverpool","chelsea");
        var bad=fixture with{Players=fixture.Players.Select((p,i)=>i==0?p with{Name="Invalid identity"}:p).ToArray()};
        await Assert.ThrowsAsync<ScenarioValidationException>(()=>core.GenerateAsync(null,CancellationToken.None,bad));
        Assert.Empty(source.Requests);Assert.Equal(0,writes);Assert.Equal(GenerationState.Ready,core.State);
        using var cancel=new CancellationTokenSource();
        var job=core.GenerateAsync(null,cancel.Token,fixture);
        fixture.Players[0]=fixture.Players[0] with{Name="Mutated caller object"};
        Assert.NotEqual("Mutated caller object",source.Requests.Single().Fixture!.Players[0].Name);
        Assert.Equal(FixtureCatalog.Create("liverpool","chelsea").Players,core.RequestedFixture!.Players);
        cancel.Cancel();await job;
    }
    private sealed class WaitingActions:IMomentSource,IDirectorSource
    {
        private readonly TaskCompletionSource<string> pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> ExplainAsync(MomentEvidence evidence,CancellationToken token)=>pending.Task.WaitAsync(token);
        public Task<string> CreateAsync(DirectorEvidence evidence,IProgress<string>? progress,CancellationToken token)=>pending.Task.WaitAsync(token);
    }
    internal sealed class CaptureSource:IScenarioSource,IMomentSource,IDirectorSource
    {
        public List<VarietyRequest> Requests=[];
        public TaskCompletionSource? Wait;
        public bool Fail;
        public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken t)=>throw new InvalidOperationException("Fixture request required.");
        public async Task<string> GenerateAsync(VarietyRequest request,IProgress<string>? p,CancellationToken t)
        {Requests.Add(request);if(Wait is not null)await Wait.Task.WaitAsync(t);if(Fail)throw new CopilotGenerationException("Controlled selected-fixture failure.");return FixtureTests.Json(request.Fixture!);}
        public Task<string> ExplainAsync(MomentEvidence e,CancellationToken t)=>throw new InvalidOperationException("No Explain");
        public Task<string> CreateAsync(DirectorEvidence e,IProgress<string>?p,CancellationToken t)=>throw new InvalidOperationException("No Director");
    }
}
