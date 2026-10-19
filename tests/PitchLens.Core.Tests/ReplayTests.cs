using PitchLens.Core;

namespace PitchLens.Core.Tests;

public sealed class ReplayTests
{
    private readonly Match match = DemoMatch.Create();

    [Fact]
    public void Final_totals_are_exact_and_team_scores_agree()
    {
        var s = Replay.At(match, 72);
        Assert.Equal(10, s.Events.Count);
        Assert.Equal(6, s.Players.Sum(p => p.Passes));
        Assert.Equal(4, s.Players.Sum(p => p.CompletedPasses));
        Assert.Equal(1, s.Players.Sum(p => p.Shots));
        Assert.Equal(1, s.Players.Sum(p => p.Goals));
        Assert.Equal(1, s.Score["harbor"]);
        Assert.Equal(0, s.Score["orchard"]);
        Assert.Equal(new[] { 1, 0, 2, 0, 0, 0, 0, 1, 1, 1, 0, 0 }, s.Players.Select(p => p.Passes));
        Assert.Equal(1, s.Events.Count(e => e.Kind == EventKind.Carry));
    }

    [Fact]
    public void Statistics_and_insights_do_not_leak_future_events_or_double_count_after_seeks()
    {
        var end = Replay.At(match, 72);
        foreach (var time in new[] { 0d, 7.999, 8, 15, 32, 55.999, 56, 0, 72, 8, 72 })
        {
            var s = Replay.At(match, time);
            Assert.All(s.Events, e => Assert.True(e.Time <= time));
            Assert.Equal(match.Events.Count(e => e.Time <= time && e.Kind == EventKind.Pass), s.Players.Sum(p => p.Passes));
            Assert.Equal(time < 56 ? 0 : 1, s.Score["harbor"]);
            foreach (var mode in Enum.GetValues<ExplanationMode>())
            {
                var insights = Explanations.At(match, time, mode);
                Assert.Equal(s.Events.Select(e => e.Id), insights.Select(e => e.EventId));
                Assert.All(insights, e => Assert.True(e.Time <= time));
            }
        }
        Assert.Equal(end.Players, Replay.At(match, 72).Players);
        Assert.Empty(Explanations.At(match, 0, ExplanationMode.Analyst));
        Assert.DoesNotContain(Explanations.At(match, 55.999, ExplanationMode.Analyst),
            e => e.EventId is "E07" or "E08");
    }

    [Fact]
    public void Equal_timestamp_events_preserve_explicit_order()
    {
        var events = Replay.At(match, 56).Events.Where(e => e.Time == 56).ToArray();
        Assert.Equal(new[] { "E07", "E08" }, events.Select(e => e.Id));
        Assert.Equal(new[] { EventKind.Shot, EventKind.Goal }, events.Select(e => e.Kind));
    }

    [Fact]
    public void Zero_attempt_accuracy_is_unavailable_not_zero()
    {
        Assert.All(Replay.At(match, 0).Players, p => Assert.Null(p.PassAccuracy));
        Assert.Equal(1d, Replay.At(match, 8).Players.Single(p => p.PlayerId == "h1").PassAccuracy);
        Assert.Equal(0d, Replay.At(match, 16).Players.Single(p => p.PlayerId == "h3").PassAccuracy);
        Assert.Equal(.5, Replay.At(match, 72).Players.Single(p => p.PlayerId == "h3").PassAccuracy);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2.5, 2.5)]
    [InlineData(2, 5, 2.5)]
    [InlineData(3, 9, 4)]
    [InlineData(5, 13, 4)]
    [InlineData(10, 13, 4)]
    public void Trajectory_distance_and_speed_include_partial_segments(double time, double distance, double speed)
    {
        var path = new Trajectory(new[] { new Keyframe(0, new(0, 0)), new(2, new(3, 4)),
            new Keyframe(4, new(11, 4)), new(6, new(11, 4)) });
        Assert.Equal(distance, path.Measure(time).Distance, 10);
        Assert.Equal(speed, path.Measure(time).MaxSpeed, 10);
        Assert.Equal(new Position(1.5, 2), path.At(1));
        Assert.Equal(new Position(7, 4), path.At(3));
    }

    [Fact]
    public void Demo_carry_has_exact_partial_distance_and_speed()
    {
        var path = match.Player("h3").Path;
        var fullCarry = Math.Sqrt(409);
        Assert.Equal(fullCarry / 2, path.Measure(39).Distance - path.Measure(36).Distance, 9);
        var carryText = Explanations.At(match, 42, ExplanationMode.Analyst).Single(e => e.EventId == "E05").Text;
        Assert.Contains(fullCarry.ToString("F1"), carryText);
        Assert.Equal(Math.Max(path.Measure(36).MaxSpeed, fullCarry / 6), path.Measure(42).MaxSpeed, 9);
    }

    [Fact]
    public void Same_seed_and_time_are_identical_and_different_seed_changes_paths()
    {
        var again = DemoMatch.Create();
        var other = DemoMatch.Create(17);
        Assert.Equal(match.Events, again.Events);
        foreach (var p in match.Players)
        {
            Assert.Equal(p.Path.Frames, again.Player(p.Id).Path.Frames);
            Assert.NotEqual(p.Path.At(0), other.Player(p.Id).Path.At(0));
        }

        foreach (var t in new[] { 0d, 3.713, 8, 39, 56, 64, 72 })
        {
            Assert.Equal(Replay.At(match, t).Players, Replay.At(again, t).Players);
            Assert.Equal(match.Ball.At(t), again.Ball.At(t));
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(17u)]
    [InlineData(2026u)]
    [InlineData(uint.MaxValue)]
    public void Generated_player_speeds_remain_below_eight_metres_per_second(uint seed)
    {
        var scenario = DemoMatch.Create(seed);
        Assert.All(scenario.Players, p => Assert.InRange(p.Path.Measure(72).MaxSpeed, 0, 8));
    }

    [Fact]
    public void Ball_is_attached_to_owner_and_passes_and_shot_interpolate_to_destinations()
    {
        for (var i = 0; i <= 720; i++)
        {
            var time = i / 10d;
            var owner = match.OwnerAt(time);
            if (owner is not null)
                Assert.True(match.Player(owner).Path.At(time).DistanceTo(match.Ball.At(time)) < 1e-9, $"Ball detached at {time}");
        }
        foreach (var e in match.Events.Where(e => e.Kind is EventKind.Pass or EventKind.Shot))
        {
            var start = e.StartTime!.Value;
            var from = match.Player(e.ActorId).Path.At(start);
            var to = e.ReceiverId is null ? new Position(105, 34) : match.Player(e.ReceiverId).Path.At(e.Time);
            Assert.Equal(from, match.Ball.At(start));
            Assert.Equal(to, match.Ball.At(e.Time));
            Assert.True(Position.Lerp(from, to, .5).DistanceTo(match.Ball.At((start + e.Time) / 2)) < 1e-9);
            Assert.Null(match.OwnerAt((start + e.Time) / 2));
        }
    }

    [Fact]
    public void Possession_and_event_chain_are_coherent()
    {
        foreach (var e in match.Events)
        {
            if (e.Kind is EventKind.Pass or EventKind.Shot)
                Assert.Equal(e.ActorId, match.OwnerAt(e.StartTime!.Value - .001));
            if (e.Kind == EventKind.Pass) Assert.Equal(e.ReceiverId, match.OwnerAt(e.Time));
            if (e.Kind == EventKind.Carry)
                for (var t = e.StartTime!.Value; t <= e.Time; t += .25) Assert.Equal(e.ActorId, match.OwnerAt(t));
        }
        Assert.Null(match.OwnerAt(57));
        Assert.Equal("a3", match.OwnerAt(58));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(60)]
    [InlineData(144)]
    public void Playback_is_independent_of_render_frequency(int frequency)
    {
        var clock = new FakeClock();
        var player = new Playback(72, clock);
        player.SetRate(1);
        player.Play();
        for (var i = 0; i < 50 * frequency; i++)
        {
            clock.Seconds = i / (double)frequency;
            _ = Replay.At(match, player.Position);
        }
        clock.Seconds = 51.12345;
        Assert.Equal(51.12345, player.Position);
        Assert.Equal(Replay.At(match, 51.12345).Players, Replay.At(match, player.Position).Players);
    }

    [Fact]
    public void Playback_pause_resume_seek_end_and_restart_have_exact_semantics()
    {
        var clock = new FakeClock();
        var player = new Playback(72, clock);
        player.SetRate(1);
        player.Play();
        clock.Seconds = 12;
        player.Pause();
        clock.Seconds = 100;
        Assert.Equal(12, player.Position);
        player.Play();
        clock.Seconds = 103;
        Assert.Equal(15, player.Position);
        player.Seek(4);
        Assert.True(player.IsPlaying);
        clock.Seconds = 105;
        Assert.Equal(6, player.Position);
        player.Seek(-10);
        Assert.Equal(0, player.Position);
        player.Seek(100);
        Assert.Equal(72, player.Position);
        Assert.False(player.IsPlaying);
        player.Play();
        Assert.False(player.IsPlaying);
        player.Restart();
        Assert.Equal(0, player.Position);
        Assert.False(player.IsPlaying);
        player.Play();
        clock.Seconds += 80;
        Assert.Equal(72, player.Position);
        Assert.False(player.IsPlaying);
        player.Seek(10);
        Assert.False(player.IsPlaying);
        player.Play();
        clock.Seconds++;
        Assert.Equal(11, player.Position);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Invalid_playback_and_query_times_are_rejected(double time)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Playback(time, new FakeClock()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Playback(72, new FakeClock()).Seek(time));
        Assert.Throws<ArgumentOutOfRangeException>(() => Replay.At(match, time));
        Assert.Throws<ArgumentOutOfRangeException>(() => match.Player("h1").Path.At(time));
        Assert.Throws<ArgumentOutOfRangeException>(() => match.Player("h1").Path.Measure(time));
        Assert.Throws<ArgumentOutOfRangeException>(() => Explanations.At(match, time, ExplanationMode.Casual));
    }

    [Fact]
    public void Queries_clamp_outside_match_bounds()
    {
        Assert.Equal(Replay.At(match, 0).Players, Replay.At(match, -10).Players);
        Assert.Equal(Replay.At(match, 72).Players, Replay.At(match, 100).Players);
        Assert.Empty(Replay.At(match, -10).Events);
    }

    private sealed class FakeClock : IMonotonicClock
    {
        public double Seconds { get; set; }
    }
}
