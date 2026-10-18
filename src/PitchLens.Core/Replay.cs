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
    private double position, anchor;
    public bool IsPlaying { get; private set; }

    public Playback(double duration, IMonotonicClock clock)
    {
        if (!double.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        this.duration = duration;
        this.clock = clock;
    }

    public double Position
    {
        get
        {
            if (IsPlaying)
            {
                var current = Math.Min(duration, position + Math.Max(0, clock.Seconds - anchor));
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
