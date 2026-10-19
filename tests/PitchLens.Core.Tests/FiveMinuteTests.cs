using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class FiveMinuteTests
{
    [Fact]
    public void Sol_recording_is_a_real_complete_five_minute_match_not_a_retimed_legacy_sample()
    {
        var match = FiveMinuteRecording.Create();
        Assert.Equal(300, match.Duration);
        Assert.Equal("gpt-6.1-sol", FiveMinuteRecording.Model);
        Assert.Equal(82, match.Events.Count);
        Assert.Equal(42, match.Events.Count(e => e.Kind == EventKind.Pass));
        Assert.Equal(28, match.Events.Count(e => e.Kind == EventKind.Carry));
        Assert.Equal(4, match.Events.Count(e => e.Kind == EventKind.Shot));
        Assert.Equal(4, match.Events.Count(e => e.Kind == EventKind.Goal));
        Assert.Equal(4, match.Events.Count(e => e.Kind == EventKind.Kickoff));
        Assert.Equal(7, match.Events.Count(e => e.Kind == EventKind.Pass && e.Completed == false));
        Assert.Equal(10, match.Story!.Beats.Count);
        Assert.All(match.Players, player => Assert.Equal(300, player.Path.Frames[^1].Time));
        Assert.True(match.Events.Last().Time >= 295);
        Assert.Equal(72, RecordedScenario.Create().Duration); // Historical recording was not relabeled.
        SustainedQuality.Validate(match);
    }

    [Fact]
    public void Every_sliding_window_has_visible_action_and_both_clubs_participate_in_every_minute()
    {
        var match = FiveMinuteRecording.Create();
        for (double start = 0; start <= 280; start += 10)
            foreach (var team in match.Teams)
                Assert.Equal(5, match.Players.Count(p => p.TeamId == team.Id && !StoryQuality.IsKeeper(p) &&
                    p.Path.Measure(start + 20).Distance - p.Path.Measure(start).Distance >= 8));
        foreach (var time in new[] { 0d, 30, 66.8, 90, 120, 150, 194, 226, 270, 300 })
        {
            Assert.InRange(match.Player("arsenal-raya").Path.At(time).X, 0, 18);
            Assert.InRange(match.Player("manchester-city-donnarumma").Path.At(time).X, 87, 105);
            var snapshot = Replay.At(match, time);
            Assert.All(snapshot.Events, e => Assert.True(e.Time <= time));
            Assert.All(StoryReplay.At(match, time), beat => Assert.True(beat.Time <= time));
        }
    }

    [Fact]
    public void Ball_possession_receiver_arrival_and_score_are_coherent_throughout300s()
    {
        var match = FiveMinuteRecording.Create();
        for (var i = 0; i <= 3000; i++)
        {
            var time = i / 10d;
            if (match.OwnerAt(time) is { } owner)
                Assert.True(match.Player(owner).Path.At(time).DistanceTo(match.Ball.At(time)) < 1e-8, $"Detached ball at {time}");
        }
        foreach (var pass in match.Events.Where(e => e.Kind == EventKind.Pass))
            Assert.True(match.Player(pass.ReceiverId!).Path.At(pass.Time).DistanceTo(match.Ball.At(pass.Time)) < 1e-8);
        Assert.Equal(2, Replay.At(match, 300).Score["arsenal"]);
        Assert.Equal(2, Replay.At(match, 300).Score["manchester-city"]);
        Assert.Equal(0, Replay.At(match, 0).Players.Sum(p => p.Goals));
        Assert.Equal(4, Replay.At(match, 300).Players.Sum(p => p.Goals));
    }

    [Fact]
    public void Goals_are_exact_playback_timed_seek_safe_and_do_not_duplicate()
    {
        var match = FiveMinuteRecording.Create();
        foreach (var goal in match.Events.Where(e => e.Kind == EventKind.Goal))
        {
            Assert.Null(GoalReplay.At(match, goal.Time - .001));
            Assert.Equal(goal.Id, GoalReplay.At(match, goal.Time)!.EventId);
            Assert.Equal(goal.Id, GoalReplay.At(match, goal.Time + GoalReplay.CelebrationSeconds - .001)!.EventId);
            Assert.Null(GoalReplay.At(match, goal.Time + GoalReplay.CelebrationSeconds));
            Assert.Equal(Replay.At(match, goal.Time).Score, GoalReplay.At(match, goal.Time)!.Score);
        }
        Assert.Null(GoalReplay.At(match, 0));
    }

    [Fact]
    public void Fulltime_toggle_restarts_and_plays_instead_of_a_dead_play_control()
    {
        var clock = new TestClock();
        using var controller = new ReplayPresentation(FiveMinuteRecording.Create(), clock);
        controller.Toggle();
        clock.Seconds = 301;
        var end = controller.Sample(true)!;
        Assert.Equal(300, end.Snapshot.Time);
        Assert.False(end.Playing);
        controller.Toggle();
        var replay = controller.Sample(true)!;
        Assert.Equal(0, replay.Snapshot.Time);
        Assert.True(replay.Playing);
        Assert.Empty(replay.Explanations);
        clock.Seconds += 40;
        controller.Toggle();
        var paused = controller.Sample(true)!;
        Assert.Equal(120, paused.Snapshot.Time);
        clock.Seconds += 100;
        Assert.Equal(120, controller.Sample(true)!.Snapshot.Time);
        controller.Seek(194);
        Assert.Equal(194, controller.Sample(true)!.Snapshot.Time);
    }

    [Fact]
    public void Frozen_motion_or_a_padded_idle_tail_is_rejected_even_with_good_aggregate_totals()
    {
        var original = FiveMinuteRecording.Create();
        var players = original.Players.Select(p => StoryQuality.IsKeeper(p) ? p : p with
        {
            Path = new Trajectory(p.Path.Frames.Select(f => f.Time <= 120 ? f : f with { Position = p.Path.At(120) }))
        }).ToArray();
        var frozen = new Match(300, original.Teams, players, original.Events, original.Ball, original.Possession, original.Story);
        Assert.Throws<ScenarioValidationException>(() => SustainedQuality.Validate(frozen));
        var tail = new Match(300, original.Teams, original.Players, original.Events.Where(e => e.Time <= 260),
            original.Ball, original.Possession, original.Story);
        Assert.Throws<ScenarioValidationException>(() => SustainedQuality.Validate(tail));
    }

    [Fact]
    public void Five_minute_parser_rejects_old_duration_and_unknown_actor_without_faking_success()
    {
        var plan = JsonSerializer.Deserialize(FiveMinuteRecording.Json(), PlanJsonContext.Default.MatchPlan)!;
        Assert.Throws<ScenarioValidationException>(() => FiveMinutePlan.Build(plan with { Duration = 72 }));
        Assert.Throws<ScenarioValidationException>(() => FiveMinutePlan.Build(plan with { Title = "Seventy-Two Seconds" }));
        var phases = plan.Phases.ToArray();
        var actions = phases[0].Actions.ToArray();
        actions[0] = actions[0] with { ActorId = "invented" };
        phases[0] = phases[0] with { Actions = actions };
        Assert.Throws<ScenarioValidationException>(() => FiveMinutePlan.Build(plan with { Phases = phases }));
        Assert.Throws<ScenarioValidationException>(() => FiveMinutePlan.Parse("{}"));
    }

    [Fact]
    public void Five_minute_playback_is_cadence_independent_and_backward_seeks_preserve_selection_not_future_evidence()
    {
        var match = FiveMinuteRecording.Create();
        foreach (var frequency in new[] { 24, 60 })
        {
            var clock = new TestClock();
            using var controller = new ReplayPresentation(match, clock);
            controller.SetRate(1);
            controller.Select("arsenal-saka");
            controller.Play();
            for (var step = 0; step <= 300 * frequency; step++)
            {
                clock.Seconds = step / (double)frequency;
                _ = controller.Sample();
            }
            Assert.InRange(controller.SnapshotComputations, 1200, 1201);
            var complete = controller.Sample(true)!;
            Assert.Equal(82, complete.Snapshot.Events.Count);
            Assert.Equal(10, complete.Story.Count);
            controller.Seek(66.8);
            var back = controller.Sample(true)!;
            Assert.Equal("arsenal-saka", back.SelectedId);
            Assert.All(back.Explanations, e => Assert.True(e.Time <= 66.8));
            Assert.All(back.Story, e => Assert.True(e.Time <= 66.8));
            Assert.Equal(1, back.Snapshot.Players.Sum(p => p.Goals));
            Assert.Equal(Replay.At(match, 66.8).Players.Select(p => p.Distance), back.Snapshot.Players.Select(p => p.Distance));
        }
    }

    [Fact]
    public void Separately_authored_demo_is_also_sustained300s_and_not_the_historical_sdk_recording()
    {
        var demo = FiveMinuteDemo.Create();
        Assert.Equal(300, demo.Duration);
        Assert.NotEqual(FiveMinuteRecording.Create().Events.Select(e => e.Time), demo.Events.Select(e => e.Time));
        SustainedQuality.Validate(demo);
    }
    private sealed class TestClock : IMonotonicClock { public double Seconds { get; set; } }
}
