using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record AuthoredPlayer([property: JsonRequired] string Id, [property: JsonRequired] double[][] Frames);
public sealed record AuthoredMatchDocument([property: JsonRequired] double Duration,
    [property: JsonRequired] string Title, [property: JsonRequired] string InitialOwnerId,
    [property: JsonRequired] AuthoredPlayer[] Players, [property: JsonRequired] EventDocument[] Events,
    [property: JsonRequired]     string[] PhaseKinds, CommentaryCue[]? Commentary = null, NarrationDocument? Narration = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AuthoredMatchDocument))]
[JsonSerializable(typeof(MatchRosterPlayer[]))]
public partial class AuthoredJsonContext : JsonSerializerContext;

public static class AuthoredMatchPlan
{
    private static void Require([DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new ScenarioValidationException(message);
    }
    public static AuthoredMatchDocument Document(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 200_000, "Empty or oversized 22-player scenario.");
        try
        {
            var options = new JsonSerializerOptions(AuthoredJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 24 };
            return JsonSerializer.Deserialize(json, new AuthoredJsonContext(options).AuthoredMatchDocument)
                ?? throw new ScenarioValidationException("Null 22-player scenario.");
        }
        catch (JsonException e) { throw new ScenarioValidationException($"Invalid authored-path JSON: {e.Message}", e); }
    }
    public static Match Parse(string json, bool requireCommentary = true) => Build(Document(json), requireCommentary);
    public static Match Build(AuthoredMatchDocument document, bool requireCommentary = true)
    {
        Require(document.Duration == 300, "Authored match must last 300 replay seconds.");
        Require(document.Title is "Pressure and Space" or "The Turning Tide" or "A Game of Transitions", "Use a neutral match title.");
        Require(document.Players is { Length: 22 } && document.Players.All(p => p is not null) &&
            document.Players.Select(p => p.Id).Distinct().Count() == 22, "All 22 unique verified players need explicit AI paths.");
        Require(document.Players.All(p => RegulationRoster.Players.Any(r => r.Id == p.Id)), "Unknown 11v11 roster identity.");
        Require(document.Events is { Length: >= 45 and <= 90 } && document.Events.All(e => e is not null), "Expected 45-90 substantive events.");
        Require(document.PhaseKinds is { Length: 10 } && document.PhaseKinds.All(k =>
            k is "possession" or "turnover" or "counterattack" or "finish" or "response"), "Ten valid 30-second phase tags required.");
        var players = RegulationRoster.Players.Select(r =>
        {
            var entry = document.Players.Single(p => p.Id == r.Id);
            Require(entry.Frames is { Length: >= 31 and <= 81 } && entry.Frames.All(f => f is { Length: 3 }),
                $"Expected 31-81 explicit [time,x,y] knots for {r.Id}.");
            // Every knot is copied from the model; no solver, role offsets or support-path expansion.
            return new Player(r.Id, r.TeamId, r.Number, r.Name,
                new(entry.Frames.Select(f => new Keyframe(f[0], new(f[1], f[2])))));
        }).ToArray();
        Require(players.Any(p => p.Id == document.InitialOwnerId), "Unknown initial owner.");
        Require(document.Events.All(e => e.Id is { Length: >= 1 and <= 40 } &&
            e.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')), "Invalid event ID.");
        var events = document.Events.Select(e => new MatchEvent(e.Id, e.Time, e.Order, e.Kind switch
        {
            "pass" => EventKind.Pass,
            "carry" => EventKind.Carry,
            "shot" => EventKind.Shot,
            "goal" => EventKind.Goal,
            "kickoff" => EventKind.Kickoff,
            _ => throw new ScenarioValidationException("Unsupported authored event kind.")
        }, e.ActorId, e.ReceiverId, e.Completed, e.StartTime)).ToArray();
        try
        {
            var possession = new List<PossessionChange> { new(0, document.InitialOwnerId) };
            var skeleton = new Match(300, RegulationRoster.Teams, players, events,
                new([new(0, new(52.5, 34)), new(300, new(52.5, 34))]), possession);
            string? owner = document.InitialOwnerId;
            double lastEnd = 0;
            MatchEvent? waitingGoal = null;
            foreach (var e in events)
            {
                if (e.Kind == EventKind.Goal)
                {
                    Require(waitingGoal is null && e.StartTime is null, "Duplicate goal or invalid goal start time.");
                    waitingGoal = e;
                    continue;
                }
                if (e.Kind == EventKind.Kickoff)
                {
                    Require(waitingGoal is not null && e.Time >= waitingGoal.Time + 3 &&
                        skeleton.Player(e.ActorId).TeamId != skeleton.Player(waitingGoal.ActorId).TeamId &&
                        !RegulationRoster.IsKeeper(e.ActorId) && e.StartTime is null, "Goal needs an opposing outfield restart after >=3s.");
                    Require(skeleton.Player(e.ActorId).Path.At(e.Time).DistanceTo(new(52.5, 34)) < .001,
                        "AI-authored kickoff path must reach the centre; it is not auto-corrected.");
                    owner = e.ActorId; possession.Add(new(e.Time, owner)); waitingGoal = null; lastEnd = e.Time;
                    continue;
                }
                Require(waitingGoal is null && e.ActorId == owner && e.StartTime.HasValue &&
                    e.StartTime >= lastEnd && e.Time - e.StartTime is >= 1 and <= 10, "Invalid ownership, overlap or action duration.");
                if (e.Kind == EventKind.Carry)
                    Require(skeleton.Player(e.ActorId).Path.Measure(e.Time).Distance -
                        skeleton.Player(e.ActorId).Path.Measure(e.StartTime.Value).Distance >= 3, "Carry must move on its explicit authored path.");
                else
                {
                    Require(e.StartTime > possession[^1].Time, "Flight must leave time after the previous possession change.");
                    if (e.Kind == EventKind.Shot)
                        Require(events.Count(g => g.Kind == EventKind.Goal && g.ActorId == e.ActorId && g.Time == e.Time) == 1,
                            "This prototype supports explicitly paired scoring shots.");
                    possession.Add(new(e.StartTime.Value, null)); owner = e.ReceiverId; possession.Add(new(e.Time, owner));
                    if (e.Kind == EventKind.Pass && e.Completed == true) ValidateOnside(skeleton, e);
                }
                lastEnd = e.Time;
            }
            Position BallAt(double time)
            {
                var flight = events.FirstOrDefault(e => e.Kind is EventKind.Pass or EventKind.Shot && e.StartTime <= time && time <= e.Time);
                if (flight is not null)
                {
                    var from = skeleton.Player(flight.ActorId).Path.At(flight.StartTime!.Value);
                    var to = flight.ReceiverId is null ? Goal(skeleton.Player(flight.ActorId).TeamId) : skeleton.Player(flight.ReceiverId).Path.At(flight.Time);
                    return Position.Lerp(from, to, (time - flight.StartTime.Value) / (flight.Time - flight.StartTime.Value));
                }
                var held = possession.Last(p => p.Time <= time).PlayerId;
                if (held is not null) return skeleton.Player(held).Path.At(time);
                var goal = events.Last(e => e.Kind == EventKind.Goal && e.Time <= time);
                var restart = events.FirstOrDefault(e => e.Kind == EventKind.Kickoff && e.Time > goal.Time);
                return restart is null ? Goal(skeleton.Player(goal.ActorId).TeamId) :
                    Position.Lerp(Goal(skeleton.Player(goal.ActorId).TeamId), new(52.5, 34), (time - goal.Time) / (restart.Time - goal.Time));
            }
            var times = players.SelectMany(p => p.Path.Frames.Select(f => f.Time)).Concat(events.Select(e => e.Time))
                .Concat(possession.Select(p => p.Time)).Distinct().Order();
            var beats = document.PhaseKinds.Select((kind, n) =>
            {
                var evidence = events.Where(e => e.Time > n * 30 && e.Time <= (n + 1) * 30).ToArray();
                Require(evidence.Length > 0, "Every phase needs observed actions.");
                if (kind == "finish") Require(evidence.Any(e => e.Kind == EventKind.Goal), "Finish phase lacks goal evidence.");
                if (kind == "turnover") Require(evidence.Any(e => e.Kind == EventKind.Pass && e.Completed == false), "Turnover phase lacks interception.");
                return new StoryBeat("phase-" + (n + 1), n * 30, (n + 1) * 30, kind,
                    evidence.Select(e => e.Id).ToArray(), evidence.SelectMany(e => new[] { e.ActorId, e.ReceiverId }).OfType<string>().Distinct().ToArray());
            }).ToArray();
            var match = new Match(300, skeleton.Teams, players, events, new(times.Select(t => new Keyframe(t, BallAt(t)))),
                possession, new(document.Title, beats));
            AuthoredPathQuality.Validate(match);
            Require(document.Narration is null || document.Commentary is null, "Caption-only and coherent narration payloads cannot conflict.");
            if (document.Narration is not null) return NarrationPrompt.Attach(document.Narration, match);
            Require(!requireCommentary || document.Commentary is not null, "New 11v11 games need their own AI commentary.");
            return document.Commentary is null ? match : match.WithCommentary(document.Commentary, CopilotScenarioSource.Model);
        }
        catch (ArgumentException e) { throw new ScenarioValidationException($"Invalid AI-authored match: {e.Message}", e); }
    }
    private static Position Goal(string teamId) => new(teamId == "arsenal" ? 105 : 0, 34);
    private static void ValidateOnside(Match match, MatchEvent pass)
    {
        var actor = match.Player(pass.ActorId);
        var receiver = match.Player(pass.ReceiverId!).Path.At(pass.StartTime!.Value);
        var opponents = match.Players.Where(p => p.TeamId != actor.TeamId).Select(p => p.Path.At(pass.StartTime.Value).X).Order().ToArray();
        var ball = actor.Path.At(pass.StartTime.Value);
        Require(actor.TeamId == "arsenal" ? receiver.X <= Math.Max(52.5, Math.Max(ball.X, opponents[^2])) + 1.5
            : receiver.X >= Math.Min(52.5, Math.Min(ball.X, opponents[1])) - 1.5, $"Obvious offside receiver at {pass.Id}.");
    }
}
