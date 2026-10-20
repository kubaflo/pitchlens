using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class GoalHighlightPaceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Authored_shot_is_one_wall_second_and_base_rate_restores_exactly_at_kickoff(double rate)
    {
        var match = AuthoredMatchRecording.Create();
        var clock = new Clock();
        var windows = GoalHighlights.For(match);
        Assert.Equal(new[] { 76d, 166d }, windows.Select(w => w.BuildUpStart));
        using var surface = new ReplayPresentation(match, clock);
        surface.SetRate(rate);
        foreach (var window in windows)
        {
            surface.Pause(); surface.Seek(window.ShotStart); surface.Play();
            var start = clock.Seconds;
            Assert.Equal(1, surface.Sample(true)!.EffectiveRate);
            clock.Seconds = start + 1;
            Assert.Equal(window.GoalTime, surface.Position, 8);
            clock.Seconds = start + 3;
            Assert.Equal(window.GoalTime + 2, surface.Position, 8);
            surface.Seek(window.End);
            var resume = clock.Seconds;
            Assert.Equal(rate, surface.Sample(true)!.EffectiveRate);
            clock.Seconds = resume + 1;
            Assert.Equal(window.End + rate, surface.Position, 8);
            Assert.Equal(rate, surface.Rate);
        }
    }

    [Fact]
    public void Window_crossing_rate_changes_pause_resume_and_disable_reanchor_without_jumps()
    {
        var clock = new Clock();
        var playback = new Playback(300, clock, [new(76, 96), new(166, 186)]);
        playback.Seek(75); playback.Play();
        clock.Seconds = 1d / 3 + 10;
        Assert.Equal(86, playback.Position, 8);
        playback.SetRate(5);
        Assert.Equal(86, playback.Position, 8);
        Assert.Equal(1, playback.EffectiveRate);
        clock.Seconds++;
        Assert.Equal(87, playback.Position, 8);
        playback.Pause(); clock.Seconds += 40;
        Assert.Equal(87, playback.Position, 8);
        playback.Play(); clock.Seconds++;
        Assert.Equal(88, playback.Position, 8);
        playback.SetHighlights(false);
        Assert.Equal(88, playback.Position, 8);
        clock.Seconds++;
        Assert.Equal(93, playback.Position, 8);
        playback.Seek(playback.Position - 5);
        Assert.Equal(88, playback.Position, 8);
        playback.SetHighlights(true);
        Assert.Equal(88, playback.Position, 8);
        clock.Seconds++;
        Assert.Equal(89, playback.Position, 8);
    }

    [Theory]
    [InlineData(1, 300)]
    [InlineData(3, 126.66666666666667)]
    [InlineData(5, 92)]
    public void Native_goal_policy_is_frequency_independent_and_caps_match_at_actual_duration(double rate, double wallToEnd)
    {
        foreach (var fps in new[] { 24, 60, 144 })
        {
            var clock = new Clock();
            var playback = new Playback(300, clock, [new(76, 96), new(166, 186)]);
            playback.SetRate(rate); playback.Play();
            for (var frame = 0; frame < 50 * fps; frame++) { clock.Seconds = frame / (double)fps; _ = playback.Position; }
            clock.Seconds = wallToEnd + .000001;
            Assert.Equal(300, playback.Position);
            Assert.False(playback.IsPlaying);
            playback.Restart();
            Assert.Equal(0, playback.Position);
            Assert.Equal(rate, playback.Rate);
        }
    }

    [Fact]
    public void Observed_goal_watch_uses_actual_buildup_and_cannot_reveal_unseen_goal()
    {
        var clock = new Clock();
        using var surface = new ReplayPresentation(AuthoredMatchRecording.Create(), clock);
        var goal = surface.Match.Events.First(e => e.Kind == EventKind.Goal);
        Assert.Throws<InvalidOperationException>(() => surface.WatchGoal(goal.Id));
        surface.Seek(goal.Time + 1);
        surface.WatchGoal(goal.Id);
        Assert.Equal(76, surface.Position);
        Assert.True(surface.Playing);
        Assert.Equal(1, surface.Sample(true)!.EffectiveRate);
        surface.SetRate(5); surface.SetHighlights(false);
        surface.Replace(AuthoredMatchRecording.Create());
        Assert.Equal(5, surface.Rate); Assert.False(surface.HighlightsEnabled);
        surface.Seek(85);
        Assert.Equal(5, surface.Sample(true)!.EffectiveRate);
    }

    [Fact]
    public void Goal_pace_does_not_raise_native_snapshot_cadence()
    {
        var clock = new Clock();
        using var surface = new ReplayPresentation(AuthoredMatchRecording.Create(), clock);
        surface.SetRate(5); surface.Seek(75); surface.Play();
        for (var frame = 0; frame <= 600; frame++) { clock.Seconds = frame / 60d; _ = surface.Sample(); }
        Assert.InRange(surface.SnapshotComputations, 40, 41);
        Assert.InRange(surface.Position, 85, 86);
        Assert.All(surface.Sample(true)!.Snapshot.Events, e => Assert.True(e.Time <= surface.Position));
    }
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
}
