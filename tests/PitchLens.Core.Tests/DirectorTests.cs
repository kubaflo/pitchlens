using System.Text.Json;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class DirectorTests
{
    [Fact]
    public async Task Actual_recorded_edit_is_58_seconds_and_preview_is_explicit_not_a_fresh_request()
    {
        var match = NarrationRecording.Create();
        Assert.False(DirectorRecording.Available(match, 1, 299.9));
        Assert.False(DirectorRecording.Available(match, 2, 300));
        var recorded = DirectorRecording.Load(match, 1, 300);
        Assert.False(recorded.Fresh);
        Assert.Equal(DirectorRecording.Hash, recorded.Evidence.Hash);
        Assert.Equal(58, FilmTimeline.Duration(recorded.Plan));
        Assert.Equal(new[] { "switch-the-point", "wide-to-central-payoff", "possession-changes-direction", "answer-through-the-flank" },
            recorded.Plan.Scenes.Select(s => s.Id));
        Assert.Equal(78, FilmTimeline.At(recorded.Plan, 9).SourceTime);
        Assert.True(FilmTimeline.At(recorded.Plan, 9).Holding);
        Assert.Equal(79, FilmTimeline.At(recorded.Plan, 12).SourceTime);
        Assert.Equal(80, FilmTimeline.At(recorded.Plan, 14).SourceTime);
        Assert.Equal(86, FilmTimeline.At(recorded.Plan, 26).SourceTime);
        Assert.Equal(152, FilmTimeline.At(recorded.Plan, 30).SourceTime);
        Assert.Equal(165, FilmTimeline.At(recorded.Plan, 43).SourceTime);
        Assert.Equal(176, FilmTimeline.At(recorded.Plan, 58).SourceTime);
        var source = new Source(); source.Release.SetResult();
        using var native = new MatchService(new NoScenario(), new NoMoment(), (_, _) => Task.CompletedTask, source);
        native.Command("seek", 300); native.PreviewRecordedDirector();
        Assert.True(native.FilmActive); Assert.False(native.Director.Current!.Fresh);
        Assert.Equal(0, source.Calls);
        native.FilmCommand("exit");
        await native.DirectCurrentAsync();
        Assert.Equal(1, source.Calls);
        Assert.True(native.Director.Current!.Fresh);
    }
    internal static FilmPlan Plan(DirectorEvidence evidence)
    {
        FilmScene Scene(string id, double start, double end, double hold, string camera)
        {
            var fact = evidence.Facts.Events.Last(e => e.Time == end);
            return new(id, start, end, 1, hold, camera, fact.Id, [fact.Id],
                camera == "finish" ? "The recorded move reaches its finish." : "The next observed sequence changes the route.");
        }
        return new(evidence.Hash, evidence.Revision, evidence.Cutoff,
            [Scene("first", 75, 86, 3, "finish"), Scene("turn", 115, 125, 3, "tactical"),
                Scene("level", 166, 176, 4, "finish"), Scene("response", 189, 199, 3, "tactical")]);
    }
    internal static string Json(FilmPlan plan) => JsonSerializer.Serialize(plan, DirectorJsonContext.Default.FilmPlan);
    internal static DirectorEvidence Capture() => DirectorEvidence.Capture(NarrationRecording.Create(), 1, 300);

    [Fact]
    public void Captured_facts_have_no_future_knots_or_events_and_invalid_plans_fail_strictly()
    {
        using var schema = JsonDocument.Parse(DirectorPrompt.Schema);
        Assert.Equal(4, schema.RootElement.GetProperty("properties").GetProperty("scenes").GetProperty("minItems").GetInt32());
        var match = NarrationRecording.Create();
        Assert.False(DirectorEvidence.CanCapture(match, 60));
        Assert.Throws<InvalidOperationException>(() => DirectorEvidence.Capture(match, 1, 85));
        var clipped = DirectorEvidence.Capture(match, 1, 86.5);
        Assert.All(clipped.Facts.Events, e => Assert.True(e.Time <= 86.5));
        Assert.All(clipped.Facts.Players, p => { Assert.Equal(86.5, p.Path[^1].Time); Assert.All(p.Path, f => Assert.True(f.Time <= 86.5)); });
        var evidence = Capture(); var plan = Plan(evidence);
        Assert.Equal(54, FilmTimeline.Duration(evidence.Validate(Json(plan))));
        foreach (var bad in new[]
        {
            plan with { FactsHash = "wrong" }, plan with { Revision = 2 }, plan with { Cutoff = 299 },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { Rate = 3 } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { SourceEnd = 301 } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { HoldSeconds = 7 } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { EvidenceIds = ["unknown"] } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { EvidenceIds = [s.FocusEventId, plan.Scenes[2].FocusEventId] } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select((s,i) => i==0 ? s with { Text = "An invented 99 percent xG winner." } : s).ToArray() },
            plan with { Scenes = plan.Scenes.Select(s => s with { Rate = .5 }).ToArray() }
        }) Assert.Throws<ScenarioValidationException>(() => evidence.Validate(Json(bad)));
        Assert.Throws<ScenarioValidationException>(() => evidence.Validate(Json(plan).Insert(1,"\"extra\":1,")));
        Assert.Throws<ScenarioValidationException>(() => clipped.Validate(Json(plan)));
    }

    [Fact]
    public void Film_clock_maps_exact_cuts_holds_and_slow_motion_without_base_rate_or_highlight_multiplication()
    {
        var plan = Plan(Capture());
        Assert.Equal(75, FilmTimeline.At(plan, 0).SourceTime);
        Assert.Equal(86, FilmTimeline.At(plan, 11).SourceTime);
        Assert.True(FilmTimeline.At(plan, 11).Holding);
        Assert.Equal(86, FilmTimeline.At(plan, 13.999).SourceTime);
        Assert.Equal(115, FilmTimeline.At(plan, 14).SourceTime);
        Assert.Equal(199, FilmTimeline.At(plan, 54).SourceTime);
        var clock = new Clock(); var playback = new FilmPlayback(plan, clock);
        playback.Play(); clock.Seconds = 14;
        Assert.Equal(115, playback.Frame.SourceTime);
        playback.Pause(); clock.Seconds = 200;
        Assert.Equal(14, playback.Frame.FilmTime);
        playback.Seek(53); playback.Play(); clock.Seconds = 201;
        Assert.Equal(54, playback.Frame.FilmTime); Assert.False(playback.Playing);
        playback.Restart(); Assert.Equal(0, playback.Frame.FilmTime);
        var slow = plan with { Scenes = [plan.Scenes[0] with { Rate = .5 }] };
        Assert.Equal(76, FilmTimeline.At(slow, 2).SourceTime);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Native_film_keeps_main_position_speed_selection_and_source_facts_when_exiting(double rate)
    {
        var source = new Source(); source.Release.SetResult();
        using var native = new MatchService(new NoScenario(), new NoMoment(), (_, _) => Task.CompletedTask, source);
        native.Command("seek", 300); native.Command("rate", rate); native.Command("select", id: "arsenal-saka");
        await native.DirectCurrentAsync();
        var match = native.Match;
        native.WatchDirector(); native.FilmCommand("toggle"); native.FilmCommand("seek", 0);
        var setup = native.Sample(true)!.Frame;
        Assert.Equal(75, setup.Snapshot.Time); Assert.Equal(0, setup.Snapshot.Score["arsenal"]); Assert.False(setup.HighlightsEnabled);
        native.FilmCommand("seek", 11);
        Assert.Equal(86, native.Sample(true)!.Frame.Snapshot.Time);
        Assert.Equal(1, native.Sample(true)!.Frame.Snapshot.Score["arsenal"]);
        native.FilmCommand("exit");
        var returned = native.Sample(true)!.Frame;
        Assert.Equal(300, returned.Snapshot.Time); Assert.Equal(rate, returned.Rate);
        Assert.Equal("arsenal-saka", returned.SelectedId); Assert.False(returned.Playing);
        Assert.Same(match, native.Match);
        await native.DirectCurrentAsync(); Assert.Equal(1, source.Calls); Assert.True(native.Director.Current!.Cached);
    }

    [Fact]
    public async Task Request_admission_busy_cancel_failure_stale_and_cache_keep_valid_match_and_earlier_film()
    {
        var source = new Source(); var director = new DirectorService(source); director.ChangeScene(1);
        var evidence = Capture(); var pending = director.CreateAsync(evidence);
        Assert.True(director.Busy); Assert.True(director.Activity.Snapshot.Active);
        await director.CreateAsync(evidence); Assert.Equal(1, source.Calls);
        source.Release.SetResult(); await pending;
        var previous = director.Current;
        source.Invalid = true;
        await director.CreateAsync(evidence, true);
        Assert.Same(previous, director.Current);
        Assert.Equal(RequestStage.Failed, director.Activity.Snapshot.Stage);
        Assert.Contains("failed", director.Status);
        var calls = source.Calls;
        await director.CreateAsync(evidence);
        Assert.Equal(calls, source.Calls);
        source.Invalid = false;
        source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = director.CreateAsync(evidence, true); director.Cancel(); await pending;
        Assert.Same(previous, director.Current);
        Assert.False(director.Activity.Snapshot.Active);
        source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = director.CreateAsync(evidence, true); director.ChangeScene(2); source.Release.SetResult(); await pending;
        Assert.Null(director.Current); Assert.False(director.Activity.Snapshot.Active);
    }
    [Fact]
    public async Task Timeout_unload_and_explicit_evidence_exit_do_not_change_canonical_match_or_publish_late_film()
    {
        var source = new Source();
        var service = new DirectorService(source, TimeSpan.FromMilliseconds(20)); service.ChangeScene(1);
        await service.CreateAsync(Capture());
        Assert.False(service.Activity.Snapshot.Active);
        Assert.Equal(RequestStage.Failed, service.Activity.Snapshot.Stage);
        Assert.Contains("timed out", service.Status);
        using var native = new MatchService(new NoScenario(), new NoMoment(), (_, _) => Task.CompletedTask, source);
        native.Command("seek", 300);
        var pending = native.DirectCurrentAsync();
        native.Dispose(); await pending;
        Assert.Null(native.Director.Current);
        source = new Source(); source.Release.SetResult();
        using var film = new MatchService(new NoScenario(), new NoMoment(), (_, _) => Task.CompletedTask, source);
        film.Command("seek", 300); await film.DirectCurrentAsync(); film.WatchDirector();
        var evidence = MomentEvidence.Capture(film.Match, 1, 86, "e28");
        film.InspectEvidence(evidence, "e26");
        Assert.False(film.FilmActive); Assert.Equal(84, film.Sample(true)!.Frame.Snapshot.Time);
        Assert.False(film.Sample(true)!.Frame.Playing);
    }
    internal sealed class Source : IDirectorSource
    {
        public int Calls { get; private set; }
        public bool Invalid { get; set; }
        public TaskCompletionSource Release { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token)
        { Calls++; progress?.Report("Sol is choosing observed clips…"); await Release.Task.WaitAsync(token); return Invalid ? "{}" : Json(Plan(evidence)); }
    }
    internal sealed class NoScenario : IScenarioSource
    { public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) => throw new InvalidOperationException("No paid calls."); }
    internal sealed class NoMoment : IMomentSource
    { public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) => throw new InvalidOperationException("No paid calls."); }
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
}
