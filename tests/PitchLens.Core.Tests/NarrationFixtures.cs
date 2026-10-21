using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

internal static class NarrationFixtures
{
    public static NarrationDocument Package(Match match, string? legacyTiming = null)
    {
        // Controlled test prose, never bundled or presented as an actual AI recording.
        var cues = CaptionSchedule.For(match).Select((slot,i) => new CommentaryCue("dense-"+i,slot.EventId,slot.Start,slot.End,
            [match.Events.Single(e=>e.Id==slot.EventId).ActorId],
            match.Events.Single(e => e.Id == slot.EventId).Kind == EventKind.Goal ? "The shot ends in the goal." : "The ball changes its route.",
            [slot.EventId])).ToArray();
        var selected = match.Events.Where(e => e.Kind == EventKind.Goal)
            .Concat(match.Events.Where(e => e.Kind == EventKind.Pass && e.Completed == false).Take(2))
            .Concat(new[] { 25d, 75, 115, 225, 285 }.Select(time =>
                match.Events.Last(e => e.Time <= time && e.Kind == EventKind.Pass && e.Completed == true)))
            .DistinctBy(e => e.Id).OrderBy(e => e.Time).ToArray();
        var chapters = selected.Select((e, i) =>
        {
            var refs = match.Events.Where(p => p.Time <= e.Time && p.Time >= e.Time - 20).TakeLast(6).ToArray();
            var kind = e.Kind == EventKind.Goal ? "goal" : e.Completed == false ? "turnover" : "buildup";
            return new NarrativeChapter("chapter-" + i, e.Time, kind, e.Id, refs.Select(p => p.Id).ToArray(),
                [e.ActorId], "Observed move", "The recorded sequence connects the move.",
                "The observed route suggests another way to connect the passing sequence.");
        }).ToArray();
        return new(NarrationPrompt.Hash(match), cues, chapters,2);
    }
    public static string Json(Match match, string? legacyTiming = null) =>
        JsonSerializer.Serialize(Package(match, legacyTiming), NarrationJsonContext.Default.NarrationDocument);
}
