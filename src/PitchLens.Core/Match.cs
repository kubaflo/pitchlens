namespace PitchLens.Core;

public readonly record struct Position(double X, double Y)
{
    public double DistanceTo(Position other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    public static Position Lerp(Position a, Position b, double fraction) => fraction switch
    {
        0 => a,
        1 => b,
        _ => new(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction)
    };
}

public sealed record Keyframe(double Time, Position Position);
public sealed record Team(string Id, string Name);
public sealed record Player(string Id, string TeamId, int Number, string Name, Trajectory Path);
public enum EventKind { Pass, Carry, Shot, Goal, Kickoff }
public sealed record MatchEvent(string Id, double Time, int Order, EventKind Kind,
    string ActorId, string? ReceiverId = null, bool? Completed = null, double? StartTime = null);

public sealed class Trajectory
{
    public IReadOnlyList<Keyframe> Frames { get; }

    public Trajectory(IEnumerable<Keyframe> frames) => Frames = Array.AsReadOnly(frames.ToArray());

    public Position At(double time)
    {
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        if (Frames.Count == 0) throw new InvalidOperationException("Trajectory has no frames.");
        if (time <= Frames[0].Time) return Frames[0].Position;
        for (var i = 1; i < Frames.Count; i++)
        {
            var b = Frames[i];
            if (time <= b.Time)
            {
                if (time == b.Time) return b.Position;
                var a = Frames[i - 1];
                return Position.Lerp(a.Position, b.Position, (time - a.Time) / (b.Time - a.Time));
            }
        }
        return Frames[^1].Position;
    }

    public (double Distance, double MaxSpeed) Measure(double time)
    {
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        double distance = 0, maxSpeed = 0;
        for (var i = 1; i < Frames.Count; i++)
        {
            var a = Frames[i - 1];
            var b = Frames[i];
            var elapsed = Math.Clamp(time - a.Time, 0, b.Time - a.Time);
            if (elapsed <= 0) break;
            var speed = a.Position.DistanceTo(b.Position) / (b.Time - a.Time);
            distance += speed * elapsed;
            maxSpeed = Math.Max(maxSpeed, speed);
        }
        return (distance, maxSpeed);
    }
}

public sealed record PossessionChange(double Time, string? PlayerId);

public sealed class Match
{
    public const double PitchWidth = 105;
    public const double PitchHeight = 68;
    public double Duration { get; }
    public IReadOnlyList<Team> Teams { get; }
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<MatchEvent> Events { get; }
    public Trajectory Ball { get; }
    public IReadOnlyList<PossessionChange> Possession { get; }
    public MatchStory? Story { get; }
    public CommentaryTrack? Commentary { get; }
    public NarrativeTrack? Narrative { get; }

    public Match(double duration, IEnumerable<Team> teams, IEnumerable<Player> players,
        IEnumerable<MatchEvent> events, Trajectory ball, IEnumerable<PossessionChange> possession, MatchStory? story = null,
        IEnumerable<CommentaryCue>? commentary = null, string? commentaryModel = null,
        IEnumerable<NarrativeChapter>? narrative = null, string? narrationFactsHash = null)
    {
        Duration = duration;
        Teams = Array.AsReadOnly(teams.ToArray());
        Players = Array.AsReadOnly(players.ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        Ball = ball;
        Possession = Array.AsReadOnly(possession.ToArray());
        Story = story;
        Validate();
        if (commentary is not null) Commentary = new(this, commentaryModel ?? throw new ArgumentException("Commentary model required."), commentary, narrationFactsHash);
        if (narrative is not null) Narrative = new(this, commentaryModel ?? throw new ArgumentException("Narration model required."),
            narrationFactsHash ?? throw new ArgumentException("Narration facts hash required."), narrative);
    }

    public Match WithCommentary(IEnumerable<CommentaryCue> cues, string model) =>
        new(Duration, Teams, Players, Events, Ball, Possession, Story, cues, model);
    public Match WithNarrative(IEnumerable<CommentaryCue> cues, IEnumerable<NarrativeChapter> chapters, string model, string factsHash) =>
        new(Duration, Teams, Players, Events, Ball, Possession, Story, cues, model, chapters, factsHash);

    public Player Player(string id) => Players.Single(p => p.Id == id);
    public double ClampTime(double time) => double.IsFinite(time)
        ? Math.Clamp(time, 0, Duration) : throw new ArgumentOutOfRangeException(nameof(time));
    public string? OwnerAt(double time)
    {
        time = ClampTime(time);
        return Possession.Last(p => p.Time <= time).PlayerId;
    }

    private void Validate()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }
        static bool Unique(IEnumerable<string> ids)
        {
            var list = ids.ToArray();
            return list.All(id => !string.IsNullOrWhiteSpace(id)) && list.Distinct().Count() == list.Length;
        }
        Require(double.IsFinite(Duration) && Duration > 0, "Duration must be finite and positive.");
        Require(Teams.Count == 2 && Unique(Teams.Select(t => t.Id)), "Exactly two uniquely identified teams are required.");
        Require(Players.Count > 0 && Unique(Players.Select(p => p.Id)), "Player IDs must be nonempty and unique.");
        Require(Players.All(p => Teams.Any(t => t.Id == p.TeamId) && p.Number > 0 &&
            !string.IsNullOrWhiteSpace(p.Name)), "Players need a valid team, shirt number and name.");
        Require(Teams.All(t => Players.Any(p => p.TeamId == t.Id)), "Both teams must have players.");
        foreach (var path in Players.Select(p => p.Path).Append(Ball))
        {
            Require(path.Frames.Count >= 2 && path.Frames[0].Time == 0 &&
                path.Frames[^1].Time == Duration, "Trajectories must cover the entire match.");
            double previous = -1;
            foreach (var frame in path.Frames)
            {
                Require(double.IsFinite(frame.Time) && frame.Time > previous && frame.Time <= Duration,
                    "Trajectory timestamps must be finite and strictly increasing.");
                Require(double.IsFinite(frame.Position.X) && double.IsFinite(frame.Position.Y) &&
                    frame.Position.X >= 0 && frame.Position.X <= PitchWidth &&
                    frame.Position.Y >= 0 && frame.Position.Y <= PitchHeight, "Coordinates must lie on the pitch.");
                previous = frame.Time;
            }
        }
        Require(Unique(Events.Select(e => e.Id)), "Event IDs must be nonempty and unique.");
        (double Time, int Order) last = (-1, -1);
        foreach (var e in Events)
        {
            Require(double.IsFinite(e.Time) && e.Time >= 0 && e.Time <= Duration && e.Order >= 0 &&
                (e.Time > last.Time || (e.Time == last.Time && e.Order > last.Order)),
                "Events must be ordered by timestamp and unique sequence number.");
            Require(Players.Any(p => p.Id == e.ActorId) &&
                (e.ReceiverId is null || Players.Any(p => p.Id == e.ReceiverId)), "Event references an unknown player.");
            Require(Enum.IsDefined(e.Kind), "Unknown event kind.");
            Require(e.StartTime is null || (double.IsFinite(e.StartTime.Value) &&
                e.StartTime >= 0 && e.StartTime < e.Time), "Invalid event start time.");
            if (e.Kind == EventKind.Pass)
            {
                Require(e.ReceiverId is not null && e.ReceiverId != e.ActorId &&
                    e.Completed.HasValue && e.StartTime.HasValue, "Pass needs receiver, outcome and start time.");
                Require(e.Completed == (Player(e.ActorId).TeamId == Player(e.ReceiverId!).TeamId),
                    "Pass outcome must agree with receiver team.");
            }
            else
                Require(e.ReceiverId is null && e.Completed is null, "Only passes have receivers and completion outcomes.");
            if (e.Kind is EventKind.Carry or EventKind.Shot)
                Require(e.StartTime.HasValue, "Carry and shot need a start time.");
            if (e.Kind == EventKind.Goal)
                Require(Events.Any(s => s.Kind == EventKind.Shot && s.ActorId == e.ActorId &&
                    s.Time == e.Time && s.Order < e.Order), "Goal needs a preceding shot at the same timestamp.");
            last = (e.Time, e.Order);
        }
        Require(Possession.Count > 0 && Possession[0].Time == 0, "Initial possession is required.");
        double prior = -1;
        foreach (var p in Possession)
        {
            Require(double.IsFinite(p.Time) && p.Time > prior && p.Time <= Duration &&
                (p.PlayerId is null || Players.Any(x => x.Id == p.PlayerId)), "Invalid possession change.");
            prior = p.Time;
        }
    }
}
