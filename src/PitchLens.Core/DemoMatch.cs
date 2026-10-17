namespace PitchLens.Core;

public static class DemoMatch
{
    public const uint DefaultSeed = 2026;
    public const double Duration = 72;

    public static Match Create(uint seed = DefaultSeed)
    {
        var teams = new[] { new Team("harbor", "Harbor"), new Team("orchard", "Orchard") };
        var names = new[] { "Reed", "Vale", "Finch", "Lane", "Wren", "Stone",
            "Brook", "Ash", "Rowan", "Field", "Elm", "North" };
        var players = new List<Player>();
        var state = seed == 0 ? 1u : seed;
        double Jitter()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state / (double)uint.MaxValue - .5) * 6;
        }
        for (var i = 0; i < 12; i++)
        {
            var home = i < 6;
            var n = i % 6;
            var id = $"{(home ? "h" : "a")}{n + 1}";
            var origin = new Position(home ? 12 + n * 12 : 93 - n * 10, 12 + n % 3 * 22);
            var frames = Enumerable.Range(0, 19)
                .Select(k => new Keyframe(k * 4, new Position(origin.X + Jitter(), origin.Y + Jitter())))
                .ToList();
            void Route(double start, double end, params Keyframe[] route)
            {
                frames.RemoveAll(f => f.Time >= start && f.Time <= end);
                frames.AddRange(route);
                frames.Sort((a, b) => a.Time.CompareTo(b.Time));
            }
            if (id == "h3")
                Route(24, 72, new(24, new(40, 42)), new(28, new(44, 28)), new(32, new(48, 28)),
                    new(36, new(53, 28)), new(42, new(73, 25)), new(46, new(76, 24)),
                    new(48, new(77, 24)), new(52, new(75, 25)), new(56, new(73, 25)),
                    new(60, new(70, 27)), new(64, new(68, 29)), new(68, new(64, 31)), new(72, new(60, 33)));
            if (id == "h5")
                Route(44, 64, new(44, new(80, 32)), new(48, new(84, 32)),
                    new(54, new(90, 32)), new(56, new(92, 32)), new(60, new(88, 32)), new(64, new(78, 32)));
            if (id == "a3")
                Route(52, 72, new(52, new(58, 35)), new(56, new(52.5, 34)), new(58, new(52.5, 34)),
                    new(60, new(52.5, 34)), new(64, new(55, 34)), new(68, new(60, 35)), new(72, new(65, 38)));
            players.Add(new(id, home ? "harbor" : "orchard", n + 1, names[i], new(frames)));
        }
        var events = new[]
        {
            new MatchEvent("E01", 8, 0, EventKind.Pass, "h1", "h3", true, 6),
            new MatchEvent("E02", 16, 0, EventKind.Pass, "h3", "a2", false, 14),
            new MatchEvent("E03", 24, 0, EventKind.Pass, "a2", "a4", true, 22),
            new MatchEvent("E04", 32, 0, EventKind.Pass, "a4", "h3", false, 30),
            new MatchEvent("E05", 42, 0, EventKind.Carry, "h3", StartTime: 36),
            new MatchEvent("E06", 48, 0, EventKind.Pass, "h3", "h5", true, 46),
            new MatchEvent("E07", 56, 0, EventKind.Shot, "h5", StartTime: 54),
            new MatchEvent("E08", 56, 1, EventKind.Goal, "h5"),
            new MatchEvent("E09", 58, 0, EventKind.Kickoff, "a3"),
            new MatchEvent("E10", 64, 0, EventKind.Pass, "a3", "a4", true, 62)
        };
        var possession = new List<PossessionChange> { new(0, "h1") };
        foreach (var e in events)
        {
            if (e.Kind is EventKind.Pass or EventKind.Shot)
            {
                possession.Add(new(e.StartTime!.Value, null));
                possession.Add(new(e.Time, e.ReceiverId));
            }
            else if (e.Kind == EventKind.Kickoff)
                possession.Add(new(e.Time, e.ActorId));
        }
        Position PlayerAt(string id, double time) => players.Single(p => p.Id == id).Path.At(time);
        Position BallAt(double time)
        {
            var flight = events.FirstOrDefault(e => e.Kind is EventKind.Pass or EventKind.Shot &&
                e.StartTime <= time && time <= e.Time);
            if (flight is not null)
            {
                var from = PlayerAt(flight.ActorId, flight.StartTime!.Value);
                var to = flight.ReceiverId is null ? new Position(105, 34) : PlayerAt(flight.ReceiverId, flight.Time);
                return Position.Lerp(from, to, (time - flight.StartTime.Value) / (flight.Time - flight.StartTime.Value));
            }
            var owner = possession.Last(p => p.Time <= time).PlayerId;
            return owner is null ? new(105, 34) : PlayerAt(owner, time);
        }
        // Include every path and flight boundary so attached ball motion shares the player's interpolation.
        var times = players.SelectMany(p => p.Path.Frames.Select(f => f.Time))
            .Concat(possession.Select(p => p.Time)).Distinct().Order().ToArray();
        var ball = new Trajectory(times.Select(t => new Keyframe(t, BallAt(t))));
        return new(Duration, teams, players, events, ball, possession);
    }
}
