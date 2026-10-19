namespace PitchLens.Core;

public sealed record GoalMoment(string EventId, double Time, string Scorer, string TeamId, IReadOnlyDictionary<string, int> Score);

public static class GoalReplay
{
    public static GoalMoment? At(Match match, double time)
    {
        time = match.ClampTime(time);
        var goal = match.Events.LastOrDefault(e => e.Kind == EventKind.Goal && e.Time <= time && time < e.Time + 6);
        if (goal is null) return null;
        var scorer = match.Player(goal.ActorId);
        return new(goal.Id, goal.Time, scorer.Name, scorer.TeamId, Replay.At(match, goal.Time).Score);
    }
}
