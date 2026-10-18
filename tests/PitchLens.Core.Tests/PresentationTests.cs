using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class PresentationTests
{
    [Fact]
    public void Fast_render_sampling_is_bounded_and_histories_are_cached()
    {
        var clock = new Clock();
        using var surface = new ReplayPresentation(RecordedScenario.Create(), clock);
        surface.Play();
        for (var i = 0; i <= 4320; i++)
        {
            clock.Seconds = i / 60d;
            var frame = surface.Sample();
            if (frame is null) continue;
            Assert.Equal(Replay.At(surface.Match, frame.Snapshot.Time).Players, frame.Snapshot.Players);
        }
        Assert.Equal(289, surface.SnapshotComputations);
        Assert.InRange(surface.ExplanationComputations, 1, surface.Match.Events.Count + 1);
        Assert.Equal(surface.Match.Story!.Beats.Count + 1, surface.StoryComputations);
    }

    [Fact]
    public void Pause_seek_restart_and_replacement_keep_selection_and_drop_future_evidence()
    {
        var clock = new Clock();
        using var surface = new ReplayPresentation(RecordedScenario.Create(), clock);
        surface.Select("manchester-city-doku");
        surface.Play();
        clock.Seconds = 60;
        Assert.NotEmpty(surface.Sample(true)!.Story);
        surface.Pause();
        var paused = surface.Sample(true)!;
        clock.Seconds = 500;
        Assert.Equal(paused.Snapshot.Time, surface.Sample(true)!.Snapshot.Time);
        surface.Seek(0);
        var reset = surface.Sample(true)!;
        Assert.Empty(reset.Explanations);
        Assert.Empty(reset.Story);
        Assert.Equal("manchester-city-doku", reset.SelectedId);
        var revision = reset.Revision;
        surface.Replace(RecordedScenario.Create());
        var replacement = surface.Sample(true)!;
        Assert.Equal(revision + 1, replacement.Revision);
        Assert.Equal(0, replacement.Snapshot.Time);
        Assert.False(replacement.Playing);
        Assert.Equal(reset.SelectedId, replacement.SelectedId);
        surface.Play();
        clock.Seconds++;
        surface.Restart();
        Assert.Equal(0, surface.Sample(true)!.Snapshot.Time);
        Assert.False(surface.Playing);
    }

    [Fact]
    public void Mode_changes_recompute_history_once_and_disposal_stops_admission()
    {
        var clock = new Clock { Seconds = 0 };
        var surface = new ReplayPresentation(RecordedScenario.Create(), clock);
        surface.Seek(30);
        var first = surface.Sample(true)!;
        var again = surface.Sample(true)!;
        Assert.Same(first.Snapshot, again.Snapshot);
        Assert.Same(first.Explanations, again.Explanations);
        Assert.Same(first.Story, again.Story);
        surface.SetMode(ExplanationMode.Analyst);
        Assert.NotSame(first.Explanations, surface.Sample(true)!.Explanations);
        surface.Dispose();
        Assert.False(surface.Playing);
        Assert.Throws<ObjectDisposedException>(() => surface.Sample());
    }
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
}
