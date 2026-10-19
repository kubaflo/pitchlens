using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record AuthoredPathCorrection([property: JsonRequired] AuthoredPlayer[] Players);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AuthoredPathCorrection))]
[JsonSerializable(typeof(AuthoredShapeIssue[]))]
public partial class CorrectionJsonContext : JsonSerializerContext;

public static class AuthoredPathRefinement
{
    public static string Schema
    {
        get
        {
            var original = JsonNode.Parse(AuthoredMatchPrompt.Schema)!;
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("players"),
                ["properties"] = new JsonObject { ["players"] = original["properties"]!["players"]!.DeepClone() }
            }.ToJsonString();
        }
    }
    public static string Prompt(AuthoredMatchDocument original, AuthoredPathValidationException failure) =>
        """
        Refine this rejected synthetic football geometry. SAME22 IDs,300s and EXACT existing events are IMMUTABLE.
        Return ONLY {"players":[...all22...]} with complete explicit AI-authored [time,x,y] trajectories,31..81knots each.
        No events/title/duration/commentary fields. No seed/solver. Do not move unrelated knots unnecessarily.
        Original failed at the listed one-second samples. Minimum outfield width30m/depth22m applies throughout linear interpolation,
        NOT only at individual player's knots. Leave a small safe depth/width margin across these intervals, not equality-rounding tricks.
        Correct ALL listed failures together. You author every modified numerical knot. Restoring passing formation is not permission
        to break keeper ends, speed<=8m/s, acceleration<=2.5m/s², running bursts, carry distance, centre restarts, receiver onside,
        crowding, off-ball movement or event causality. The FULL unchanged validator will run again.
        Events cannot be edited and all actors/receivers/times/outcomes stay exactly as below; coordinate the paths with them.
        This is the ONLY correction attempt; no tools, extra requests or markdown.
        Failure evidence:
        """ + JsonSerializer.Serialize(failure.Issues.ToArray(), CorrectionJsonContext.Default.AuthoredShapeIssueArray)
        + "\nOriginal safe synthetic scenario:\n" + JsonSerializer.Serialize(original with { Commentary = null }, AuthoredJsonContext.Default.AuthoredMatchDocument);

    public static AuthoredMatchDocument Apply(AuthoredMatchDocument original, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 200_000) throw new ScenarioValidationException("Empty or oversized AI path correction.");
        try
        {
            var options = new JsonSerializerOptions(CorrectionJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 24 };
            var correction = JsonSerializer.Deserialize(json, new CorrectionJsonContext(options).AuthoredPathCorrection)
                ?? throw new ScenarioValidationException("Null AI path correction.");
            // Import only explicit model-authored paths; event/roster/time metadata cannot be patched by this response.
            return original with { Players = correction.Players };
        }
        catch (JsonException e) { throw new ScenarioValidationException("Invalid AI path correction JSON.", e); }
    }
}

public static class AuthoredGenerationPipeline
{
    public static async Task<string> RunAsync(
        Func<CancellationToken, Task<string>> generatePaths,
        Func<AuthoredMatchDocument, AuthoredPathValidationException, CancellationToken, Task<string>> refinePaths,
        Func<Match, CancellationToken, Task<string>> generateCommentary,
        IProgress<string>? progress, CancellationToken token, GenerationResponseArchive? archive = null)
    {
        var raw = await generatePaths(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var document = AuthoredMatchPlan.Document(raw);
        Match match;
        progress?.Report("Validating all 22 AI-authored paths and events…");
        try { match = AuthoredMatchPlan.Build(document, requireCommentary: false); }
        catch (AuthoredPathValidationException failure)
        {
            progress?.Report("AI refining rejected player paths (one bounded correction; events unchanged)…");
            var originalEvents = document.Events.ToArray();
            var originalPhases = document.PhaseKinds.ToArray();
            var originalIds = document.Players.Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
            var duration = document.Duration;
            var owner = document.InitialOwnerId;
            var title = document.Title;
            var correction = await refinePaths(document, failure, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var corrected = AuthoredPathRefinement.Apply(document, correction);
            if (!corrected.Events.SequenceEqual(originalEvents) || corrected.Duration != duration ||
                corrected.InitialOwnerId != owner || corrected.Title != title || !corrected.PhaseKinds.SequenceEqual(originalPhases) ||
                corrected.Players is null || corrected.Players.Any(p => p is null) ||
                !corrected.Players.Select(p => p.Id).Order(StringComparer.Ordinal).SequenceEqual(originalIds))
                throw new ScenarioValidationException("AI correction changed immutable event/time metadata.");
            if (archive is not null)
                await archive.SaveAsync("corrected", JsonSerializer.Serialize(corrected, AuthoredJsonContext.Default.AuthoredMatchDocument)).ConfigureAwait(false);
            document = corrected;
            match = AuthoredMatchPlan.Build(document, requireCommentary: false); // Full validation, no second correction.
        }
        token.ThrowIfCancellationRequested();
        progress?.Report("Generating AI commentator subtitles for the fully validated match…");
        var commentary = await generateCommentary(match, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var narrated = CommentaryPrompt.Attach(commentary, match);
        var result = JsonSerializer.Serialize(document with { Commentary = narrated.Commentary!.Cues.ToArray() },
            AuthoredJsonContext.Default.AuthoredMatchDocument);
        if (archive is not null) await archive.SaveAsync("validated", result).ConfigureAwait(false);
        return result;
    }
}
