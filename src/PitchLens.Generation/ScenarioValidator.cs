using PitchLens.Core;

namespace PitchLens.Generation;

public static class ScenarioValidator
{
    public static Match Parse(string json) => Build(ScenarioParser.ParseDocument(json));

    public static Match Build(ScenarioDocument document)
    {
        static void Require(bool valid, string message)
        {
            if (!valid) throw new ScenarioValidationException(message);
        }
        Require(document.Duration == 72, "Generated replay must be exactly 72 seconds.");
        Require(document.Players is { Length: 12 } && document.Players.All(p => p is not null) &&
            document.Players.Select(p => p.Id).Distinct().Count() == 12, "Expected all 12 unique verified roster IDs.");
        Require(document.Events is { Length: >= 5 and <= 20 } && document.Events.All(e => e is not null),
            "Expected 5-20 ordered synthetic events.");
        Require(document.Events!.All(e => e.Id is { Length: >= 1 and <= 40 } &&
            e.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')), "Event IDs must be short ASCII letters, digits or hyphens.");
        var players = new List<Player>();
        foreach (var entry in document.Players!)
        {
            var roster = PremierLeagueRoster.Players.SingleOrDefault(p => p.Id == entry.Id)
                ?? throw new ScenarioValidationException("Unknown roster ID; use only the verified player list.");
            Require(entry.Frames is { Length: >= 2 and <= 40 } && entry.Frames.All(f => f is not null),
                $"Invalid trajectory frames for {entry.Id}.");
            players.Add(new(roster.Id, roster.TeamId, roster.Number, roster.Name,
                new(entry.Frames!.Select(f => new Keyframe(f.Time, new(f.X, f.Y))))));
        }
        players = PremierLeagueRoster.Players.Select(r => players.Single(p => p.Id == r.Id)).ToList();
        Require(players.Any(p => p.Id == document.InitialOwnerId), "Unknown initial possession ID.");
        var events = document.Events!.Select(e =>
        {
            var kind = e.Kind switch
            {
                "pass" => EventKind.Pass,
                "carry" => EventKind.Carry,
                "shot" => EventKind.Shot,
                "goal" => EventKind.Goal,
                "kickoff" => EventKind.Kickoff,
                _ => throw new ScenarioValidationException("Unknown event kind; use pass, carry, shot, goal or kickoff.")
            };
            return new MatchEvent(e.Id, e.Time, e.Order, kind, e.ActorId, e.ReceiverId, e.Completed, e.StartTime);
        }).ToArray();
        var possession = new List<PossessionChange> { new(0, document.InitialOwnerId) };
        var dummyBall = new Trajectory([new(0, new(52.5, 34)), new(72, new(52.5, 34))]);
        Match skeleton;
        try
        {
            skeleton = new(72, PremierLeagueRoster.Teams, players, events, dummyBall, possession);
        }
        catch (ArgumentException e)
        {
            throw new ScenarioValidationException($"Invalid generated scenario: {e.Message}", e);
        }
        Require(players.All(p => p.Path.Measure(72).MaxSpeed <= 8), "Player movement exceeds the 8 m/s synthetic speed limit.");
        Require(events.Any(e => e.Kind == EventKind.Pass) && events.Any(e => e.Kind == EventKind.Carry) &&
            events.Any(e => e.Kind == EventKind.Goal), "Scenario needs passes, a carry, a shot and a goal.");
        string? owner = document.InitialOwnerId;
        double lastActionEnd = 0;
        MatchEvent? lastGoal = null;
        foreach (var e in events)
        {
            if (e.Kind == EventKind.Goal)
            {
                Require(e.StartTime is null, "Goal cannot have a separate start time.");
                Require(lastGoal is null && events.Count(s => s.Kind == EventKind.Goal && s.Time == e.Time) == 1,
                    "Goal must occur once per shot.");
                lastGoal = e;
                continue;
            }
            if (e.Kind == EventKind.Kickoff)
            {
                Require(lastGoal is not null && e.Time >= lastGoal.Time + 2 && e.Time >= lastActionEnd &&
                    skeleton.Player(e.ActorId).TeamId != skeleton.Player(lastGoal.ActorId).TeamId,
                    "Kickoff must follow a goal after at least two seconds, by the opposing team.");
                Require(skeleton.Player(e.ActorId).Path.At(e.Time).DistanceTo(new(52.5, 34)) <= .01,
                    "Kickoff player must be at the pitch centre.");
                Require(e.StartTime is null, "Kickoff cannot have a flight start.");
                owner = e.ActorId;
                possession.Add(new(e.Time, owner));
                lastActionEnd = e.Time;
                lastGoal = null;
                continue;
            }
            Require(e.StartTime.HasValue && e.StartTime >= lastActionEnd && e.Time - e.StartTime >= 1,
                "Actions must not overlap and must last at least one second.");
            Require(lastGoal is null && e.ActorId == owner, "Action actor must own the ball; a goal needs an opposing kickoff before further play.");
            if (e.Kind == EventKind.Carry)
            {
                Require(skeleton.Player(e.ActorId).Path.Measure(e.Time).Distance -
                    skeleton.Player(e.ActorId).Path.Measure(e.StartTime!.Value).Distance >= 1, "Carry must move at least one metre.");
            }
            else
            {
                Require(e.StartTime > possession[^1].Time, "Flight must start after the last possession change.");
                possession.Add(new(e.StartTime!.Value, null));
                owner = e.ReceiverId;
                possession.Add(new(e.Time, owner));
                if (e.Kind == EventKind.Shot)
                    Require(events.Count(g => g.Kind == EventKind.Goal && g.Time == e.Time && g.ActorId == e.ActorId) == 1,
                        "This bounded prototype supports only explicitly paired shot/goal events.");
                var destination = e.ReceiverId is null
                    ? new Position(skeleton.Player(e.ActorId).TeamId == PremierLeagueRoster.Teams[0].Id ? 105 : 0, 34)
                    : skeleton.Player(e.ReceiverId).Path.At(e.Time);
                var speed = skeleton.Player(e.ActorId).Path.At(e.StartTime.Value).DistanceTo(destination) / (e.Time - e.StartTime.Value);
                Require(speed <= 40, "Generated ball flight exceeds 40 m/s.");
            }
            lastActionEnd = e.Time;
        }
        Position BallAt(double time)
        {
            var flight = events.FirstOrDefault(e => e.Kind is EventKind.Pass or EventKind.Shot && e.StartTime <= time && time <= e.Time);
            if (flight is not null)
            {
                var from = skeleton.Player(flight.ActorId).Path.At(flight.StartTime!.Value);
                var to = flight.ReceiverId is not null ? skeleton.Player(flight.ReceiverId).Path.At(flight.Time)
                    : new Position(skeleton.Player(flight.ActorId).TeamId == PremierLeagueRoster.Teams[0].Id ? 105 : 0, 34);
                return Position.Lerp(from, to, (time - flight.StartTime.Value) / (flight.Time - flight.StartTime.Value));
            }
            var currentOwner = possession.Last(p => p.Time <= time).PlayerId;
            if (currentOwner is not null) return skeleton.Player(currentOwner).Path.At(time);
            var goal = events.Last(e => e.Kind == EventKind.Goal && e.Time <= time);
            var nextKickoff = events.FirstOrDefault(e => e.Kind == EventKind.Kickoff && e.Time > goal.Time);
            var target = new Position(skeleton.Player(goal.ActorId).TeamId == PremierLeagueRoster.Teams[0].Id ? 105 : 0, 34);
            return nextKickoff is null ? target : Position.Lerp(target, new(52.5, 34), Math.Clamp((time - goal.Time) / (nextKickoff.Time - goal.Time), 0, 1));
        }
        var times = players.SelectMany(p => p.Path.Frames.Select(f => f.Time))
            .Concat(events.Select(e => e.Time)).Concat(possession.Select(p => p.Time)).Distinct().Order();
        var ball = new Trajectory(times.Select(t => new Keyframe(t, BallAt(t))));
        try
        {
            return new(72, skeleton.Teams, players, events, ball, possession);
        }
        catch (ArgumentException e)
        {
            throw new ScenarioValidationException($"Invalid generated match: {e.Message}", e);
        }
    }
}
