using PitchLens.Core;

namespace PitchLens.Core.Tests;

public sealed class ValidationTests
{
    private readonly Match valid = DemoMatch.Create();
    private Match Copy(double? duration = null, IEnumerable<Team>? teams = null, IEnumerable<Player>? players = null,
        IEnumerable<MatchEvent>? events = null, Trajectory? ball = null, IEnumerable<PossessionChange>? possession = null) =>
        new(duration ?? valid.Duration, teams ?? valid.Teams, players ?? valid.Players,
            events ?? valid.Events, ball ?? valid.Ball, possession ?? valid.Possession);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_durations_are_rejected(double duration) =>
        Assert.Throws<ArgumentException>(() => Copy(duration: duration));

    [Fact]
    public void Duplicate_and_unknown_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Copy(teams: new[] { valid.Teams[0], valid.Teams[0] }));
        Assert.Throws<ArgumentException>(() => Copy(players: valid.Players.Append(valid.Players[0])));
        Assert.Throws<ArgumentException>(() => Copy(players: valid.Players.Select(p => p with { TeamId = "unknown" })));
        Assert.Throws<ArgumentException>(() => Copy(events: valid.Events.Select(e => e with { ActorId = "unknown" })));
        Assert.Throws<ArgumentException>(() => Copy(events: valid.Events.Select(e => e with { Id = "duplicate" })));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { ReceiverId = "unknown" } }));
    }

    [Fact]
    public void Invalid_event_order_timestamps_and_semantics_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Copy(events: valid.Events.Reverse()));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[7], valid.Events[6] }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { Time = double.NaN } }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { Time = 73 } }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { StartTime = 8 } }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { Completed = false } }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[0] with { ReceiverId = null } }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[7] }));
        Assert.Throws<ArgumentException>(() => Copy(events: new[] { valid.Events[4] with { StartTime = null } }));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(106, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 69)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_coordinates_are_rejected(double x, double y) =>
        Assert.Throws<ArgumentException>(() => Copy(ball: new(new[] { new Keyframe(0, new(x, y)), new(72, new(1, 1)) })));

    [Fact]
    public void Invalid_trajectory_timestamps_are_rejected()
    {
        foreach (var times in new[] { new[] { 1d, 72 }, new[] { 0d, 71 }, new[] { 0d, 0, 72 },
            new[] { 0d, 12, 11, 72 }, new[] { 0d, double.NaN, 72 } })
            Assert.Throws<ArgumentException>(() => Copy(ball: new(times.Select(t => new Keyframe(t, new(1, 1))))));
        Assert.Throws<ArgumentException>(() => Copy(ball: new(Array.Empty<Keyframe>())));
    }

    [Fact]
    public void Invalid_possession_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Copy(possession: Array.Empty<PossessionChange>()));
        Assert.Throws<ArgumentException>(() => Copy(possession: new[] { new PossessionChange(0, "unknown") }));
        Assert.Throws<ArgumentException>(() => Copy(possession: valid.Possession.Reverse()));
        Assert.Throws<ArgumentException>(() => Copy(possession: new[] { new PossessionChange(1, "h1") }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Empty_event_scenarios_still_validate_explanation_query_time(double time)
    {
        var empty = Copy(events: Array.Empty<MatchEvent>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Explanations.At(empty, time, ExplanationMode.Casual));
        Assert.Empty(Explanations.At(empty, 10, ExplanationMode.Analyst));
    }
}
