using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class InsightVisualTests
{
    [Fact]
    public void Exact_recorded_histogram_is_local_eight_attempts_with_known_bins_and_original_ids()
    {
        var recording = MomentRecording.Load(AuthoredMatchRecording.Create(), 1, 86);
        var facts = recording.Evidence.Facts;
        var chart = new MomentVisuals(facts);
        Assert.Equal(8, chart.MeasuredPasses);
        Assert.Equal(0, chart.UnknownDistances);
        Assert.Equal(new[] { 2, 4, 1, 1 }, chart.Bins.Select(b => b.EventIds.Length));
        Assert.Equal(17.042944713347826, chart.MeanPassMetres!.Value, 10);
        Assert.Equal(new[] { "e17", "e21" }, chart.Bins[0].EventIds);
        Assert.Equal(new[] { "e18", "e19", "e24", "e26" }, chart.Bins[1].EventIds);
        Assert.Equal("e28", chart.Selected.Id);
        chart.SelectBin(chart.Bins[0]);
        Assert.Equal("e17", chart.Selected.Id);
        Assert.Equal(2, chart.HighlightedIds.Count);
        Assert.Contains(chart.DrawEvents, e => e.Id == "e17");
        chart.Select("e26");
        Assert.Equal(84, chart.Selected.Time);
        Assert.Equal(new[] { "e26" }, chart.HighlightedIds);
        Assert.Null(chart.SelectedBin);
        Assert.Equal(17.5, MomentVisuals.Distance(chart.Selected));
        Assert.Throws<ArgumentException>(() => chart.Select("future"));
        Assert.Throws<ArgumentException>(() => new MomentVisuals(facts with { Cutoff = 85 }));
    }

    [Fact]
    public void Bin_boundaries_zero_curved_paths_unknown_and_empty_samples_are_not_conflated()
    {
        static MomentEvent Pass(string id, double? length) => new(id, 2, 1, "Pass", "A", "B", true,
            length is null ? [] : [new(1, 0, 0), new(2, length.Value, 0)]);
        var chart = new MomentVisuals(new(1, 2, "p0",
            [Pass("p0", 0), Pass("p10", 10), Pass("p20", 20), Pass("p30", 30), Pass("unknown", null)], []));
        Assert.Equal(new[] { 1, 1, 1, 1 }, chart.Bins.Select(b => b.EventIds.Length));
        Assert.Equal(1, chart.UnknownDistances); Assert.Equal(4, chart.MeasuredPasses);
        Assert.Equal(15, chart.MeanPassMetres);
        var curve = new MomentEvent("curve", 3, 1, "Pass", "A", "B", false,
            [new(1, 0, 0), new(2, 3, 4), new(3, 6, 0)]);
        Assert.Equal(10, MomentVisuals.Distance(curve));
        Assert.Null(MomentVisuals.Distance(curve with { Ball = [new(1, 0, 0), new(3, double.NaN, 0)] }));
        var empty = new MomentVisuals(new(1, 0, "", [], []));
        Assert.Equal(0, empty.MeasuredPasses); Assert.Null(empty.MeanPassMetres);
        Assert.Empty(empty.Sequence);
        Assert.All(empty.Bins, b => Assert.Empty(b.EventIds));
    }
}
