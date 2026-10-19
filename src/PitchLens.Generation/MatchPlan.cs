using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record PlanPoint([property: JsonRequired] double X, [property: JsonRequired] double Y);
public sealed record PlannedAction([property: JsonRequired] string Id, [property: JsonRequired] string Kind,
    [property: JsonRequired] double StartTime, [property: JsonRequired] double Time,
    [property: JsonRequired] string ActorId, [property: JsonRequired] string? ReceiverId,
    [property: JsonRequired] bool? Completed, [property: JsonRequired] PlanPoint Target);
public sealed record PlannedPhase([property: JsonRequired] string Id, [property: JsonRequired] string Kind,
    [property: JsonRequired] double StartTime, [property: JsonRequired] double EndTime,
    [property: JsonRequired] PlanPoint[] Arsenal, [property: JsonRequired] PlanPoint[] City,
    [property: JsonRequired] PlannedAction[] Actions);
public sealed record MatchPlan([property: JsonRequired] double Duration, [property: JsonRequired] string Title,
    [property: JsonRequired] string InitialOwnerId, [property: JsonRequired] PlannedPhase[] Phases, CommentaryCue[]? Commentary = null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MatchPlan))]
public partial class PlanJsonContext : JsonSerializerContext;

public static class FiveMinutePlan
{
    public const double Duration = 300;
    private static void Require([DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new ScenarioValidationException(message);
    }
    public static Match Parse(string json, bool requireCommentary = true)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 200_000, "Empty or oversized match plan.");
        try
        {
            var options = new JsonSerializerOptions(PlanJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 32 };
            var context = new PlanJsonContext(options);
            return Build(JsonSerializer.Deserialize(json, context.MatchPlan) ?? throw new ScenarioValidationException("Null match plan."), requireCommentary);
        }
        catch (JsonException e) { throw new ScenarioValidationException("Malformed five-minute plan JSON.", e); }
    }
    public static Match Build(MatchPlan plan, bool requireCommentary = true)
    {
        Require(plan.Duration == Duration, "Match duration must be 300 seconds; the SDK request timeout is separate.");
        Require(plan.Title != "Seventy-Two Seconds" && StoryQuality.Titles.Contains(plan.Title), "Use a neutral supported five-minute story title.");
        Require(plan.Phases is { Length: 10 } && plan.Phases.All(p => p is not null), "Expected ten evolving thirty-second phases.");
        Require(PremierLeagueRoster.Players.Any(p => p.Id == plan.InitialOwnerId), "Unknown initial owner.");
        var roster = PremierLeagueRoster.Players.ToArray();
        var index = roster.Select((p, i) => (p.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var actions = new List<PlannedAction>();
        for (var n = 0; n < plan.Phases.Length; n++)
        {
            var phase = plan.Phases[n];
            Require(phase.StartTime == n * 30 && phase.EndTime == (n + 1) * 30, "Phases must cover 0-300 without gaps.");
            Require(phase.Id is { Length: >= 1 and <= 32 } && phase.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'), "Phase identifier required.");
            Require(new[] { "possession", "turnover", "counterattack", "finish", "response" }.Contains(phase.Kind), "Unsupported five-minute phase kind.");
            foreach (var shape in new[] { phase.Arsenal, phase.City })
                Require(shape is { Length: 5 } && shape.All(p => p is not null && double.IsFinite(p.X) && double.IsFinite(p.Y) &&
                    p.X is >= 10 and <= 95 && p.Y is >= 7 and <= 61), "Each club needs five valid, spread outfield anchors.");
            Require(phase.Actions is { Length: >= 4 and <= 8 } && phase.Actions.All(a => a is not null), "Each phase needs 4-8 meaningful actions.");
            Require(phase.Actions.All(a => a.StartTime >= phase.StartTime && a.Time > phase.StartTime && a.Time <= phase.EndTime), "Action lies outside its phase.");
            if (phase.Kind == "finish") Require(phase.Actions.Any(a => a.Kind == "shot"), "Finish phase needs an actual shot/goal.");
            if (phase.Kind == "turnover") Require(phase.Actions.Any(a => a.Kind == "pass" && a.Completed == false), "Turnover phase needs an interception.");
            if (phase.Kind == "counterattack") Require(phase.Actions.Any(a => a.Kind == "carry") &&
                phase.Actions.Any(a => a.Kind == "pass" && a.Completed == true), "Counterattack needs actual carries and completed passes.");
            actions.AddRange(phase.Actions);
        }
        Require(plan.Phases.Select(p => p.Id).Distinct().Count() == 10 &&
            actions.Select(a => a.Id).Distinct().Count() == actions.Count, "Plan identifiers must be unique.");
        string? owner = plan.InitialOwnerId;
        string? scoringTeam = null;
        double last = 0;
        var events = new List<MatchEvent>();
        var possession = new List<PossessionChange> { new(0, owner) };
        foreach (var a in actions)
        {
            Require(a.Id is { Length: >= 1 and <= 32 } && a.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'), "Invalid action identifier.");
            Require(a.ActorId is not null && index.ContainsKey(a.ActorId) && (a.ReceiverId is null || index.ContainsKey(a.ReceiverId)), "Unknown roster identity.");
            Require(double.IsFinite(a.StartTime) && double.IsFinite(a.Time) && a.StartTime >= last && a.Time > a.StartTime,
                "Actions must be ordered, nonoverlapping and have positive duration.");
            Require(a.Target is not null && double.IsFinite(a.Target.X) && double.IsFinite(a.Target.Y) &&
                a.Target.X is >= 0 and <= 105 && a.Target.Y is >= 0 and <= 68, "Invalid action target.");
            var actor = roster[index[a.ActorId]];
            if (a.Kind == "kickoff")
            {
                Require(scoringTeam is not null && actor.TeamId != scoringTeam && a.Time >= last + 3 &&
                    a.Target.X == 52.5 && a.Target.Y == 34, "Goal needs an opposing centre restart after three seconds.");
                Require(actor.Id is not ("arsenal-raya" or "manchester-city-donnarumma"), "Use an outfield player for kickoff.");
                Require(a.ReceiverId is null && a.Completed is null, "Kickoff cannot have a receiver/outcome.");
                owner = a.ActorId; scoringTeam = null;
                possession.Add(new(a.Time, owner));
                events.Add(new(a.Id, a.Time, 0, EventKind.Kickoff, a.ActorId));
            }
            else
            {
                Require(scoringTeam is null && a.ActorId == owner, "Action actor must own the ball; restart after a goal.");
                if (a.Kind == "pass")
                {
                    Require(a.ReceiverId is not null && a.ReceiverId != a.ActorId && a.Completed.HasValue &&
                        a.Completed == (roster[index[a.ReceiverId!]].TeamId == actor.TeamId), "Pass outcome/receiver team mismatch.");
                    Require(a.StartTime > possession[^1].Time, "Leave possession time before a pass.");
                    events.Add(new(a.Id, a.Time, 0, EventKind.Pass, a.ActorId, a.ReceiverId, a.Completed, a.StartTime));
                    possession.Add(new(a.StartTime, null)); owner = a.ReceiverId; possession.Add(new(a.Time, owner));
                }
                else if (a.Kind == "carry")
                    events.Add(new(a.Id, a.Time, 0, EventKind.Carry, a.ActorId, StartTime: a.StartTime));
                else if (a.Kind == "shot")
                {
                    Require(a.StartTime > possession[^1].Time, "Leave possession time before a shot.");
                    Require(a.Target.X == (actor.TeamId == "arsenal" ? 105 : 0) && a.Target.Y == 34, "Shot must target the attacking goal.");
                    events.Add(new(a.Id, a.Time, 0, EventKind.Shot, a.ActorId, StartTime: a.StartTime));
                    events.Add(new(a.Id + "-goal", a.Time, 1, EventKind.Goal, a.ActorId));
                    possession.Add(new(a.StartTime, null)); owner = null; possession.Add(new(a.Time, null));
                    scoringTeam = actor.TeamId;
                }
                else throw new ScenarioValidationException("Use pass, carry, shot or kickoff.");
                if (a.Kind != "pass") Require(a.ReceiverId is null && a.Completed is null, "Only passes have a receiver/outcome.");
            }
            last = a.Time;
        }
        Position Shape(int player, double time)
        {
            var n = Math.Min(9, (int)(time / 30));
            var phase = plan.Phases[n]; var next = plan.Phases[Math.Min(9, n + 1)];
            var own = player < 6 ? phase.Arsenal : phase.City;
            var other = player < 6 ? next.Arsenal : next.City;
            var slot = player % 6 - 1;
            var f = Math.Clamp((time - phase.StartTime) / 30, 0, 1);
            return Position.Lerp(new(own[slot].X, own[slot].Y), new(other[slot].X, other[slot].Y), f);
        }
        var positions = roster.Select((p, i) => i % 6 == 0
            ? new Position(i == 0 ? 6 : 99, 34) : Shape(i, 0)).ToArray();
        var frames = roster.Select((p, i) => new List<Keyframe> { new(0, positions[i]) }).ToArray();
        var starts = new Dictionary<string, Position>();
        var times = Enumerable.Range(0, 301).Select(i => (double)i)
            .Concat(actions.SelectMany(a => new[] { a.StartTime, a.Time, Math.Max(0, a.Time - 10) })).Distinct().Order().ToArray();
        foreach (var a in actions.Where(a => a.StartTime == 0)) starts[a.Id] = positions[index[a.ActorId]];
        double previous = 0;
        foreach (var time in times.Skip(1))
        {
            var dt = time - previous;
            var currentOwner = possession.Last(p => p.Time <= previous).PlayerId;
            var ballReference = currentOwner is null ? new Position(52.5, 34) : positions[index[currentOwner]];
            var activeCarry = actions.FirstOrDefault(a => a.Kind == "carry" && a.StartTime < time && time <= a.Time);
            for (var i = 0; i < roster.Length; i++)
            {
                Position target;
                if (i % 6 == 0)
                    target = new(i == 0 ? 6 : 99, Math.Clamp(34 + (ballReference.Y - 34) * .3, 25, 43));
                else
                {
                    target = Shape(i, time);
                    target = new(Math.Clamp(target.X + (ballReference.X - 52.5) * .12, 10, 95),
                        Math.Clamp(target.Y + (ballReference.Y - 34) * .15, 7, 61));
                }
                var arrival = actions.FirstOrDefault(a => a.Time >= time && a.Time - time <= 10 &&
                    ((a.Kind == "pass" && a.ReceiverId == roster[i].Id) || (a.Kind == "kickoff" && a.ActorId == roster[i].Id)));
                if (activeCarry is not null && activeCarry.ActorId == roster[i].Id)
                {
                    var from = starts[activeCarry.Id];
                    target = new(activeCarry.Target.X, activeCarry.Target.Y);
                    Require(from.DistanceTo(target) / (activeCarry.Time - activeCarry.StartTime) <= 8,
                        "Carry target exceeds realistic speed.");
                    positions[i] = Position.Lerp(from, target, (time - activeCarry.StartTime) / (activeCarry.Time - activeCarry.StartTime));
                }
                else if (arrival is not null)
                {
                    target = new(arrival.Target.X, arrival.Target.Y);
                    var remaining = arrival.Time - previous;
                    var required = positions[i].DistanceTo(target) / remaining;
                    Require(required <= 8 + 1e-8, $"Unreachable receiver/restart target for {roster[i].Name} at {arrival.Time}s.");
                    positions[i] = Position.Lerp(positions[i], target, dt / remaining);
                }
                else
                {
                    var distance = positions[i].DistanceTo(target);
                    var speed = i % 6 == 0 ? 2 : 2.4 + (i % 3) * .4;
                    positions[i] = distance <= speed * dt ? target : Position.Lerp(positions[i], target, speed * dt / distance);
                }
                frames[i].Add(new(time, positions[i]));
            }
            foreach (var a in actions.Where(a => a.StartTime == time)) starts[a.Id] = positions[index[a.ActorId]];
            previous = time;
        }
        var players = roster.Select((p, i) => new Player(p.Id, p.TeamId, p.Number, p.Name, new(frames[i]))).ToArray();
        var skeleton = new Match(Duration, PremierLeagueRoster.Teams, players, events,
            new([new(0, new(52.5, 34)), new(Duration, new(52.5, 34))]), possession);
        Position BallAt(double time)
        {
            var flight = actions.FirstOrDefault(a => a.Kind is "pass" or "shot" && a.StartTime <= time && time <= a.Time);
            if (flight is not null)
            {
                var from = skeleton.Player(flight.ActorId).Path.At(flight.StartTime);
                var to = new Position(flight.Target.X, flight.Target.Y);
                return Position.Lerp(from, to, (time - flight.StartTime) / (flight.Time - flight.StartTime));
            }
            var held = possession.Last(p => p.Time <= time).PlayerId;
            if (held is not null) return skeleton.Player(held).Path.At(time);
            var goal = actions.Last(a => a.Kind == "shot" && a.Time <= time);
            var kickoff = actions.FirstOrDefault(a => a.Kind == "kickoff" && a.Time > goal.Time);
            var fromGoal = new Position(goal.Target.X, goal.Target.Y);
            return kickoff is null ? fromGoal : Position.Lerp(fromGoal, new(52.5, 34), (time - goal.Time) / (kickoff.Time - goal.Time));
        }
        var ballFrames = times.Select(t => new Keyframe(t, BallAt(t)));
        var beats = plan.Phases.Select(p => new StoryBeat(p.Id, p.StartTime, p.EndTime, p.Kind,
            events.Where(e => e.Time > p.StartTime && e.Time <= p.EndTime).Select(e => e.Id).ToArray(),
            p.Actions.SelectMany(a => new[] { a.ActorId, a.ReceiverId }).OfType<string>().Distinct().ToArray())).ToArray();
        var match = new Match(Duration, skeleton.Teams, players, events, new(ballFrames), possession, new(plan.Title, beats));
        SustainedQuality.Validate(match);
        Require(!requireCommentary || plan.Commentary is not null, "AI commentator cues are required for a new SDK match.");
        if (plan.Commentary is not null)
        {
            try { return match.WithCommentary(plan.Commentary, CopilotScenarioSource.Model); }
            catch (ArgumentException e) { throw new ScenarioValidationException($"Invalid AI commentary: {e.Message}", e); }
        }
        return match;
    }
}
