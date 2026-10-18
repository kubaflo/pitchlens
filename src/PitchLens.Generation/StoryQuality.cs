using PitchLens.Core;

namespace PitchLens.Generation;

public static class StoryQuality
{
    public static readonly IReadOnlyList<string> Titles = Array.AsReadOnly(new[]
    {
        "Pressure and Space", "The Turning Tide", "A Game of Transitions", "Seventy-Two Seconds"
    });
    public static bool IsKeeper(Player p) => p.Id is "arsenal-raya" or "manchester-city-donnarumma";
    public static bool IsActiveOutfielder(Player p) => !IsKeeper(p) && p.Path.Frames.Count >= 6 &&
        p.Path.Measure(72).Distance >= 35 && p.Path.Measure(72).MaxSpeed >= 3 && DirectionChanges(p.Path) >= 2;

    public static int DirectionChanges(Trajectory path)
    {
        var vectors = path.Frames.Zip(path.Frames.Skip(1), (a, b) =>
            new Position(b.Position.X - a.Position.X, b.Position.Y - a.Position.Y))
            .Where(v => v.DistanceTo(default) >= 4).ToArray();
        return vectors.Zip(vectors.Skip(1), (a, b) =>
            (a.X * b.X + a.Y * b.Y) / (a.DistanceTo(default) * b.DistanceTo(default))).Count(cos => cos < .85);
    }

    public static MatchStory Validate(Match match, StoryDocument document)
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new ScenarioValidationException(message);
        }
        Require(document is not null && Titles.Contains(document.Title), "Story needs a neutral, spoiler-free title from the supported list.");
        Require(document!.Beats is { Length: >= 6 and <= 9 } && document.Beats.All(b => b is not null), "Expected 6-9 linked story beats.");
        var beats = document.Beats!;
        Require(beats.Select(b => b.Id).Distinct().Count() == beats.Length &&
            beats.All(b => b.Id is { Length: >= 1 and <= 32 } && b.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')),
            "Story beat IDs must be unique short ASCII identifiers.");
        Require(beats[0].StartTime == 0 && beats[^1].EndTime == 72, "Story beats must cover the whole 72-second replay.");
        double previous = 0;
        var kinds = new[] { "build-up", "turnover", "counterattack", "support-run", "finish", "response" };
        foreach (var beat in beats)
        {
            Require(double.IsFinite(beat.StartTime) && double.IsFinite(beat.EndTime) &&
                beat.StartTime == previous && beat.EndTime > beat.StartTime && beat.EndTime <= 72,
                "Story beats must be contiguous, ordered, finite time ranges.");
            previous = beat.EndTime;
            Require(kinds.Contains(beat.Kind), "Unknown story beat kind.");
            Require(beat.EventIds is { Length: >= 1 and <= 10 } && beat.EventIds.Distinct().Count() == beat.EventIds.Length &&
                beat.EventIds.All(id => match.Events.Any(e => e.Id == id && e.Time >= beat.StartTime && e.Time <= beat.EndTime)),
                "Every beat needs known event IDs resolved inside its time range.");
            Require(beat.PlayerIds is { Length: >= 2 and <= 6 } && beat.PlayerIds.Distinct().Count() == beat.PlayerIds.Length &&
                beat.PlayerIds.All(id => match.Players.Any(p => p.Id == id)), "Beat player IDs must reference 2-6 verified players.");
            var events = beat.EventIds!.Select(id => match.Events.Single(e => e.Id == id)).ToArray();
            Require(events.All(e => beat.PlayerIds!.Contains(e.ActorId) &&
                (e.ReceiverId is null || beat.PlayerIds.Contains(e.ReceiverId))), "Beat players must include its event actors and receivers.");
            var team = match.Player(events[0].ActorId).TeamId;
            if (beat.Kind == "turnover")
                Require(events.Any(e => e.Kind == EventKind.Pass && e.Completed == false), "Turnover beat needs an intercepted pass.");
            if (beat.Kind == "finish")
                Require(events.Any(e => e.Kind == EventKind.Goal) && events.Any(e => e.Kind == EventKind.Shot),
                    "Finish beat needs the paired shot and goal.");
            if (beat.Kind == "response")
                Require(events.Any(e => e.Kind == EventKind.Kickoff) && events.Any(e => e.Kind == EventKind.Pass && e.Completed == true),
                    "Response beat needs an opposing kickoff and a completed pass.");
            if (beat.Kind is "build-up" or "counterattack")
            {
                Require(events.Any(e => e.Kind == EventKind.Pass && e.Completed == true), "Attacking beat needs a completed pass.");
                var reacts = beat.PlayerIds!.Select(match.Player).Any(p => p.TeamId != team && !IsKeeper(p) &&
                    p.Path.Measure(beat.EndTime).Distance - p.Path.Measure(beat.StartTime).Distance >= 8 &&
                    p.Path.At(beat.StartTime).DistanceTo(match.Ball.At(beat.StartTime)) -
                        p.Path.At(beat.EndTime).DistanceTo(match.Ball.At(beat.EndTime)) >= 5 &&
                    p.Path.At(beat.EndTime).DistanceTo(match.Ball.At(beat.EndTime)) <= 15);
                Require(reacts, "Build-up/counterattack must show a named opposing defender closing at least 5 m to within 15 m of the ball.");
            }
            if (beat.Kind == "counterattack")
                Require(events.Any(e => e.Kind == EventKind.Carry), "Counterattack needs a moving carry.");
            if (beat.Kind == "support-run")
            {
                var supported = events.Any(e => e.Kind == EventKind.Pass && e.Completed == true &&
                    !IsKeeper(match.Player(e.ReceiverId!)) &&
                    match.Player(e.ReceiverId!).Path.Measure(e.Time).Distance -
                        match.Player(e.ReceiverId!).Path.Measure(beat.StartTime).Distance >= 10 &&
                    (match.Player(e.ReceiverId!).Path.At(e.Time).X - match.Player(e.ReceiverId!).Path.At(beat.StartTime).X) *
                        (match.Player(e.ActorId).TeamId == "arsenal" ? 1 : -1) >= 8 &&
                    match.OwnerAt(beat.StartTime) != e.ReceiverId &&
                    match.Possession.Where(p => p.Time >= beat.StartTime && p.Time < e.Time).All(p => p.PlayerId != e.ReceiverId));
                Require(supported, "Support beat needs a completed pass to an advancing off-ball runner (10 m travelled, 8 m forward).");
            }
        }
        Require(kinds.All(kind => beats.Any(b => b.Kind == kind)), "Story must include build-up, turnover, counterattack, support-run, finish and response.");
        Require(beats[0].Kind == "build-up" &&
            Array.FindIndex(beats, b => b.Kind == "turnover") < Array.FindIndex(beats, b => b.Kind == "counterattack") &&
            Array.FindIndex(beats, b => b.Kind == "counterattack") < Array.FindIndex(beats, b => b.Kind == "finish") &&
            Array.FindIndex(beats, b => b.Kind == "finish") < Array.FindIndex(beats, b => b.Kind == "response"),
            "Story arc must progress from build-up through turnover/counterattack to finish and response.");
        Require(match.Events.All(e => beats.Any(b => b.EventIds.Contains(e.Id))), "All match events must be anchored in the story.");
        foreach (var keeper in match.Players.Where(IsKeeper))
        {
            var left = keeper.TeamId == "arsenal";
            Require(keeper.Path.At(0).X is >= 0 and <= 105 &&
                (left ? keeper.Path.At(0).X <= 10 : keeper.Path.At(0).X >= 95) &&
                keeper.Path.At(0).Y is >= 24 and <= 44, "Keepers must start at their own goal: Arsenal left, City right.");
            Require(keeper.Path.Frames.All(f => (left ? f.Position.X <= 18 : f.Position.X >= 87) &&
                f.Position.Y is >= 18 and <= 50), "Goalkeepers must remain near their own goal.");
        }
        foreach (var team in match.Teams)
        {
            var active = match.Players.Count(p => p.TeamId == team.Id && IsActiveOutfielder(p));
            Require(active >= 3, "Each club needs at least three active outfield paths: 35 m, a 3 m/s burst and two purposeful direction changes.");
            Require(match.Events.Any(e => match.Player(e.ActorId).TeamId == team.Id && e.Kind == EventKind.Pass && e.Completed == true) &&
                match.Events.Any(e => match.Player(e.ActorId).TeamId == team.Id && e.Kind == EventKind.Carry),
                "Both clubs must complete passes and carry the ball.");
            foreach (var time in beats.Select(b => b.EndTime).Prepend(0))
            {
                var points = match.Players.Where(p => p.TeamId == team.Id && !IsKeeper(p)).Select(p => p.Path.At(time)).ToArray();
                Require(points.SelectMany(a => points.Select(a.DistanceTo)).Max() >= 20,
                    "Outfield players must retain at least 20 m of team spread at story boundaries.");
            }
        }
        return new(document.Title, Array.AsReadOnly(beats.Select(b => new StoryBeat(b.Id, b.StartTime, b.EndTime,
            b.Kind, Array.AsReadOnly(b.EventIds.ToArray()), Array.AsReadOnly(b.PlayerIds.ToArray()))).ToArray()));
    }
}
