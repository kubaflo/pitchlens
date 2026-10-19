using PitchLens.Core;

namespace PitchLens.Generation;

public static class SustainedQuality
{
    public static void Validate(Match match)
    {
        static void Require(bool valid, string message) { if (!valid) throw new ScenarioValidationException(message); }
        Require(match.Duration == 300, "Expected a genuine 300-second match.");
        foreach (var player in match.Players)
        {
            Require(player.Path.Measure(300).MaxSpeed <= 8 + 1e-8, "Player exceeds 8 m/s.");
            if (StoryQuality.IsKeeper(player))
                Require(player.Path.Frames.All(f => (player.TeamId == "arsenal" ? f.Position.X <= 18 : f.Position.X >= 87) &&
                    f.Position.Y is >= 18 and <= 50), "Goalkeeper at wrong end/outside own goal area.");
        }
        for (double t = 0; t <= 300; t += 5)
        {
            foreach (var team in match.Teams)
            {
                var points = match.Players.Where(p => p.TeamId == team.Id && !StoryQuality.IsKeeper(p)).Select(p => p.Path.At(t)).ToArray();
                Require(points.Max(p => p.Y) - points.Min(p => p.Y) >= 14 &&
                    points.SelectMany(a => points.Select(a.DistanceTo)).Max() >= 25,
                    $"Team shape collapses at {t}s.");
            }
        }
        for (double start = 0; start <= 280; start += 10)
        {
            var moving = match.Players.Where(p => !StoryQuality.IsKeeper(p) &&
                p.Path.Measure(start + 20).Distance - p.Path.Measure(start).Distance >= 8).ToArray();
            Require(moving.Length >= 6 && match.Teams.All(t => moving.Count(p => p.TeamId == t.Id) >= 2),
                $"Insufficient visible movement during {start}-{start + 20}s.");
            Require(match.Events.Count(e => e.Time > start && e.Time <= start + 20 && e.Kind is EventKind.Pass or EventKind.Carry or EventKind.Shot) >= 2,
                $"No meaningful event distribution during {start}-{start + 20}s.");
        }
        Require(match.Events.Where(e => e.Kind is EventKind.Pass or EventKind.Carry or EventKind.Shot).Last().Time >= 295,
            "Match has an idle padded tail.");
        Require(match.Events.Count(e => e.Kind == EventKind.Pass && e.Completed == false) >= 4, "Match needs multiple genuine turnovers.");
        foreach (var team in match.Teams)
        {
            Require(match.Events.Any(e => e.Kind == EventKind.Goal && match.Player(e.ActorId).TeamId == team.Id),
                "Both clubs need a meaningful finish.");
            for (double start = 0; start < 300; start += 60)
                Require(match.Events.Any(e => e.Kind == EventKind.Pass && e.Completed == true &&
                    match.Player(e.ActorId).TeamId == team.Id && e.Time > start && e.Time <= start + 60),
                    "Both clubs must participate in each minute.");
        }
        foreach (var e in match.Events)
        {
            if (e.Kind == EventKind.Carry)
                Require(match.Player(e.ActorId).Path.Measure(e.Time).Distance -
                    match.Player(e.ActorId).Path.Measure(e.StartTime!.Value).Distance >= 3, "Carry must visibly move the ball.");
            if (e.Kind is EventKind.Pass or EventKind.Shot)
            {
                var speed = match.Ball.At(e.StartTime!.Value).DistanceTo(match.Ball.At(e.Time)) / (e.Time - e.StartTime.Value);
                Require(speed <= 40, "Ball flight exceeds 40 m/s.");
                if (e.Kind == EventKind.Pass)
                    Require(match.Player(e.ReceiverId!).Path.At(e.Time).DistanceTo(match.Ball.At(e.Time)) < .001,
                        "Pass does not reach its receiver.");
            }
        }
    }
}
