using System.Diagnostics;
using PitchLens.Core;
using PitchLens.Generation;

var match = AuthoredMatchRecording.Create();
var legacy = Measure(() => Legacy(match));
Console.WriteLine($"Legacy {match.Duration}s / 30Hz pure computation: {legacy.Result} updates, {legacy.Milliseconds:F2}ms, {legacy.Bytes / 1024d / 1024:F2}MiB allocated.");
var optimized = Measure(() => Optimized(match));
var result = optimized.Result;
Console.WriteLine($"Hybrid bounded computation: {result.SnapshotComputations} snapshots, {result.ExplanationComputations} explanation builds, {result.StoryComputations} story builds, {optimized.Milliseconds:F2}ms, {optimized.Bytes / 1024d / 1024:F2}MiB allocated.");
Console.WriteLine("CPU and allocation figures are medians of five warmed runs.");
Console.WriteLine("Headless computation only; excludes native controls, drawing, WebView and GPU time.");

static (T Result, double Milliseconds, long Bytes) Measure<T>(Func<T> operation)
{
    var result = operation();
    var samples = new List<(double Milliseconds, long Bytes)>();
    for (var i = 0; i < 5; i++)
    {
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        result = operation();
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        samples.Add((elapsed, GC.GetAllocatedBytesForCurrentThread() - before));
    }
    return (result, samples.OrderBy(s => s.Milliseconds).ElementAt(2).Milliseconds,
        samples.OrderBy(s => s.Bytes).ElementAt(2).Bytes);
}

static ReplayPresentation Optimized(Match match)
{
    var clock = new FakeClock();
    var surface = new ReplayPresentation(match, clock);
    surface.SetRate(1);
    surface.Play();
    Func<PlayerStats, double>[] metrics = [p => p.Goals, p => p.Shots, p => p.Passes, p => p.Distance];
    for (var i = 0; i <= match.Duration * 30; i++)
    {
        clock.Seconds = i / 30d;
        var frame = surface.Sample();
        if (frame is null) continue;
        foreach (var metric in metrics)
        {
            GC.KeepAlive(frame.Snapshot.Players.OrderByDescending(metric).ThenBy(p => p.PlayerId, StringComparer.Ordinal).Take(5).ToArray());
            GC.KeepAlive(match.Teams.Select(t => frame.Snapshot.Players.Where(p => match.Player(p.PlayerId).TeamId == t.Id).Sum(metric)).ToArray());
        }
    }
    return surface;
}
static int Legacy(Match match)
{
    Func<PlayerStats, double>[] metrics = [p => p.Goals, p => p.Shots, p => p.Passes, p => p.Distance];
    for (var i = 0; i <= match.Duration * 30; i++)
    {
        var time = i / 30d;
        var snapshot = Replay.At(match, time);
        GC.KeepAlive(StoryReplay.At(match, time));
        GC.KeepAlive(Explanations.At(match, time, ExplanationMode.Casual));
        foreach (var metric in metrics)
        {
            GC.KeepAlive(snapshot.Players.OrderByDescending(metric).ThenBy(p => p.PlayerId, StringComparer.Ordinal).Take(5).ToArray());
            GC.KeepAlive(match.Teams.Select(t => snapshot.Players.Where(p => match.Player(p.PlayerId).TeamId == t.Id).Sum(metric)).ToArray());
        }
    }
    return (int)(match.Duration * 30) + 1;
}

sealed class FakeClock : IMonotonicClock { public double Seconds { get; set; } }
