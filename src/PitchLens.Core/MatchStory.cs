namespace PitchLens.Core;

public sealed record StoryBeat(string Id, double StartTime, double EndTime, string Kind,
    IReadOnlyList<string> EventIds, IReadOnlyList<string> PlayerIds);
public sealed record MatchStory(string Title, IReadOnlyList<StoryBeat> Beats);
public sealed record StoryMoment(string Id, double Time, string Heading, string Detail, IReadOnlyList<string> EventIds);

public static class StoryReplay
{
    public static IReadOnlyList<StoryMoment> At(Match match, double time)
    {
        time = match.ClampTime(time);
        if (match.Narrative is not null)
            return NarrativeReplay.At(match, time).Select(c => new StoryMoment(c.Id, c.RevealTime, c.Heading, c.Fan, c.EventIds)).ToArray();
        if (match.Story is null) return [];
        return match.Story.Beats.Where(b => b.EndTime <= time).Select(b =>
        {
            var events = b.EventIds.Select(id => match.Events.Single(e => e.Id == id)).ToArray();
            var facts = events.Select(e => e.Kind switch
            {
                EventKind.Pass => $"{match.Player(e.ActorId).Name} → {match.Player(e.ReceiverId!).Name}" +
                    (e.Completed == true ? " (completed)" : " (intercepted)"),
                EventKind.Carry => $"{match.Player(e.ActorId).Name} carries " +
                    $"{match.Player(e.ActorId).Path.Measure(e.Time).Distance - match.Player(e.ActorId).Path.Measure(e.StartTime!.Value).Distance:F0} m",
                EventKind.Shot => $"{match.Player(e.ActorId).Name} shoots",
                EventKind.Goal => $"{match.Player(e.ActorId).Name} scores",
                EventKind.Kickoff => $"{match.Player(e.ActorId).Name} restarts",
                _ => throw new InvalidOperationException("Unknown story event.")
            });
            var heading = b.Kind switch
            {
                "build-up" => "Building the move",
                "turnover" when events.FirstOrDefault(e => e.Kind == EventKind.Pass && e.Completed == false) is { } turnover =>
                    $"{match.Player(turnover.ReceiverId!).Name} cuts it out!",
                "turnover" => "Possession turned",
                "counterattack" => "The break is on",
                "support-run" => "A runner in space",
                "finish" when events.FirstOrDefault(e => e.Kind == EventKind.Goal) is { } scored =>
                    $"{match.Player(scored.ActorId).Name}. GOAL!",
                "finish" => "The finishing move",
                "response" => "Straight back to it",
                "possession" => "Keep it moving",
                _ => throw new InvalidOperationException("Unknown story beat.")
            };
            var movement = b.PlayerIds.Select(match.Player).Select(p => (p.Name, p.Id,
                Distance: p.Path.Measure(b.EndTime).Distance - p.Path.Measure(b.StartTime).Distance))
                .OrderByDescending(p => p.Distance).ThenBy(p => p.Id, StringComparer.Ordinal).Take(2);
            var detail = string.Join(" · ", facts) + ". Movement: " +
                string.Join(", ", movement.Select(p => $"{p.Name} {p.Distance:F0} m")) + ".";
            return new StoryMoment(b.Id, b.EndTime, heading, detail, b.EventIds);
        }).ToArray();
    }
}
