namespace PitchLens.Generation;

public sealed record PassDistance(string EventId, double? Metres);
public sealed record DistanceBin(string Label, double Minimum, double? Maximum, string[] EventIds);

public sealed class MomentVisuals
{
    private readonly MomentFacts facts;
    public string SelectedId { get; private set; }
    public HashSet<string> HighlightedIds { get; private set; } = [];
    public DistanceBin? SelectedBin { get; private set; }
    public MomentEvent Selected => facts.Events.Single(e => e.Id == SelectedId);
    public IReadOnlyList<PassDistance> Passes { get; }
    public IReadOnlyList<DistanceBin> Bins { get; }
    public IReadOnlyList<MomentEvent> Sequence { get; }
    public int UnknownDistances => Passes.Count(p => p.Metres is null);
    public int MeasuredPasses => Passes.Count - UnknownDistances;
    public double? MeanPassMetres => MeasuredPasses == 0 ? null : Passes.Where(p => p.Metres.HasValue).Average(p => p.Metres!.Value);
    public double WindowStart { get; }
    public double Cutoff { get; }
    public IEnumerable<MomentEvent> DrawEvents => facts.Events.Where(e => HasGeometry(e) &&
        (Sequence.Any(s => s.Id == e.Id) || HighlightedIds.Contains(e.Id)));

    public MomentVisuals(MomentFacts facts)
    {
        this.facts = facts;
        SelectedId = facts.FocusId;
        if (!double.IsFinite(facts.Cutoff) || facts.Events.Any(e => !double.IsFinite(e.Time) || !double.IsFinite(e.Start) ||
            e.Start < 0 || e.Start > e.Time || e.Time > facts.Cutoff))
            throw new ArgumentException("Chart events must be observed through the captured cutoff.");
        Cutoff = facts.Cutoff;
        WindowStart = facts.Events.Length == 0 ? facts.Cutoff : facts.Events.Min(e => e.Start);
        Passes = facts.Events.Where(e => e.Kind == "Pass").Select(e => new PassDistance(e.Id, Distance(e))).ToArray();
        double[] boundaries = [0, 10, 20, 30];
        Bins = boundaries.Select((lower, index) =>
        {
            double? upper = index == boundaries.Length - 1 ? null : boundaries[index + 1];
            return new DistanceBin(upper is null ? "30+ m" : $"{lower:0}–<{upper:0} m", lower, upper,
                Passes.Where(p => p.Metres is double m && m >= lower && (upper is null || m < upper)).Select(p => p.EventId).ToArray());
        }).ToArray();
        Sequence = facts.Events.Where(e => e.Kind != "Kickoff" && HasGeometry(e)).TakeLast(6).ToArray();
        if (facts.Events.Any(e => e.Id == SelectedId)) HighlightedIds = [SelectedId];
    }
    public void Select(string id)
    {
        if (!facts.Events.Any(e => e.Id == id)) throw new ArgumentException("Unknown observed evidence ID.", nameof(id));
        SelectedId = id; HighlightedIds = [id]; SelectedBin = null;
    }
    public void SelectBin(DistanceBin bin)
    {
        if (!Bins.Contains(bin)) throw new ArgumentException("Unknown histogram bin.", nameof(bin));
        SelectedBin = bin; HighlightedIds = bin.EventIds.ToHashSet();
        if (bin.EventIds.Length > 0) SelectedId = bin.EventIds[0];
    }
    private static bool HasGeometry(MomentEvent e) => e.Ball is { Length: > 0 } && e.Ball.Select((p, i) =>
        double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Time) &&
        p.X >= 0 && p.X <= 105 && p.Y >= 0 && p.Y <= 68 && p.Time >= e.Start && p.Time <= e.Time &&
        (i == 0 || p.Time > e.Ball[i - 1].Time)).All(valid => valid);

    public static double? Distance(MomentEvent e)
    {
        var points = e.Ball;
        if (points is null || points.Length < 2 || points[0].Time != e.Start || points[^1].Time != e.Time) return null;
        double total = 0;
        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i];
            if (!double.IsFinite(p.Time) || !double.IsFinite(p.X) || !double.IsFinite(p.Y) ||
                p.X < 0 || p.X > 105 || p.Y < 0 || p.Y > 68 || p.Time < e.Start || p.Time > e.Time ||
                (i > 0 && p.Time <= points[i - 1].Time)) return null;
            if (i > 0) total += Math.Sqrt(Math.Pow(p.X - points[i - 1].X, 2) + Math.Pow(p.Y - points[i - 1].Y, 2));
        }
        return total;
    }
}
