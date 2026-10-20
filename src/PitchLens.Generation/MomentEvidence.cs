using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record MomentPoint(double Time, double X, double Y);
public sealed record MomentEvent(string Id, double Time, double Start, string Kind, string Actor, string? Receiver,
    bool? Completed, MomentPoint[] Ball);
public sealed record MomentMetric(string PlayerId, string Name, int Passes, int CompletedPasses, int Shots, int Goals,
    double DistanceMetres, double MaxSpeedMetresPerSecond, MomentPoint[] Path);
public sealed record MomentFacts(long Revision, double Cutoff, string FocusId, MomentEvent[] Events, MomentMetric[] Players);
public sealed record MomentIndexEvent(string Id, double Time, string Kind, string Actor, string? Receiver, bool? Completed);
public sealed record MomentIndex(long Revision, double Cutoff, string FocusId, MomentIndexEvent[] Events, MomentMetric[] Players);
public sealed record MomentClaim(string Text, string[] EvidenceIds);
public sealed record MomentAnswer(string SnapshotHash, MomentClaim[] Fan, MomentClaim[] Analyst);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MomentFacts))]
[JsonSerializable(typeof(MomentEvent))]
[JsonSerializable(typeof(MomentIndex))]
[JsonSerializable(typeof(MomentAnswer))]
public partial class MomentJsonContext : JsonSerializerContext;

public sealed class MomentEvidence
{
    public long Revision { get; }
    public double Cutoff { get; }
    public string FocusId { get; }
    public string Json { get; }
    public string Hash { get; }
    public MomentFacts Facts => JsonSerializer.Deserialize(Json, MomentJsonContext.Default.MomentFacts)!;

    private MomentEvidence(MomentFacts facts)
    {
        Revision = facts.Revision; Cutoff = facts.Cutoff; FocusId = facts.FocusId;
        Json = JsonSerializer.Serialize(facts, MomentJsonContext.Default.MomentFacts);
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
    }

    public static MomentEvidence Capture(Match match, long revision, double cutoff, string? focusId = null, string[]? evidenceIds = null)
    {
        if (!double.IsFinite(cutoff) || cutoff < 0 || cutoff > match.Duration)
            throw new ArgumentOutOfRangeException(nameof(cutoff));
        var observed = match.Events.Where(e => e.Time <= cutoff).ToArray();
        var focus = focusId is null ? observed.LastOrDefault(e => e.Kind != EventKind.Kickoff) ?? observed.LastOrDefault()
            : observed.SingleOrDefault(e => e.Id == focusId);
        if (focus is null) throw new InvalidOperationException("Choose an event already observed in this match.");
        if (evidenceIds is not null && (evidenceIds.Length is < 1 or > 12 || !evidenceIds.Contains(focus.Id) ||
            evidenceIds.Distinct().Count() != evidenceIds.Length || evidenceIds.Any(id => !observed.Any(e => e.Id == id && e.Time <= focus.Time))))
            throw new InvalidOperationException("Explicit visual evidence must resolve through the captured focus.");
        var window = evidenceIds is null ? observed.Where(e => e.Time <= focus.Time).TakeLast(12) :
            observed.Where(e => evidenceIds.Contains(e.Id));
        var events = window.Select(e =>
        {
            var start = e.StartTime ?? e.Time;
            var points = match.Ball.Frames.Where(f => f.Time > start && f.Time < e.Time)
                .Prepend(new(start, match.Ball.At(start))).Append(new(e.Time, match.Ball.At(e.Time)))
                .DistinctBy(f => f.Time).Select(f => new MomentPoint(f.Time, f.Position.X, f.Position.Y)).ToArray();
            return new MomentEvent(e.Id, e.Time, start, e.Kind.ToString(), match.Player(e.ActorId).Name,
                e.ReceiverId is null ? null : match.Player(e.ReceiverId).Name, e.Completed, points);
        }).ToArray();
        var stats = Replay.At(match, cutoff).Players;
        var players = match.Players.Select(p =>
        {
            var s = stats.Single(s => s.PlayerId == p.Id);
            var start = Math.Max(0, (focus.StartTime ?? focus.Time) - 15);
            var path = p.Path.Frames.Where(f => f.Time > start && f.Time < cutoff)
                .Prepend(new(start, p.Path.At(start))).Append(new(cutoff, p.Path.At(cutoff)))
                .DistinctBy(f => f.Time).Select(f => new MomentPoint(f.Time, f.Position.X, f.Position.Y)).ToArray();
            return new MomentMetric(p.Id, p.Name, s.Passes, s.CompletedPasses, s.Shots, s.Goals,
                s.Distance, s.MaxSpeed, path);
        }).ToArray();
        return new(new(revision, cutoff, focus.Id, events, players));
    }

    public MomentEvent Resolve(string id) => Facts.Events.SingleOrDefault(e => e.Id == id)
        ?? throw new InvalidOperationException("Evidence is not in this captured observed window.");

    public MomentAnswer Validate(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16000) throw new ArgumentException("Empty or oversized explanation.");
            var options = new JsonSerializerOptions(MomentJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 12 };
            var answer = JsonSerializer.Deserialize(json, new MomentJsonContext(options).MomentAnswer)
                ?? throw new ArgumentException("Explanation is missing.");
            if (answer.SnapshotHash != Hash) throw new ArgumentException("Explanation belongs to a different captured snapshot.");
            foreach (var mode in new[] { answer.Fan, answer.Analyst })
            {
                if (mode is null || mode.Length is < 1 or > 4) throw new ArgumentException("Each mode needs one to four interpretations.");
                foreach (var claim in mode)
                {
                    if (claim is null || string.IsNullOrWhiteSpace(claim.Text) || claim.Text.Length > 280 ||
                        claim.Text.Any(c => char.IsDigit(c) || char.IsControl(c) || c is '<' or '>' or '`') ||
                        new[] { "xg", "pressure", "formation", "certainly", "guaranteed", "will", "eventually" }
                            .Any(word => claim.Text.Split([' ', '.', ',', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
                                .Contains(word, StringComparer.OrdinalIgnoreCase)))
                        throw new ArgumentException("Interpretations must be bounded plain text, without numerical or unsupported metric claims.");
                    if (claim.EvidenceIds is null || claim.EvidenceIds.Length is < 1 or > 4 ||
                        claim.EvidenceIds.Distinct().Count() != claim.EvidenceIds.Length)
                        throw new ArgumentException("Each interpretation needs distinct observed evidence references.");
                    foreach (var id in claim.EvidenceIds) _ = Resolve(id);
                }
                if (!mode.Any(c => c.EvidenceIds.Contains(FocusId))) throw new ArgumentException("Both modes must address the selected moment.");
            }
            return answer;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        { throw new ScenarioValidationException($"Invalid moment explanation: {e.Message}", e); }
    }
}
