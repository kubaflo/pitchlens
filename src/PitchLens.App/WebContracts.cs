using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.App;

public sealed record WebFrame(double Time, double X, double Y);
public sealed record WebPortrait(string PlayerId,string TeamId,string Name,string ImagePath);
public sealed record WebPlayer(string Id, string TeamId, string Name, int Number, WebFrame[] Frames,WebPortrait? Portrait=null);
public sealed record WebGoal(string Id, double Time, string Scorer, string ScorerId, string TeamId, string TeamName,
    double? ShotStart, double? RestartTime, double BuildUpStart, string[] PlayerIds, double GoalX, double GoalY);
public sealed record WebCaption(string Id, string EventId, double Start, double End, string Text);
public sealed record WebScenario(long Revision, double Duration, WebPlayer[] Players, WebFrame[] Ball, WebGoal[] Goals, WebCaption[] Captions,
    PlaybackRateWindow[] FocusWindows,Team[] Teams);
public sealed record WebClock(long Revision, long Sequence, double Time, bool Playing, string SelectedId, double Rate,
    double EffectiveRate, bool HighlightsEnabled);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WebScenario))]
[JsonSerializable(typeof(WebClock))]
[JsonSerializable(typeof(FilmPlan))]
[JsonSerializable(typeof(PitchLensView))]
public partial class WebJsonContext : JsonSerializerContext;

public static class WebContracts
{
    public static string Scenario(Match match, long revision)
    {
        static WebFrame[] Frames(Trajectory t) => t.Frames.Select(f => new WebFrame(f.Time, f.Position.X, f.Position.Y)).ToArray();
        var highlights = GoalHighlights.For(match);
        var dto = new WebScenario(revision, match.Duration,
            match.Players.Select(p =>
            {
                var image=PlayerPortraitCatalog.Find(p)?.ImagePath;
                return new WebPlayer(p.Id,p.TeamId,p.Name,p.Number,Frames(p.Path),
                    image is null?null:new(p.Id,p.TeamId,p.Name,image));
            }).ToArray(), Frames(match.Ball),
            match.Events.Where(e => e.Kind == EventKind.Goal).Select(e =>
            {
                var scorer = match.Player(e.ActorId);
                var shot = match.Events.Single(s => s.Kind == EventKind.Shot && s.ActorId == e.ActorId && s.Time == e.Time);
                var restart = match.Events.FirstOrDefault(s => s.Kind == EventKind.Kickoff && s.Time > e.Time);
                var highlight = highlights.Single(h => h.EventId == e.Id);
                var goalPoint = match.Ball.At(e.Time);
                return new WebGoal(e.Id, e.Time, scorer.Name, scorer.Id, scorer.TeamId,
                    match.Teams.Single(t => t.Id == scorer.TeamId).Name, shot.StartTime, restart?.Time,
                    highlight.BuildUpStart, highlight.PlayerIds.ToArray(), goalPoint.X, goalPoint.Y);
            }).ToArray(), match.Commentary?.Cues.Select(c => new WebCaption(c.Id, c.EventId, c.Start, c.End, c.Text)).ToArray() ?? [],
            highlights.Select(h => new PlaybackRateWindow(h.BuildUpStart, h.End)).ToArray(),match.Teams.ToArray());
        return JsonSerializer.Serialize(dto, WebJsonContext.Default.WebScenario);
    }
    public static string Clock(PresentationFrame frame) => JsonSerializer.Serialize(
        new WebClock(frame.Revision, frame.Sequence, frame.Snapshot.Time, frame.Playing, frame.SelectedId, frame.Rate,
            frame.EffectiveRate, frame.HighlightsEnabled),
        WebJsonContext.Default.WebClock);
}
