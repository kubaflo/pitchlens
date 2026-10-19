using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.App;

public sealed record WebFrame(double Time, double X, double Y);
public sealed record WebPlayer(string Id, string TeamId, string Name, int Number, WebFrame[] Frames);
public sealed record WebGoal(string Id, double Time, string Scorer, string TeamId, string TeamName);
public sealed record WebCaption(string Id, double Start, double End, string Text);
public sealed record WebScenario(long Revision, double Duration, WebPlayer[] Players, WebFrame[] Ball, WebGoal[] Goals, WebCaption[] Captions);
public sealed record WebClock(long Revision, long Sequence, double Time, bool Playing, string SelectedId, double Rate);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WebScenario))]
[JsonSerializable(typeof(WebClock))]
public partial class WebJsonContext : JsonSerializerContext;

public static class WebContracts
{
    public static string Scenario(Match match, long revision)
    {
        static WebFrame[] Frames(Trajectory t) => t.Frames.Select(f => new WebFrame(f.Time, f.Position.X, f.Position.Y)).ToArray();
        var dto = new WebScenario(revision, match.Duration,
            match.Players.Select(p => new WebPlayer(p.Id, p.TeamId, p.Name, p.Number, Frames(p.Path))).ToArray(), Frames(match.Ball),
            match.Events.Where(e => e.Kind == EventKind.Goal).Select(e =>
            {
                var scorer = match.Player(e.ActorId);
                return new WebGoal(e.Id, e.Time, scorer.Name, scorer.TeamId, match.Teams.Single(t => t.Id == scorer.TeamId).Name);
            }).ToArray(), match.Commentary?.Cues.Select(c => new WebCaption(c.Id, c.Start, c.End, c.Text)).ToArray() ?? []);
        return JsonSerializer.Serialize(dto, WebJsonContext.Default.WebScenario);
    }
    public static string Clock(PresentationFrame frame) => JsonSerializer.Serialize(
        new WebClock(frame.Revision, frame.Sequence, frame.Snapshot.Time, frame.Playing, frame.SelectedId, frame.Rate),
        WebJsonContext.Default.WebClock);
}
