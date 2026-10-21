namespace PitchLens.Core;

public sealed record CommentaryCue(string Id, string EventId, double Start, double End, string[] PlayerIds, string Text, string[]? EvidenceIds = null);

public sealed class CommentaryTrack
{
    public string Model { get; }
    public IReadOnlyList<CommentaryCue> Cues { get; }
    public string? FactsHash { get; }
    public int Contract { get; }

    public CommentaryTrack(Match match, string model, IEnumerable<CommentaryCue> cues, string? factsHash = null,int contract=1)
    {
        var items = cues.ToArray();
        static void Require(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
        Require(!string.IsNullOrWhiteSpace(model), "Commentary model provenance is required.");
        Require(contract is 1 or 2,"Unknown commentary contract.");
        Require(items.Length >= (contract==1?24:1) && items.Length <= (contract==1?64:90) && items.All(c => c is not null), "Invalid AI commentary count for its declared contract.");
        if(contract==2)CaptionSchedule.Validate(match,items);
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
            var context = cue.EvidenceIds ?? [cue.EventId];
            Require(context.Length is >= 1 and <= 6 && context.Distinct().Count() == context.Length && context.Contains(cue.EventId),
                "Cue needs distinct evidence including its focus.");
            var contextEvents = context.Select(id => match.Events.SingleOrDefault(e => e.Id == id)).ToArray();
            Require(contextEvents.All(e => e is not null && e.Time <= cue.Start && e.Time >= cue.Start - 12),
                "Cue context must be observed through its start and within twelve seconds.");
            Require(cue.PlayerIds is { Length: >= 1 and <= 6 } && (cue.EvidenceIds is not null || cue.PlayerIds.Length <= 2) &&
                cue.PlayerIds.Distinct().Count() == cue.PlayerIds.Length &&
                cue.PlayerIds.All(id => id is not null && contextEvents.Any(e => id == e!.ActorId || id == e.ReceiverId)),
                "Commentary players must participate in the anchored event.");
            Require(cue.Text is { Length: >= 8 and <= 140 } && !cue.Text.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '<' or '>'),
                "Commentary must be short plain text, without markup or numerical statistics.");
            var words = cue.Text!.ToLowerInvariant().Split([' ', '.', ',', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
            Require(!words.Any(w => w is "will" or "later" or "eventually" or "winner" or "prediction" or "finally"),
                "Commentary cannot predict future outcomes.");
            Require(evidence!.Kind == EventKind.Goal || !NarrativeText.HasScoringClaim(cue.Text),
                $"Cue '{cue.Id}' at {cue.Start:0.###}s: scoring commentary requires goal evidence.");
            Require(NarrativeText.SpatialGoalReferencesSupported(cue.Text,match,contextEvents.OfType<MatchEvent>()),
                $"Cue '{cue.Id}' at {cue.Start:0.###}s: spatial goal wording lacks observed geometry.");
            var remainingNames = cue.Text;
            foreach (var player in match.Players.OrderByDescending(p => p.Name.Length))
                if (remainingNames.Contains(player.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Require(cue.PlayerIds.Contains(player.Id), "Named commentary player is not in its evidence.");
                    remainingNames = remainingNames.Replace(player.Name, "", StringComparison.OrdinalIgnoreCase);
                }
            previousEnd = cue.End;
            if (cue.EvidenceIds is not null)
            {
                NarrativeText.Validate(cue.Text, 8, 140);
                Require(evidence.Kind == EventKind.Goal || !words.Any(w => w is "lead" or "level" or "equalizer" or "equaliser" or "opener"),
                    "Outcome-bearing commentary requires the primary observed goal.");
                if (words.Any(w => w is "equalizer" or "equaliser"))
                    Require(Replay.At(match, evidence.Time).Score.Values.Distinct().Count() == 1, "Equalizer cue requires an actual tying goal.");
            }
        }
        for (double minute = 0; minute < match.Duration; minute += 60)
            Require(items.Count(c => c.Start >= minute && c.Start < minute + 60) >= 2, "Commentary needs coverage in every minute.");
        Require(match.Events.Where(e => e.Kind == EventKind.Goal).All(g => items.Any(c => c.EventId == g.Id)),
            "Each goal needs a commentary cue.");
        Model = model;
        FactsHash = factsHash;
        Contract=contract;
        Cues = Array.AsReadOnly(items.Select(c => c with { PlayerIds = c.PlayerIds.ToArray(), EvidenceIds = c.EvidenceIds?.ToArray() }).ToArray());
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
