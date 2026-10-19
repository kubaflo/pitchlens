namespace PitchLens.Core;

public sealed record CommentaryCue(string Id, string EventId, double Start, double End, string[] PlayerIds, string Text);

public sealed class CommentaryTrack
{
    public string Model { get; }
    public IReadOnlyList<CommentaryCue> Cues { get; }

    public CommentaryTrack(Match match, string model, IEnumerable<CommentaryCue> cues)
    {
        var items = cues.ToArray();
        static void Require(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
        Require(!string.IsNullOrWhiteSpace(model), "Commentary model provenance is required.");
        Require(items.Length is >= 24 and <= 64 && items.All(c => c is not null), "Expected 24-64 AI commentary cues.");
        Require(items.Select(c => c.Id).Distinct().Count() == items.Length, "Commentary cue IDs must be unique.");
        double previousEnd = 0;
        foreach (var cue in items)
        {
            Require(cue.Id is { Length: >= 1 and <= 40 } && cue.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'), "Invalid cue ID.");
            var evidence = match.Events.SingleOrDefault(e => e.Id == cue.EventId);
            Require(evidence is not null, "Commentary references an unknown event.");
            Require(double.IsFinite(cue.Start) && double.IsFinite(cue.End) && cue.Start >= evidence!.Time &&
                cue.Start <= evidence.Time + 2 && cue.Start >= previousEnd && cue.End <= match.Duration &&
                cue.End - cue.Start is >= 2 and <= 6, "Commentary must be ordered, nonoverlapping, 2-6 seconds and follow its evidence.");
            Require(cue.PlayerIds is { Length: >= 1 and <= 2 } &&
                cue.PlayerIds.Distinct().Count() == cue.PlayerIds.Length &&
                cue.PlayerIds.All(id => id is not null && (id == evidence!.ActorId || id == evidence.ReceiverId)),
                "Commentary players must participate in the anchored event.");
            Require(cue.Text is { Length: >= 8 and <= 140 } && !cue.Text.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '<' or '>'),
                "Commentary must be short plain text, without markup or numerical statistics.");
            var words = cue.Text!.ToLowerInvariant().Split([' ', '.', ',', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
            Require(!words.Any(w => w is "will" or "later" or "eventually" or "winner" or "prediction" or "finally"),
                "Commentary cannot predict future outcomes.");
            Require(evidence!.Kind == EventKind.Goal || !words.Any(w => w is "goal" or "scores" or "scored"),
                "Goal commentary requires goal evidence.");
            foreach (var player in match.Players)
                Require(!cue.Text.Contains(player.Name, StringComparison.OrdinalIgnoreCase) || cue.PlayerIds.Contains(player.Id),
                    "Named commentary player is not in its evidence.");
            previousEnd = cue.End;
        }
        for (double minute = 0; minute < match.Duration; minute += 60)
            Require(items.Count(c => c.Start >= minute && c.Start < minute + 60) >= 2, "Commentary needs coverage in every minute.");
        Require(match.Events.Where(e => e.Kind == EventKind.Goal).All(g => items.Any(c => c.EventId == g.Id)),
            "Each goal needs a commentary cue.");
        Model = model;
        Cues = Array.AsReadOnly(items.Select(c => c with { PlayerIds = c.PlayerIds.ToArray() }).ToArray());
    }
}

public static class CommentaryReplay
{
    public static CommentaryCue? At(CommentaryTrack? track, double time, bool enabled = true)
    {
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        if (!enabled || track is null) return null;
        var cues = track.Cues;
        int low = 0, high = cues.Count;
        while (low < high) { var mid = (low + high) / 2; if (cues[mid].Start <= time) low = mid + 1; else high = mid; }
        return low > 0 && time < cues[low - 1].End ? cues[low - 1] : null;
    }
}
