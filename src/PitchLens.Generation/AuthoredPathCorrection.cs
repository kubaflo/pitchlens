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
[JsonSerializable(typeof(AuthoredMotionIssue[]))]
[JsonSerializable(typeof(AuthoredQualityIssue[]))]
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
        Minimum outfield width30m/depth22m applies throughout linear interpolation,
        NOT only at individual player's knots. Leave a small safe depth/width margin across these intervals, not equality-rounding tricks.
        Correct ALL listed failures together. You author every modified numerical knot. Restoring passing formation is not permission
        to break keeper ends, speed<=8m/s, acceleration<=2.5m/s², running bursts, carry distance, centre restarts, receiver onside,
        crowding, off-ball movement or event causality. The FULL unchanged validator will run again.
        Events cannot be edited and all actors/receivers/times/outcomes stay exactly as below; coordinate the paths with them.
        This is the ONLY correction attempt; no tools, extra requests or markdown.
        Measured failure evidence:
        """ + (failure.MotionIssues.Count>0?
            """
            The listed actual AI-authored segments violate speed<=8m/s or discrete velocity-change/mean-adjacent-duration<=2.5m/s².
            Each issue includes its player, exact knot time, measured value, unchanged maximum and ORIGINAL adjacent knots.
            Smooth the explicit timings/coordinates through these knots with reachable easing; no local app solver will fix them.
            Keep every event and phase immutable and preserve all other full shape/physics constraints. No threshold relaxation.
            """+JsonSerializer.Serialize(failure.MotionIssues.ToArray(),CorrectionJsonContext.Default.AuthoredMotionIssueArray):"")
        + "\nOutfield width/depth failures at one-second samples:\n"+
            JsonSerializer.Serialize(failure.Issues.ToArray(),CorrectionJsonContext.Default.AuthoredShapeIssueArray)
        + """

        Measured path-quality failures (all limits unchanged):
        visible-movement windows are [start,end], checked at starts0,10,...280, each lasting20s.
        Count only outfield players whose cumulative segment travel within that exact window is >=8m,
        including partial segments: >=6 TOTAL and >=2 from EACH club. The two club quotas alone are NOT enough.
        PlayerDistances are observations, not a requirement for all20 players to qualify. Keep every keeper at its own end.
        For other issues, each measure states its exact bound/unit. 'strict maximum' means <; 'strict minimum' means >.
        Preserve carry>=3m, reachable centre restarts, onside at release and ballflight<=40m/s without changing any event.
        """
        +JsonSerializer.Serialize(failure.QualityIssues.ToArray(),CorrectionJsonContext.Default.AuthoredQualityIssueArray)
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
    public static async Task<(AuthoredMatchDocument Document,Match Match)> CorrectAsync(AuthoredMatchDocument document,
        AuthoredPathValidationException failure,
        Func<AuthoredMatchDocument,AuthoredPathValidationException,CancellationToken,Task<string>> refine,
        CancellationToken token,GenerationResponseArchive? archive=null,MatchFixture? fixture=null)
    {
        var immutable=JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument);
        var originalIds=document.Players.Select(p=>p.Id).Order(StringComparer.Ordinal).ToArray();
        string correction;
        try{correction=await refine(document,failure,token).ConfigureAwait(false);}
        catch(Exception error) when(error is ScenarioValidationException or CopilotGenerationException or IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {error.Data["PitchLens.ValidationStage"]="path-correction-request";throw;}
        token.ThrowIfCancellationRequested();
        try
        {
            if(JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument)!=immutable)
                throw new ScenarioValidationException("AI correction changed immutable scenario/event/time metadata.");
            var corrected=Apply(document,correction);
            if(corrected.Players is null||corrected.Players.Any(p=>p is null)||
                !corrected.Players.Select(p=>p.Id).Order(StringComparer.Ordinal).SequenceEqual(originalIds))
                throw new ScenarioValidationException("AI correction changed immutable event/time metadata.");
            if(archive is not null)
                await archive.SaveAsync("corrected",JsonSerializer.Serialize(corrected,AuthoredJsonContext.Default.AuthoredMatchDocument)).ConfigureAwait(false);
            return (corrected,AuthoredMatchPlan.Build(corrected,requireCommentary:false,expectedFixture:fixture));
        }
        catch(ScenarioValidationException error){error.Data["PitchLens.ValidationStage"]="after-path-correction";throw;}
    }
}

public static class AuthoredGenerationPipeline
{
    public static async Task<string> RunAsync(
        Func<CancellationToken, Task<string>> generatePaths,
        Func<AuthoredMatchDocument, AuthoredPathValidationException, CancellationToken, Task<string>> refinePaths,
        Func<Match, CancellationToken, Task<string>> generateCommentary,
        IProgress<string>? progress, CancellationToken token, GenerationResponseArchive? archive = null, MatchFixture? fixture = null,
        Func<AuthoredMatchDocument, AuthoredPhaseValidationException, CancellationToken, Task<string>>? refinePhases = null)
    {
        var raw = await generatePaths(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        AuthoredMatchDocument document;
        try{document=AuthoredMatchPlan.Document(raw);}
        catch(ScenarioValidationException error){error.Data["PitchLens.ValidationStage"]="initial";throw;}
        if(fixture is not null)
        {
            FixtureCatalog.RequireKnown(fixture);
            if(document.Fixture is not null) FixtureCatalog.RequireKnown(document.Fixture);
            if(document.Fixture is not null && (document.Fixture.Id!=fixture.Id || !document.Fixture.Players.SequenceEqual(fixture.Players)))
                throw new ScenarioValidationException("AI changed immutable captured fixture metadata.");
            document=document with { Fixture=fixture };
        }
        Match match;
        progress?.Report("Validating all 22 AI-authored paths and events…");
        try { match = AuthoredMatchPlan.Build(document, requireCommentary: false,expectedFixture:fixture); }
        catch(AuthoredPhaseValidationException failure) when(refinePhases is not null)
        {
            progress?.Report("AI correcting unsupported phase labels (one shared correction; paths and events unchanged)…");
            var immutable=JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument);
            string correction;
            try{correction=await refinePhases(document,failure,token).ConfigureAwait(false);}
            catch(Exception error) when(error is ScenarioValidationException or CopilotGenerationException or IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {error.Data["PitchLens.ValidationStage"]="phase-correction-request";throw;}
            token.ThrowIfCancellationRequested();
            try
            {
                if(JsonSerializer.Serialize(document,AuthoredJsonContext.Default.AuthoredMatchDocument)!=immutable)
                    throw new ScenarioValidationException("AI phase-correction adapter changed immutable scenario metadata.");
                var corrected=AuthoredPhaseRefinement.Apply(document,correction);
                if(archive is not null)
                    await archive.SaveAsync("corrected",JsonSerializer.Serialize(corrected,AuthoredJsonContext.Default.AuthoredMatchDocument)).ConfigureAwait(false);
                document=corrected;
                // One shared repair slot: any remaining phase/shape/physics error is terminal, not another catch/retry.
                match=AuthoredMatchPlan.Build(document,requireCommentary:false,expectedFixture:fixture);
            }
            catch(ScenarioValidationException error){error.Data["PitchLens.ValidationStage"]="after-phase-correction";throw;}
        }
        catch (AuthoredPathValidationException failure)
        {
            progress?.Report(failure.MotionIssues.Count>0?
                "AI correcting measured player speed/velocity changes (one shared correction; events and phases unchanged)…":
                failure.QualityIssues.Count>0?
                "AI refining measured activity/path-quality failures (one shared correction; events and phases unchanged)…":
                "AI refining rejected player paths (one bounded correction; events unchanged)…");
            (document,match)=await AuthoredPathRefinement.CorrectAsync(document,failure,refinePaths,token,archive,fixture).ConfigureAwait(false);
        }
        catch(ScenarioValidationException error){error.Data["PitchLens.ValidationStage"]="initial";throw;}
        token.ThrowIfCancellationRequested();
        progress?.Report("Creating contextual narration and Fan/Analyst story for the validated match…");
        var commentary = await generateCommentary(match, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var package = NarrationPrompt.Document(commentary);
        if(package.CaptionContract!=2)throw new ScenarioValidationException("New generation requires dense caption contract2, not a historical package.");
        _ = NarrationPrompt.Attach(package, match);
        var result = JsonSerializer.Serialize(document with { Commentary = null, Narration = package },
            AuthoredJsonContext.Default.AuthoredMatchDocument);
        if (archive is not null) await archive.SaveAsync("validated", result).ConfigureAwait(false);
        return result;
    }
}
