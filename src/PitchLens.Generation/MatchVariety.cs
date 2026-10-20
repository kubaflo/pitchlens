using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record GameplaySignature(string Hash, string[] Actions, int[] PositionCells, string Summary);
public sealed record VarietyRequest(string RequestId, string Direction, GameplaySignature[] Recent)
{
    public string Instructions => $"""
        FRESH MATCH REQUEST {RequestId}. Editorial direction: {Direction}.
        Invent a genuinely different football game, not renamed prior actions or cosmetic knot changes.
        Choose your own protagonists, pass/carry routes, tempo, possession rhythm, goal timings/scorers/counts and result.
        At least ONE goal TOTAL; either club may score none. Do not force a draw, both clubs scoring, or a particular winner.
        Avoid the recorded example's goals at86/176 and mirrored one-goal-each structure.
        Prior safe gameplay summaries to avoid (not a template to copy):
        {string.Join("\n", Recent.Select(s => s.Summary))}
        No code/local random offsets will invent or repair your paths. Every final event and coordinate must be AI-authored.
        """;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(GameplaySignature[]))]
public partial class VarietyJsonContext : JsonSerializerContext;

public sealed class MatchVariety
{
    private readonly List<GameplaySignature> recent = [];
    private readonly Action<IReadOnlyList<GameplaySignature>>? save;
    private int direction = RandomNumberGenerator.GetInt32(Directions.Length);
    private static readonly string[] Directions =
    [
        "Build patient central combinations and a few sudden diagonal switches; let the result emerge from different finishers.",
        "Use wing-led progression and recovery runs, asymmetrical possessions and a different opening owner; avoid mirrored scoring.",
        "Use varied short/long passing chains, early turnovers and late sustained attacks; choose non-template scorer and goal times.",
        "Contrast calm recycling with quick vertical carries and support runs; let one club finish more clinically without forcing equal goals."
    ];
    public IReadOnlyList<GameplaySignature> Recent => recent.AsReadOnly();
    public MatchVariety(IEnumerable<GameplaySignature>? history = null, Action<IReadOnlyList<GameplaySignature>>? save = null)
    {
        this.save = save;
        if (history is not null)
        {
            var entries = history.ToArray();
            if (entries.Length > 8 || entries.Any(s => s is null || s.Hash is not { Length: 64 } ||
                s.Actions is not { Length: >= 1 and <= 90 } || s.Actions.Any(a => a is null || a.Length > 300) ||
                s.PositionCells is not { Length: 1364 } || s.Summary is not { Length: >= 1 and <= 2000 }))
                throw new InvalidOperationException("Recent gameplay metadata is invalid.");
            recent.AddRange(entries);
        }
        var baseline = Signature(AuthoredMatchRecording.Create());
        if (!recent.Any(s => s.Hash == baseline.Hash)) recent.Insert(0, baseline);
        if (recent.Count > 8) recent.RemoveRange(1, recent.Count - 8);
    }
    public VarietyRequest Next() => new(Guid.NewGuid().ToString("N"), Directions[direction++ % Directions.Length], recent.TakeLast(8).ToArray());
    public static GameplaySignature Signature(Match match)
    {
        var actions = match.Events.Select(e => FormattableString.Invariant($"{e.Kind}|{e.ActorId}|{e.ReceiverId}|{e.Completed}|{Math.Round(e.Time / 3)}|{(e.StartTime.HasValue ? Math.Round(e.StartTime.Value / 3) : -1)}")).ToArray();
        var cells = new List<int>();
        foreach (var player in match.Players.OrderBy(p => p.Id, StringComparer.Ordinal))
            for (double t = 0; t <= match.Duration; t += 10)
            { var point = player.Path.At(t); cells.Add((int)Math.Floor(point.X / 5)); cells.Add((int)Math.Floor(point.Y / 5)); }
        var exact = string.Join("\n", match.Events.Select(e => FormattableString.Invariant($"{e.Kind}|{e.Time:R}|{e.Order}|{e.ActorId}|{e.ReceiverId}|{e.Completed}|{e.StartTime:R}"))) +
            "\n" + string.Join("\n", match.Players.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p =>
                p.Id + ":" + string.Join(";", p.Path.Frames.Select(f => FormattableString.Invariant($"{f.Time:R},{f.Position.X:R},{f.Position.Y:R}")))));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(exact)));
        var goals = match.Events.Where(e => e.Kind == EventKind.Goal).Select(e => $"{match.Player(e.ActorId).TeamId}/{e.ActorId}@{e.Time:0.#}s");
        var busiest = match.Events.Where(e => e.Kind == EventKind.Pass).GroupBy(e => e.ActorId)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(3).Select(g => g.Key);
        var summary = $"Goals: {string.Join(", ", goals)}. {match.Events.Count} actions; {match.Events.Count(e => e.Kind == EventKind.Pass)} passes; " +
            $"{match.Events.Count(e => e.Kind == EventKind.Carry)} carries. Main distributors: {string.Join(", ", busiest)}.";
        return new(hash, actions, cells.ToArray(), summary);
    }
    public static bool NearDuplicate(GameplaySignature a, GameplaySignature b)
    {
        if (a.Hash == b.Hash) return true;
        if (Math.Abs(a.Actions.Length - b.Actions.Length) > Math.Max(a.Actions.Length, b.Actions.Length) * .1) return false;
        var previous = new int[b.Actions.Length + 1];
        foreach (var action in a.Actions)
        {
            var next = new int[b.Actions.Length + 1];
            for (var j = 1; j <= b.Actions.Length; j++)
                next[j] = action == b.Actions[j - 1] ? previous[j - 1] + 1 : Math.Max(previous[j], next[j - 1]);
            previous = next;
        }
        var actionSimilarity = previous[^1] / (double)Math.Max(a.Actions.Length, b.Actions.Length);
        var sameCells = a.PositionCells.Length == b.PositionCells.Length && a.PositionCells.Length > 0
            ? a.PositionCells.Zip(b.PositionCells).Count(p => Math.Abs(p.First - p.Second) <= 1) / (double)a.PositionCells.Length : 0;
        return actionSimilarity >= .85 && sameCells >= .9;
    }
    public void Accept(Match match)
    {
        var candidate = Signature(match);
        if (recent.Any(p => NearDuplicate(p, candidate)))
            throw new ScenarioValidationException("AI repeated an identical or near-identical recent game. Previous match kept; no automatic retry.");
        var next = recent.Append(candidate).TakeLast(8).ToArray();
        save?.Invoke(next);
        recent.Clear(); recent.AddRange(next);
    }
}
