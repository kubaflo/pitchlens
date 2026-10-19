using PitchLens.Core;
using PitchLens.Generation;
using GitHub.Copilot;

if (args is ["--author-paths", var authoredRuntime, var authoredOutput])
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    try
    {
        var authoredSource = new CopilotScenarioSource(authoredRuntime, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(authoredOutput))!, "authored-work"));
        var json = await authoredSource.GeneratePlanAsync(new Progress<string>(Console.WriteLine), deadline.Token);
        await File.WriteAllTextAsync(authoredOutput + ".unvalidated.json", json, deadline.Token);
        var match = AuthoredMatchPlan.Parse(json, requireCommentary: false);
        await File.WriteAllTextAsync(authoredOutput, json, deadline.Token);
        Console.WriteLine($"LIVE AUTHORED PATHS VERIFIED | model={CopilotScenarioSource.Model} | players={match.Players.Count} | knots={match.Players.Sum(p => p.Path.Frames.Count)} | events={match.Events.Count}");
        PrintStory(match);
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or OperationCanceledException or IOException)
    { Console.Error.WriteLine($"AUTHORED PATHS NOT VERIFIED: {e.Message}"); return 1; }
}
if (args is ["--validate-commentary", var storedPlan, var storedCommentary])
{
    try
    {
        var match = StoredMatch(await File.ReadAllTextAsync(storedPlan));
        var validated = CommentaryPrompt.Attach(await File.ReadAllTextAsync(storedCommentary), match);
        Console.WriteLine($"VALIDATED COMMENTARY | cues={validated.Commentary!.Cues.Count} | model={validated.Commentary.Model} | factsHash={CommentaryPrompt.Hash(match)}");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or IOException or UnauthorizedAccessException)
    { Console.Error.WriteLine($"COMMENTARY INVALID: {e.Message}"); return 1; }
}
if (args is ["--commentary", var commentaryRuntime, var matchPath, var commentaryOutput])
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    try
    {
        var match = StoredMatch(await File.ReadAllTextAsync(matchPath));
        var commentarySource = new CopilotScenarioSource(commentaryRuntime, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(commentaryOutput))!, "commentary-work"));
        var json = await commentarySource.GenerateCommentaryAsync(match, new Progress<string>(Console.WriteLine), deadline.Token);
        await File.WriteAllTextAsync(commentaryOutput + ".unvalidated.json", json, deadline.Token);
        var validated = CommentaryPrompt.Attach(json, match);
        await File.WriteAllTextAsync(commentaryOutput, json, deadline.Token);
        Console.WriteLine($"LIVE COMMENTARY VERIFIED | model={CopilotScenarioSource.Model} | cues={validated.Commentary!.Cues.Count} | scenarioHash={CommentaryPrompt.Hash(match)}");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or OperationCanceledException or IOException)
    { Console.Error.WriteLine($"LIVE COMMENTARY NOT VERIFIED: {e.Message}"); return 1; }
}
if (args is ["--demo"])
{
    Console.WriteLine("Historical authored 6v6 demo, not SDK output and not the active 11v11 startup.");
    PrintStory(FiveMinuteDemo.Create());
    return 0;
}
if (args is ["--check-model", var runtime, var modelId])
{
    using var check = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    try
    {
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(runtime),
            UseLoggedInUser = true,
            LogLevel = CopilotLogLevel.Error
        });
        await client.StartAsync(check.Token);
        var auth = await client.GetAuthStatusAsync(check.Token);
        if (!auth.IsAuthenticated) throw new CopilotGenerationException("Normal Copilot login is not authenticated.");
        var models = await client.ListModelsAsync(check.Token);
        var model = models.SingleOrDefault(m => m.Id == modelId)
            ?? throw new CopilotGenerationException($"Exact model {modelId} is not exposed by SDK ListModels.");
        Console.WriteLine($"MODEL PREFLIGHT | id={model.Id} | policy={model.Policy?.State ?? "unspecified"} | reasoning={model.Capabilities.Supports.ReasoningEffort}");
        return model.Policy?.State == "disabled" ? 1 : 0;
    }
    catch (Exception e) when (e is CopilotGenerationException or IOException or InvalidOperationException or OperationCanceledException)
    {
        Console.Error.WriteLine($"MODEL PREFLIGHT FAILED: {e.Message}");
        return 1;
    }
}

if (args is ["--validate", var artifact])
{
    try
    {
        var json = await File.ReadAllTextAsync(artifact);
        using var shape = System.Text.Json.JsonDocument.Parse(json);
        var scenario = StoredMatch(json);
        Console.WriteLine($"VALIDATED STORED SCENARIO | duration={scenario.Duration} | players={scenario.Players.Count} | events={scenario.Events.Count}");
        PrintStory(scenario);
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"SCENARIO INVALID: {e.Message}");
        return 1;
    }
}
if (args is not ["--live", var cli, var output] || !Path.IsPathFullyQualified(output))
{
    Console.WriteLine("Two bounded SDK stages, no automatic retries: --live /absolute/path/to/copilot /absolute/output/scenario.json");
    Console.WriteLine("One explicit-path stage only: --author-paths /path/to/runtime /absolute/output/scenario.json");
    Console.WriteLine("Validate existing JSON without AI/network: --validate /path/to/scenario.json");
    Console.WriteLine("Check SDK model availability without generation: --check-model /path/to/runtime exact-model-id");
    Console.WriteLine("Validate recorded captions without AI: --validate-commentary /path/to/plan.json /path/to/commentary.json");
    Console.WriteLine("One paid commentary request: --commentary /path/to/runtime /path/to/plan.json /absolute/output/commentary.json");
    return 2;
}
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var progress = new Progress<string>(message => Console.WriteLine(message));
var source = new CopilotScenarioSource(cli, Path.Combine(Path.GetDirectoryName(output)!, "copilot-work"));
try
{
    var json = await source.GenerateAsync(progress, cancellation.Token);
    await File.WriteAllTextAsync(output + ".unvalidated.json", json, cancellation.Token);
    var match = AuthoredMatchPlan.Parse(json);
    await File.WriteAllTextAsync(output, json, cancellation.Token);
    var snapshot = Replay.At(match, match.Duration);
    Console.WriteLine($"LIVE SDK VERIFIED | model={CopilotScenarioSource.Model} | duration={match.Duration} | players={match.Players.Count} | events={match.Events.Count}");
    Console.WriteLine($"Totals: passes={snapshot.Players.Sum(p => p.Passes)}, carries={match.Events.Count(e => e.Kind == EventKind.Carry)}, shots={snapshot.Players.Sum(p => p.Shots)}, goals={snapshot.Players.Sum(p => p.Goals)}");
    Console.WriteLine($"Max player speed={snapshot.Players.Max(p => p.MaxSpeed):F3} m/s. Synthetic events only. Saved validated JSON to {output}");
    PrintStory(match);
    return 0;
}
catch (Exception e) when (e is CopilotGenerationException or ScenarioValidationException or OperationCanceledException or IOException)
{
    Console.Error.WriteLine($"LIVE SDK NOT VERIFIED: {e.GetType().Name}: {e.Message}");
    if (e.InnerException is System.Text.Json.JsonException jsonError)
        Console.Error.WriteLine($"SDK JSON compatibility diagnostic: {jsonError.Message}");
    return 1;
}

static Match StoredMatch(string json)
{
    using var shape = System.Text.Json.JsonDocument.Parse(json);
    return shape.RootElement.TryGetProperty("phaseKinds", out _) ? AuthoredMatchPlan.Parse(json, requireCommentary: false) :
        shape.RootElement.TryGetProperty("phases", out _) ? FiveMinutePlan.Parse(json, requireCommentary: false) : ScenarioValidator.Parse(json);
}

static void PrintStory(Match match)
{
    Console.WriteLine($"Story: {match.Story?.Title}");
    foreach (var beat in match.Story?.Beats ?? [])
        Console.WriteLine($"  {beat.StartTime:0.#}-{beat.EndTime:0.#}s {beat.Kind}: {string.Join(",", beat.EventIds)}");
    foreach (var team in match.Teams)
    {
        var minimumMoving = Enumerable.Range(0, (int)((match.Duration - 20) / 10) + 1).Min(n =>
            match.Players.Count(p => p.TeamId == team.Id && !StoryQuality.IsKeeper(p) &&
                p.Path.Measure(n * 10 + 20).Distance - p.Path.Measure(n * 10).Distance >= 8));
        Console.WriteLine($"{team.Name}: minimum moving outfielders per20s={minimumMoving}/{match.Players.Count(p => p.TeamId == team.Id && !StoryQuality.IsKeeper(p))}");
        foreach (var player in match.Players.Where(p => p.TeamId == team.Id))
        {
            var motion = player.Path.Measure(match.Duration);
            Console.WriteLine($"  {player.Name}: {motion.Distance:F1}m, {motion.MaxSpeed:F2}m/s, {player.Path.Frames.Count} frames");
        }
    }
}
