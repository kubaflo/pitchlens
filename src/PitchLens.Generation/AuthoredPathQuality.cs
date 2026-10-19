using PitchLens.Core;

namespace PitchLens.Generation;

public static class AuthoredPathQuality
{
    private static void Require(bool valid, string message) { if (!valid) throw new ScenarioValidationException(message); }
    public static void Validate(Match match)
    {
        Require(match.Players.Count == 22 && match.Teams.All(t => match.Players.Count(p => p.TeamId == t.Id) == 11), "Expected 11 players per team.");
        SustainedQuality.Validate(match);
        Require(match.Events.Count(e => e.Kind == EventKind.Carry) >= 8, "11v11 action needs varied controlled carries, not only passing.");
        foreach (var player in match.Players)
        {
            var frames = player.Path.Frames;
            Require(frames.Zip(frames.Skip(1), (a, b) => b.Time - a.Time).All(gap => gap <= 12), "Authored knots must cover active detail, max12s gap.");
            for (var i = 2; i < frames.Count; i++)
            {
                var a = frames[i - 2]; var b = frames[i - 1]; var c = frames[i];
                var v1 = new Position((b.Position.X - a.Position.X) / (b.Time - a.Time), (b.Position.Y - a.Position.Y) / (b.Time - a.Time));
                var v2 = new Position((c.Position.X - b.Position.X) / (c.Time - b.Time), (c.Position.Y - b.Position.Y) / (c.Time - b.Time));
                Require(v1.DistanceTo(v2) / ((c.Time - a.Time) / 2) <= 2.5, $"Unphysical velocity change for {player.Id} at {b.Time}s.");
            }
        }
        foreach (var team in match.Teams)
        {
            var outfield = match.Players.Where(p => p.TeamId == team.Id && !RegulationRoster.IsKeeper(p.Id)).ToArray();
            double Line(string role) => outfield.Where(p => RegulationRoster.Players.Single(r => r.Id == p.Id).Role == role).Average(p => p.Path.At(0).X);
            Require(team.Id == "arsenal" ? Line("defender") < Line("midfielder") && Line("midfielder") < Line("forward") :
                Line("defender") > Line("midfielder") && Line("midfielder") > Line("forward"), "Initial illustrative lines must respect attacking direction.");
            Require(outfield.Count(p => StoryQuality.DirectionChanges(p.Path) >= 2) >= 6, "Too many straight-line paths; author recovery/support turns.");
            int BurstPlayers(double speed) => outfield.Count(p => p.Path.Frames.Zip(p.Path.Frames.Skip(1),
                (a, b) => (Duration: b.Time - a.Time, Speed: a.Position.DistanceTo(b.Position) / (b.Time - a.Time)))
                .Any(segment => segment.Duration >= 2 && segment.Speed >= speed));
            Require(BurstPlayers(3) >= 4 && BurstPlayers(4) >= 2, "All-walking paths lack realistic individual running bursts.");
            Require(match.Events.Count(e => e.Kind == EventKind.Carry && match.Player(e.ActorId).TeamId == team.Id) >= 3,
                "Both clubs need controlled carries.");
            Require(match.Events.Where(e => match.Player(e.ActorId).TeamId == team.Id && !RegulationRoster.IsKeeper(e.ActorId))
                .Select(e => e.ActorId).Distinct().Count() >= 7, "New outfield players must participate, not decorate the old6v6 match.");
        }
        int synchronized = 0;
        for (double t = 0; t <= 300; t++)
        {
            var points = match.Players.Select(p => p.Path.At(t)).ToArray();
            var collisions = points.SelectMany((p, i) => points.Skip(i + 1).Select(p.DistanceTo)).Count(d => d < .4);
            Require(collisions <= 1, $"Mass player overlap at {t}s.");
            Require(points.All(p => points.Count(q => p.DistanceTo(q) < 5) <= 5), $"Unrealistic player pile-up at {t}s.");
            foreach (var team in match.Teams)
            {
                var outfield = match.Players.Where(p => p.TeamId == team.Id && !RegulationRoster.IsKeeper(p.Id)).ToArray();
                var shape = outfield.Select(p => p.Path.At(t)).ToArray();
                Require(shape.Max(p => p.Y) - shape.Min(p => p.Y) >= 30 && shape.Max(p => p.X) - shape.Min(p => p.X) >= 22,
                    $"11v11 width/depth collapses at {t}s.");
                if (t > 0 && t % 5 == 0)
                {
                    var vectors = outfield.Select(p => new Position(p.Path.At(t).X - p.Path.At(t - 5).X, p.Path.At(t).Y - p.Path.At(t - 5).Y)).ToArray();
                    if (vectors.Any(v => v.DistanceTo(default) > 2 && vectors.Count(w => v.DistanceTo(w) < .3) >= 8)) synchronized++;
                }
            }
        }
        Require(synchronized < 8, "Repeated synchronized path translation is not authored individual football movement.");
    }
}
