namespace PitchLens.Core;

public sealed record NarrativeChapter(string Id, double RevealTime, string Kind, string FocusEventId,
    string[] EventIds, string[] PlayerIds, string Heading, string Fan, string Analyst);

public sealed class NarrativeTrack
{
    public string Model { get; }
    public string FactsHash { get; }
    public IReadOnlyList<NarrativeChapter> Chapters { get; }
    public NarrativeTrack(Match match, string model, string factsHash, IEnumerable<NarrativeChapter> chapters)
    {
        static void Require(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
        var items = chapters.ToArray();
        Require(items.Length is >= 8 and <= 12 && items.All(c => c is not null), "Expected eight to twelve AI story chapters.");
        Require(items.Select(c => c.Id).Distinct().Count() == items.Length, "Chapter IDs must be unique.");
        Require(!string.IsNullOrWhiteSpace(model) && factsHash.Length == 64, "Narration provenance is required.");
        double previous = -1;
        foreach (var c in items)
        {
            Require(c.Id is { Length: >= 1 and <= 40 } && c.Id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'), "Invalid chapter ID.");
            var focus = match.Events.SingleOrDefault(e => e.Id == c.FocusEventId);
            Require(focus is not null && double.IsFinite(c.RevealTime) && c.RevealTime == focus.Time &&
                c.RevealTime > previous && c.RevealTime <= match.Duration, "Chapter reveal must be its observed focus completion, ordered without future leaks.");
            Require(c.EventIds is { Length: >= 1 and <= 6 } && c.EventIds.Distinct().Count() == c.EventIds.Length &&
                c.EventIds.Contains(c.FocusEventId), "Chapter needs distinct evidence including its focus.");
            var events = c.EventIds.Select(id => match.Events.SingleOrDefault(e => e.Id == id)).ToArray();
            Require(events.All(e => e is not null && e.Time <= c.RevealTime && e.Time >= c.RevealTime - 20),
                "Chapter evidence must be known, completed and within twenty seconds of reveal.");
            Require(c.PlayerIds is { Length: >= 1 and <= 6 } && c.PlayerIds.Distinct().Count() == c.PlayerIds.Length &&
                c.PlayerIds.All(id => events.Any(e => e!.ActorId == id || e.ReceiverId == id)) &&
                c.PlayerIds.Contains(focus!.ActorId), "Chapter player identity must resolve to its observed participants.");
            Require(c.Kind switch
            {
                "goal" => focus!.Kind == EventKind.Goal,
                "turnover" => focus!.Kind == EventKind.Pass && focus.Completed == false,
                "buildup" => events.Count(e => e!.Kind is EventKind.Pass or EventKind.Carry) >= 2,
                "response" => focus!.Kind != EventKind.Goal && match.Events.Any(e => e.Kind == EventKind.Kickoff && e.Time < focus.Time && e.Time >= focus.Time - 20),
                _ => false
            }, "Story kind must follow actual goal, turnover, buildup or restart evidence.");
            NarrativeText.Validate(c.Heading, 4, 48);
            NarrativeText.Validate(c.Fan, 8, 160);
            NarrativeText.Validate(c.Analyst, 8, 220);
            Require(c.Fan != c.Analyst, "Fan and Analyst must be distinct interpretations.");
            foreach (var p in match.Players)
                Require(!(c.Heading + " " + c.Fan + " " + c.Analyst).Contains(p.Name, StringComparison.OrdinalIgnoreCase) ||
                    c.PlayerIds.Contains(p.Id), "Named story player must be in its evidence.");
            var prose = c.Heading + " " + c.Fan + " " + c.Analyst;
            var words = NarrativeText.Words(prose);
            Require(events.Any(e => e!.Kind == EventKind.Goal) || !NarrativeText.HasScoringClaim(prose),
                $"Chapter '{c.Id}' at {c.RevealTime:0.###}s: scoring language needs observed goal evidence.");
            Require(NarrativeText.SpatialGoalReferencesSupported(prose, match, events.OfType<MatchEvent>()),
                $"Chapter '{c.Id}' at {c.RevealTime:0.###}s: spatial goal wording lacks observed geometry.");
            if (words.Any(w => w is "equaliser" or "equalizer"))
            {
                var score = Replay.At(match, c.RevealTime).Score.Values.ToArray();
                Require(focus!.Kind == EventKind.Goal && score[0] == score[1], "Equalizer language needs a locally verified tying goal.");
            }
            previous = c.RevealTime;
        }
        Require(match.Events.Where(e => e.Kind == EventKind.Goal).All(g => items.Any(c => c.Kind == "goal" && c.FocusEventId == g.Id)),
            "Every goal needs a story chapter.");
        Require(items.Count(c => c.Kind == "turnover") >= 2 && items.Any(c => c.Kind == "buildup"), "Story needs observed turnovers and buildup, not only finishes.");
        Model = model; FactsHash = factsHash;
        Chapters = Array.AsReadOnly(items.Select(c => c with { EventIds = c.EventIds.ToArray(), PlayerIds = c.PlayerIds.ToArray() }).ToArray());
    }
}

public static class NarrativeText
{
    public static bool SpatialGoalReferencesSupported(string text, Match match, IEnumerable<MatchEvent> evidence)
        => SpatialGoalReferencesSupported(text, evidence.Select(e => new GoalGeometry(
            [match.Ball.At(e.StartTime ?? e.Time), match.Ball.At(e.Time)], match.AttacksRight(match.Player(e.ActorId).TeamId))));

    public sealed record GoalGeometry(Position[] Points, bool AttacksRight);
    public static bool SpatialGoalReferencesSupported(string text, IEnumerable<GoalGeometry> evidence)
    {
        var events = evidence.ToArray();
        var words = Words(text);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] != "goal") continue;
            var suffix = i + 1 < words.Length ? words[i + 1] : "";
            bool NearGoal(Position p, GoalGeometry e, double depth, double halfWidth) =>
                Math.Abs(p.X - (e.AttacksRight ? 105 : 0)) <= depth &&
                Math.Abs(p.Y - 34) <= halfWidth;
            if (suffix is "area" or "mouth" or "line")
            {
                var supported = events.Any(e => e.Points.Any(p =>
                    suffix == "area" ? NearGoal(p,e,5.5,9.16) :
                    suffix == "mouth" ? NearGoal(p,e,1,3.66) : p.X <= 1 || p.X >= 104));
                if (!supported) return false;
            }
            var directed = i > 0 && words[i-1] is "toward" or "towards" ||
                i > 1 && words[i-1] == "the" && words[i-2] is "toward" or "towards";
            if ((directed || IsCloserToGoal(words,i)) && !events.Any(e => e.Points.Length > 1 &&
                e.Points[^1].DistanceTo(new(e.AttacksRight ? 105 : 0,34)) <
                e.Points[0].DistanceTo(new(e.AttacksRight ? 105 : 0,34)) - .1))
                return false;
            var front = i > 2 && words[i-1] == "of" && words[i-2] == "front" && words[i-3] == "in";
            if (front && !events.Any(e => e.Points.Length > 0 && NearGoal(e.Points[^1],e,16.5,9.16))) return false;
        }
        return true;
    }
    public static bool HasScoringClaim(string text)
    {
        var words = Words(text);
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i] is "scores" or "scored" or "scoring" or "goals" or "opener" or "equaliser" or "equalizer")
                return true;
            if (words[i] != "goal") continue;
            var spatialSuffix = i + 1 < words.Length && words[i + 1] is "area" or "mouth" or "line";
            var direction = i > 0 && words[i - 1] is "toward" or "towards";
            var directedArticle = i > 1 && words[i - 1] == "the" && words[i - 2] is "toward" or "towards";
            var inFront = i > 2 && words[i - 1] == "of" && words[i - 2] == "front" && words[i - 3] == "in";
            if (!spatialSuffix && !direction && !directedArticle && !inFront && !IsCloserToGoal(words,i)) return true;
        }
        return false;
    }
    private static bool IsCloserToGoal(string[] words,int goalIndex)
    {
        var toIndex=goalIndex-1;
        if(toIndex>=0&&words[toIndex]=="the")toIndex--;
        return toIndex>=1&&words[toIndex]=="to"&&words[toIndex-1]=="closer"&&
            (toIndex<2||words[toIndex-2] is not("not" or "no" or "never"));
    }
    public static string[] Words(string text) => text.ToLowerInvariant().Split([' ', '.', ',', '!', '?', ';', ':', '—', '-'], StringSplitOptions.RemoveEmptyEntries);
    public static void Validate(string text, int minimum, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < minimum || text.Length > maximum ||
            text.Any(c => char.IsDigit(c) || char.IsControl(c) || c is '<' or '>' or '`') ||
            Words(text).Any(w => w is "will" or "later" or "eventually" or "winner" or "prediction" or "finally" or "xg" or "pressure" or "formation" or "guaranteed" or "certainly"))
            throw new ArgumentException("Narration must be bounded plain text without numerical, unsupported-metric or future-outcome claims.");
    }
}

public static class NarrativeReplay
{
    public static IReadOnlyList<NarrativeChapter> At(Match match, double time)
    {
        time = match.ClampTime(time);
        return match.Narrative?.Chapters.Where(c => c.RevealTime <= time).ToArray() ?? [];
    }

    public static string Consequence(Match match, NarrativeChapter chapter)
    {
        var focus = match.Events.Single(e => e.Id == chapter.FocusEventId);
        if (focus.Kind != EventKind.Goal) return chapter.Kind == "turnover" ? "Possession changed" : chapter.Kind == "response" ? "After the restart" : "Move developing";
        var scores = Replay.At(match, chapter.RevealTime).Score;
        var scoringTeam = match.Player(focus.ActorId).TeamId;
        var home = scores[scoringTeam]; var other = scores.Single(p => p.Key != scoringTeam).Value;
        return home == other ? "Level again" : home == other + 1 ? "Takes the lead" : "Goal";
    }
}
