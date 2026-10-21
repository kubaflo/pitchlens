using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record DirectorPlayer(string Id, string Name, string TeamId, MomentPoint[] Path,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? Number=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Role=null);
public sealed record DirectorFacts(long Revision, double Cutoff, DirectorPlayer[] Players, NarrationFact[] Events,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Team[]? Clubs=null);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DirectorFacts))]
[JsonSerializable(typeof(FilmPlan))]
public partial class DirectorJsonContext : JsonSerializerContext;

public sealed class DirectorEvidence
{
    public long Revision { get; }
    public double Cutoff { get; }
    public string Json { get; }
    public string Hash { get; }
    public DirectorFacts Facts => JsonSerializer.Deserialize(NarrationPrompt.NormalizeScores(Json), DirectorJsonContext.Default.DirectorFacts)!;
    private DirectorEvidence(DirectorFacts facts)
    {
        Revision = facts.Revision; Cutoff = facts.Cutoff;
        var json=JsonSerializer.Serialize(facts, DirectorJsonContext.Default.DirectorFacts);
        Json = facts.Clubs is null?NarrationPrompt.LegacyScores(json):json;
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
    }
    public static bool CanCapture(Match match, double cutoff) => double.IsFinite(cutoff) && cutoff >= 60 && cutoff <= match.Duration &&
        match.Events.Any(e => e.Kind == EventKind.Goal && e.Time <= cutoff) &&
        match.Events.Count(e => e.Time <= cutoff) >= 12;

    public static DirectorEvidence Capture(Match match, long revision, double cutoff)
    {
        if (!CanCapture(match, cutoff)) throw new InvalidOperationException("Director's Cut needs an observed goal and at least a minute of match evidence.");
        var all = JsonSerializer.Deserialize(NarrationPrompt.NormalizeScores(NarrationPrompt.Facts(match)), NarrationJsonContext.Default.NarrationFacts)!;
        return new(new(revision, cutoff, match.Players.Select(p => new DirectorPlayer(p.Id, p.Name, p.TeamId,
            p.Path.Frames.Where(f => f.Time < cutoff).Append(new(cutoff, p.Path.At(cutoff)))
                .Select(f => new MomentPoint(f.Time, f.Position.X, f.Position.Y)).ToArray(),
                NarrationPrompt.IsHistorical(match)?null:p.Number,NarrationPrompt.IsHistorical(match)?null:p.Role)).ToArray(),
            all.Events.Where(e => e.Time <= cutoff).ToArray(),all.Clubs));
    }
    public FilmPlan Validate(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16000) throw new ArgumentException("Empty or oversized director plan.");
            var options = new JsonSerializerOptions(DirectorJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 16 };
            var plan = JsonSerializer.Deserialize(json, new DirectorJsonContext(options).FilmPlan) ?? throw new ArgumentException("Director plan missing.");
            if (plan.FactsHash != Hash || plan.Revision != Revision || plan.Cutoff != Cutoff)
                throw new ArgumentException("Director plan does not match the captured scenario/revision/cutoff.");
            if (plan.Scenes is not { Length: >= 4 and <= 6 } || plan.Scenes.Any(s => s is null) ||
                plan.Scenes.Select(s => s.Id).Distinct().Count() != plan.Scenes.Length ||
                plan.Scenes.Select(s => s.FocusEventId).Distinct().Count() != plan.Scenes.Length)
                throw new ArgumentException("Director's Cut needs four to six uniquely identified purposeful scenes.");
            var facts = Facts;
            double previousStart = -1;
            foreach (var scene in plan.Scenes)
            {
                if (scene.Id is not { Length: >= 1 and <= 40 } || !scene.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ||
                    !double.IsFinite(scene.SourceStart) || !double.IsFinite(scene.SourceEnd) || scene.SourceStart < 0 ||
                    scene.SourceEnd > Cutoff || scene.SourceEnd - scene.SourceStart is < 2 or > 12 ||
                    scene.SourceStart < previousStart || scene.Rate is not (.5 or 1) ||
                    !double.IsFinite(scene.HoldSeconds) || scene.HoldSeconds is < 2 or > 6 ||
                    scene.Camera is not ("tactical" or "finish"))
                    throw new ArgumentException("Invalid clip bounds, chronological order, rate, hold or camera.");
                var focus = facts.Events.SingleOrDefault(e => e.Id == scene.FocusEventId);
                if (focus is null || focus.Time != scene.SourceEnd) throw new ArgumentException("Scene must finish at its exact observed focus event.");
                if (scene.EvidenceIds is not { Length: >= 1 and <= 6 } || !scene.EvidenceIds.Contains(scene.FocusEventId) ||
                    scene.EvidenceIds.Distinct().Count() != scene.EvidenceIds.Length ||
                    scene.EvidenceIds.Any(id => !facts.Events.Any(e => e.Id == id && e.Time >= scene.SourceStart && e.Time <= scene.SourceEnd)))
                    throw new ArgumentException("Scene evidence must resolve inside that clip and be completed before the explanation hold.");
                if (scene.Camera == "finish" && focus.Kind != "goal") throw new ArgumentException("Finish camera requires an observed goal.");
                NarrativeText.Validate(scene.Text, 8, 160);
                foreach (var player in facts.Players)
                    if (scene.Text.Contains(player.Name, StringComparison.OrdinalIgnoreCase) &&
                        !scene.EvidenceIds.Any(id => facts.Events.Any(e => e.Id == id && (e.ActorId == player.Id || e.ReceiverId == player.Id))))
                        throw new ArgumentException("Named film player is not in that scene's observed evidence.");
                var words = NarrativeText.Words(scene.Text);
                if (focus.Kind != "goal" && (NarrativeText.HasScoringClaim(scene.Text) || words.Any(w => w is "lead" or "level")))
                    throw new ArgumentException($"Scene '{scene.Id}' at source {scene.SourceEnd:0.###}s (cutoff {Cutoff:0.###}s): outcome wording requires its observed goal.");
                var homeId=facts.Clubs?[0].Id ?? "arsenal";
                var geometry = facts.Events.Where(e=>scene.EvidenceIds.Contains(e.Id)).Select(e=>new NarrativeText.GoalGeometry(
                    e.Ball.Select(p=>new Position(p.X,p.Y)).ToArray(),facts.Players.Single(p=>p.Id==e.ActorId).TeamId==homeId));
                if(!NarrativeText.SpatialGoalReferencesSupported(scene.Text,geometry))
                    throw new ArgumentException($"Scene '{scene.Id}' at source {scene.SourceEnd:0.###}s (cutoff {Cutoff:0.###}s): spatial goal wording lacks observed geometry.");
                if (words.Any(w => w is "equalizer" or "equaliser") && focus.HomeGoals != focus.AwayGoals)
                    throw new ArgumentException("Equalizer language requires a locally tied score.");
                previousStart = scene.SourceStart;
            }
            if (FilmTimeline.Duration(plan) is < 45 or > 60) throw new ArgumentException("Measured film duration must be45–60wall seconds including holds.");
            if (!plan.Scenes.Any(s => facts.Events.Single(e => e.Id == s.FocusEventId).Kind == "goal") ||
                !plan.Scenes.Any(s => facts.Events.Single(e => e.Id == s.FocusEventId).Kind != "goal"))
                throw new ArgumentException("Film needs actual setup/response evidence as well as a goal, not just a goal loop.");
            return plan;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        { throw new ScenarioValidationException($"Invalid Director's Cut: {e.Message}", e); }
    }
}
