using PitchLens.Core;
using PitchLens.Generation;
using GitHub.Copilot;

if (args is ["--variety-live", var varietyRuntime, var varietyWork, var varietyWitness])
{
    Directory.CreateDirectory(varietyWitness);
    var initial=NarrationRecording.Create();
    var capture=new FullRequestCapture(new CopilotScenarioSource(varietyRuntime,varietyWork,Path.Combine(varietyWitness,"raw-responses")));
    var service=new GenerationService(capture,initial,ScenarioOrigin.RecordedSdkExample,CopilotScenarioSource.Model,new MatchVariety());
    await service.GenerateAsync(new Progress<string>(Console.WriteLine),CancellationToken.None);
    if(service.State!=GenerationState.Succeeded)
    { Console.Error.WriteLine($"VARIETY FULL REQUEST NOT VERIFIED: {service.Status}");return 1; }
    var varietyOutput=Path.Combine(varietyWitness,"validated-match.json");
    await File.WriteAllTextAsync(varietyOutput,capture.Response!);
    if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(varietyOutput,UnixFileMode.UserRead|UnixFileMode.UserWrite);
    var match=service.Current;var score=Replay.At(match,300).Score;
    var signature=MatchVariety.Signature(match);
    Console.WriteLine($"VARIETY FULL REQUEST VERIFIED | fixture={match.Teams[0].Name} v {match.Teams[1].Name} | model={service.CurrentModel} | players={match.Players.Count} | knots={match.Players.Sum(p=>p.Path.Frames.Count)} | events={match.Events.Count} | score={score[match.Teams[0].Id]}:{score[match.Teams[1].Id]} | cues={match.Commentary!.Cues.Count} | chapters={match.Narrative!.Chapters.Count} | gameplayHash={signature.Hash}");
    return 0;
}
if (args is ["--director", var directorRuntime, var directorWork, var directorWitness])
{
    var observed = DirectorEvidence.Capture(NarrationRecording.Create(), 1, 300);
    Directory.CreateDirectory(directorWitness);
    async Task SaveDirectorWitness(string name, string content)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(Path.Combine(directorWitness, name), options);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }
    await SaveDirectorWitness("request.txt", DirectorPrompt.For(observed));
    await SaveDirectorWitness("facts.json", observed.Json);
    await SaveDirectorWitness("schema.json", DirectorPrompt.Schema);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(270));
    try
    {
        var directorSource = new CopilotScenarioSource(directorRuntime, directorWork, Path.Combine(directorWitness, "raw-responses"));
        var raw = await directorSource.GenerateDirectorAsync(observed, new Progress<string>(Console.WriteLine), deadline.Token);
        await SaveDirectorWitness("response.json", raw);
        var plan = observed.Validate(raw);
        await SaveDirectorWitness("validated.json", raw);
        Console.WriteLine($"LIVE DIRECTOR VALIDATED | model={CopilotScenarioSource.Model} | factsHash={observed.Hash} | cutoff={observed.Cutoff} | scenes={plan.Scenes.Length} | wallSeconds={FilmTimeline.Duration(plan)} | SDK Send requests=1 | match plan requests=0");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or OperationCanceledException or IOException)
    { Console.Error.WriteLine($"DIRECTOR NOT VERIFIED: {e.Message}. No retry or repair."); return 1; }
}
if (args is ["--narration", var narrationRuntime, var narrationWork, var narrationWitness])
{
    var immutableMatch = AuthoredMatchRecording.Create();
    Directory.CreateDirectory(narrationWitness);
    async Task SaveNarrationWitness(string name, string text)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(Path.Combine(narrationWitness, name), options);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }
    await SaveNarrationWitness("request.txt", DenseNarrationPrompt.For(immutableMatch));
    await SaveNarrationWitness("facts.json", NarrationPrompt.Facts(immutableMatch));
    await SaveNarrationWitness("schema.json", DenseNarrationPrompt.Schema);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(270));
    try
    {
        var narrationSource = new CopilotScenarioSource(narrationRuntime, narrationWork, Path.Combine(narrationWitness, "raw-responses"));
        var raw = await narrationSource.GenerateNarrationAsync(immutableMatch, new Progress<string>(Console.WriteLine), deadline.Token);
        await SaveNarrationWitness("response.json", raw);
        var validated = NarrationPrompt.Attach(raw, immutableMatch);
        await SaveNarrationWitness("validated.json", raw);
        var captionStats=CaptionSchedule.Measure(validated.Commentary!.Cues.Select(c=>new CaptionSlot(c.EventId,c.Start,c.End)),validated.Duration);
        Console.WriteLine($"LIVE NARRATION VALIDATED | model={CopilotScenarioSource.Model} | factsHash={validated.Narrative!.FactsHash} | cues={validated.Commentary.Cues.Count} | chapters={validated.Narrative.Chapters.Count} | coverageSeconds={captionStats.Seconds} | fraction={captionStats.Fraction} | maxInteriorGap={captionStats.MaximumInteriorGap} | SDK Send requests=1 | match plan requests=0");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or OperationCanceledException or IOException)
    { Console.Error.WriteLine($"LIVE NARRATION NOT VERIFIED: {e.Message}. No automatic retry or repair."); return 1; }
}
if (args is ["--explain-live", var explainRuntime, var explainDirectory, var explainWitness])
{
    var match = AuthoredMatchRecording.Create();
    var goal = match.Events.First(e => e.Kind == EventKind.Goal);
    var evidence = MomentEvidence.Capture(match, 1, goal.Time, goal.Id);
    var momentSource = new CopilotMomentSource(explainRuntime, explainDirectory, explainWitness);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(270));
    try
    {
        var raw = await momentSource.ExplainAsync(evidence, deadline.Token);
        var answer = evidence.Validate(raw);
        Console.WriteLine($"LIVE EXPLANATION VALIDATED | model={CopilotScenarioSource.Model} | cutoff={evidence.Cutoff} | hash={evidence.Hash} | tools={momentSource.ToolCalls} | fan={answer.Fan.Length} | analyst={answer.Analyst.Length}");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or OperationCanceledException or IOException)
    { Console.Error.WriteLine($"LIVE EXPLANATION NOT VERIFIED: {e.Message} | tools={momentSource.ToolCalls}"); return 1; }
}
string? fullWorkingDirectory = null;
string? preservedPathsFile = null;
if (args is ["--live-at", var fullRuntimePath, var fullDirectory, var fullOutput])
{
    fullWorkingDirectory = fullDirectory;
    args = ["--live", fullRuntimePath, fullOutput];
}
if (args is ["--resume-live", var resumeRuntime, var resumeDirectory, var resumePaths, var resumeOutput])
{
    fullWorkingDirectory = resumeDirectory;
    preservedPathsFile = resumePaths;
    args = ["--live", resumeRuntime, resumeOutput];
}
if (args is [var connectionOperation, var connectionRuntime, var connectionDirectory] &&
    connectionOperation is "--check-connection" or "--check-session")
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    try
    {
        await new CopilotScenarioSource(connectionRuntime, connectionDirectory).VerifyConnectionAsync(new Progress<string>(Console.WriteLine), deadline.Token, connectionOperation == "--check-session");
        Console.WriteLine("CONNECTION VERIFIED | authenticated | gpt-6.1-sol enabled | no generation");
        return 0;
    }
    catch (CopilotGenerationException e) { Console.Error.WriteLine(e.Message); Console.Error.WriteLine(e.Diagnostic); return 1; }
    catch (OperationCanceledException) { Console.Error.WriteLine("Connection check timed out."); return 1; }
}
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
    Console.WriteLine("Paths + at most one targeted AI correction + captions (15min total): --live /absolute/path/to/copilot /absolute/output/scenario.json");
    Console.WriteLine("Use a native workdir: --live-at /runtime /workdir /output.json");
    Console.WriteLine("Resume a saved semantic failure without another initial request: --resume-live /runtime /workdir /paths.json /output.json");
    Console.WriteLine("One explicit-path stage only: --author-paths /path/to/runtime /absolute/output/scenario.json");
    Console.WriteLine("Validate existing JSON without AI/network: --validate /path/to/scenario.json");
    Console.WriteLine("Check SDK model availability without generation: --check-model /path/to/runtime exact-model-id");
    Console.WriteLine("Validate recorded captions without AI: --validate-commentary /path/to/plan.json /path/to/commentary.json");
    Console.WriteLine("One paid commentary request: --commentary /path/to/runtime /path/to/plan.json /absolute/output/commentary.json");
    return 2;
}
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var progress = new Progress<string>(message => Console.WriteLine(message));
var source = new CopilotScenarioSource(cli, fullWorkingDirectory ?? Path.Combine(Path.GetDirectoryName(output)!, "copilot-work"),
    Path.Combine(Path.GetDirectoryName(output)!, "raw-responses"));
try
{
    var captured = new FullRequestCapture(source, preservedPathsFile is null ? null : await File.ReadAllTextAsync(preservedPathsFile));
    var service = new GenerationService(captured, AuthoredMatchRecording.Create(), ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
    await service.GenerateAsync(progress, cancellation.Token);
    if (service.State != GenerationState.Succeeded)
    {
        Console.Error.WriteLine($"FULL SDK PIPELINE FAILED | state={service.State} | {service.Status}");
        return 1;
    }
    var json = captured.Response ?? throw new InvalidOperationException("Successful service did not return its generated JSON.");
    await File.WriteAllTextAsync(output + ".unvalidated.json", json, cancellation.Token);
    var match = service.Current;
    await File.WriteAllTextAsync(output, json, cancellation.Token);
    var snapshot = Replay.At(match, match.Duration);
    Console.WriteLine($"LIVE SDK VERIFIED | model={CopilotScenarioSource.Model} | duration={match.Duration} | players={match.Players.Count} | events={match.Events.Count}");
    Console.WriteLine($"Totals: passes={snapshot.Players.Sum(p => p.Passes)}, carries={match.Events.Count(e => e.Kind == EventKind.Carry)}, shots={snapshot.Players.Sum(p => p.Shots)}, goals={snapshot.Players.Sum(p => p.Goals)}");
    Console.WriteLine($"Max player speed={snapshot.Players.Max(p => p.MaxSpeed):F3} m/s. Synthetic events only. Saved validated JSON to {output}");
    Console.WriteLine($"SERVICE VERIFIED | state={service.State} | origin={service.Origin} | model={service.CurrentModel} | cues={match.Commentary!.Cues.Count}");
    foreach (var time in new[] { 0d, 30, 85, 150, 225, 299, 300, 0, 176 })
    {
        var replay = Replay.At(match, time);
        if (replay.Events.Any(e => e.Time > time) || StoryReplay.At(match, time).Any(b => b.Time > time))
            throw new InvalidOperationException("Generated replay leaked future evidence.");
        foreach (var player in match.Players) _ = player.Path.At(time);
    }
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
