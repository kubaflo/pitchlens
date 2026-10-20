using System.Diagnostics;

namespace PitchLens.Core;

public interface IMonotonicClock
{
    double Seconds { get; }
}

public sealed class MonotonicClock : IMonotonicClock
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    public double Seconds => watch.Elapsed.TotalSeconds;
}

public sealed class Playback
{
    private readonly IMonotonicClock clock;
    private readonly double duration;
    private readonly IReadOnlyList<PlaybackRateWindow> rateWindows;
    private double position, anchor;
    public bool IsPlaying { get; private set; }
    public double Rate { get; private set; } = 3;
    public bool HighlightsEnabled { get; private set; } = true;
    public bool HighlightActive { get { var time = Position; return HighlightsEnabled && rateWindows.Any(w => w.Start <= time && time < w.End); } }
    public double EffectiveRate => HighlightActive ? 1 : Rate;
    public static bool IsSupportedRate(double rate) => rate is 1 or 2 or 3 or 5;

    public Playback(double duration, IMonotonicClock clock, IEnumerable<PlaybackRateWindow>? windows = null)
    {
        if (!double.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        this.duration = duration;
        this.clock = clock;
        var ordered = (windows ?? []).OrderBy(w => w.Start).ToArray();
        if (ordered.Any(w => !double.IsFinite(w.Start) || !double.IsFinite(w.End) || w.Start < 0 || w.End <= w.Start || w.End > duration))
            throw new ArgumentException("Invalid highlight rate window.", nameof(windows));
        var merged = new List<PlaybackRateWindow>();
        foreach (var window in ordered)
            if (merged.Count > 0 && window.Start <= merged[^1].End) merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, window.End) };
            else merged.Add(window);
        rateWindows = merged;
    }

    public double Position
    {
        get
        {
            if (IsPlaying)
            {
                var current = Advance(position, Math.Max(0, clock.Seconds - anchor));
                if (current < duration) return current;
                position = duration;
                IsPlaying = false;
            }
            return position;
        }
    }

    public void Play()
    {
        if (Position >= duration || IsPlaying) return;
        anchor = clock.Seconds;
        IsPlaying = true;
    }

    public void Pause()
    {
        position = Position;
        IsPlaying = false;
    }

    public void Seek(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        position = Math.Clamp(seconds, 0, duration);
        anchor = clock.Seconds;
        if (position == duration) IsPlaying = false;
    }

    public void Restart()
    {
        IsPlaying = false;
        Seek(0);
    }

    public void SetRate(double rate)
    {
        if (!IsSupportedRate(rate)) throw new ArgumentOutOfRangeException(nameof(rate));
        position = Position;
        anchor = clock.Seconds;
        Rate = rate;
    }
    public void SetHighlights(bool enabled)
    {
        position = Position;
        anchor = clock.Seconds;
        HighlightsEnabled = enabled;
    }
    private double Advance(double from, double wallSeconds)
    {
        if (!HighlightsEnabled || rateWindows.Count == 0) return Math.Min(duration, from + wallSeconds * Rate);
        while (from < duration && wallSeconds > 0)
        {
            var active = rateWindows.FirstOrDefault(w => w.Start <= from && from < w.End);
            var boundary = active?.End ?? rateWindows.FirstOrDefault(w => w.Start > from)?.Start ?? duration;
            var rate = active is null ? Rate : 1;
            var required = (boundary - from) / rate;
            if (wallSeconds <= required) return Math.Min(duration, from + wallSeconds * rate);
            from = boundary; wallSeconds -= required;
        }
        return from;
    }
}

public sealed record PlayerStats(string PlayerId, int Passes, int CompletedPasses, int Shots, int Goals,
    double Distance, double MaxSpeed)
{
    public double? PassAccuracy => Passes == 0 ? null : CompletedPasses / (double)Passes;
}

public sealed record Snapshot(double Time, Position Ball, string? OwnerId,
    IReadOnlyList<MatchEvent> Events, IReadOnlyList<PlayerStats> Players, IReadOnlyDictionary<string, int> Score);

public static class Replay
{
    public static Snapshot At(Match match, double time)
    {
        time = match.ClampTime(time);
        var events = match.Events.Where(e => e.Time <= time).ToArray();
        var stats = match.Players.Select(p =>
        {
            var facts = events.Where(e => e.ActorId == p.Id).ToArray();
            var motion = p.Path.Measure(time);
            return new PlayerStats(p.Id, facts.Count(e => e.Kind == EventKind.Pass),
                facts.Count(e => e.Kind == EventKind.Pass && e.Completed == true),
                facts.Count(e => e.Kind == EventKind.Shot), facts.Count(e => e.Kind == EventKind.Goal),
                motion.Distance, motion.MaxSpeed);
        }).ToArray();
        var score = match.Teams.ToDictionary(t => t.Id,
            t => events.Count(e => e.Kind == EventKind.Goal && match.Player(e.ActorId).TeamId == t.Id));
        return new(time, match.Ball.At(time), match.OwnerAt(time), events, stats, score);
    }
}

public enum ExplanationMode { Casual, Analyst }
public sealed record Explanation(string EventId, double Time, string PlayerId, string Text);

public static class Explanations
{
    public static IReadOnlyList<Explanation> At(Match match, double time, ExplanationMode mode)
    {
        time = match.ClampTime(time);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        return match.Events.Where(e => e.Time <= time).Select(e =>
        {
            var actor = match.Player(e.ActorId);
            var receiver = e.ReceiverId is null ? null : match.Player(e.ReceiverId);
            var fact = e.Kind switch
            {
                EventKind.Pass when e.Completed == true => $"{actor.Name} finds {receiver!.Name} with a completed pass.",
                EventKind.Pass => $"{actor.Name}'s pass is intercepted by {receiver!.Name}.",
                EventKind.Carry => $"{actor.Name} carries the ball.",
                EventKind.Shot => $"{actor.Name} takes a shot.",
                EventKind.Goal => $"{actor.Name} scores for {match.Teams.Single(t => t.Id == actor.TeamId).Name}.",
                EventKind.Kickoff => $"{actor.Name} restarts play.",
                _ => throw new InvalidOperationException("Unsupported event kind.")
            };
            if (mode == ExplanationMode.Analyst)
            {
                var s = Replay.At(match, e.Time).Players.Single(p => p.PlayerId == actor.Id);
                var detail = e.Kind == EventKind.Carry
                    ? $" Carry distance: {actor.Path.Measure(e.Time).Distance - actor.Path.Measure(e.StartTime!.Value).Distance:F1} m."
                    : $" Player totals at this instant: {s.CompletedPasses}/{s.Passes} passes completed, {s.Shots} shots, {s.Goals} goals.";
                fact += detail + " Recorded synthetic event; no inferred metrics.";
            }
            return new Explanation(e.Id, e.Time, actor.Id, fact);
        }).ToArray();
    }
}
