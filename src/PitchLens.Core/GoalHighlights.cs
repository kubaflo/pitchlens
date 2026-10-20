namespace PitchLens.Core;

public sealed record PlaybackRateWindow(double Start, double End);
public sealed record GoalHighlight(string EventId, double BuildUpStart, double ShotStart, double GoalTime, double End,
    IReadOnlyList<string> PlayerIds);

public static class GoalHighlights
{
    public static IReadOnlyList<GoalHighlight> For(Match match) => match.Events.Where(e => e.Kind == EventKind.Goal).Select(goal =>
    {
        var shot = match.Events.Single(e => e.Kind == EventKind.Shot && e.ActorId == goal.ActorId && e.Time == goal.Time);
        var start = Math.Max(0, goal.Time - 10);
        var end = match.Events.FirstOrDefault(e => e.Kind == EventKind.Kickoff && e.Time > goal.Time)?.Time
            ?? Math.Min(match.Duration, goal.Time + 8);
        var actors = match.Events.Where(e => e.Time >= start && e.Time <= goal.Time)
            .SelectMany(e => new[] { e.ActorId, e.ReceiverId }).OfType<string>()
            .Append(match.Players.FirstOrDefault(p => p.TeamId != match.Player(goal.ActorId).TeamId &&
                match.IsKeeper(p.Id))?.Id)
            .OfType<string>().Distinct().ToArray();
        return new GoalHighlight(goal.Id, start, shot.StartTime!.Value, goal.Time, end, actors);
    }).ToArray();
}
