using PitchLens.Core;
using PitchLens.Generation;

if (args is ["--validate", var artifact])
{
    try
    {
        var scenario = ScenarioValidator.Parse(await File.ReadAllTextAsync(artifact));
        Console.WriteLine($"VALIDATED STORED SCENARIO | duration={scenario.Duration} | players={scenario.Players.Count} | events={scenario.Events.Count}");
        return 0;
    }
    catch (Exception e) when (e is ScenarioValidationException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"SCENARIO INVALID: {e.Message}");
        return 1;
    }
}
if (args is not ["--live", var cli, var output] || !Path.IsPathFullyQualified(output))
{
    Console.WriteLine("One paid SDK request, no retries: --live /absolute/path/to/copilot /absolute/output/scenario.json");
    Console.WriteLine("Validate existing JSON without AI/network: --validate /path/to/scenario.json");
    return 2;
}
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var progress = new Progress<string>(message => Console.WriteLine(message));
var source = new CopilotScenarioSource(cli, Path.Combine(Path.GetDirectoryName(output)!, "copilot-work"));
try
{
    var json = await source.GenerateAsync(progress, cancellation.Token);
    var match = ScenarioValidator.Parse(json);
    await File.WriteAllTextAsync(output, json, cancellation.Token);
    var snapshot = Replay.At(match, match.Duration);
    Console.WriteLine($"LIVE SDK VERIFIED | model={CopilotScenarioSource.Model} | duration={match.Duration} | players={match.Players.Count} | events={match.Events.Count}");
    Console.WriteLine($"Totals: passes={snapshot.Players.Sum(p => p.Passes)}, carries={match.Events.Count(e => e.Kind == EventKind.Carry)}, shots={snapshot.Players.Sum(p => p.Shots)}, goals={snapshot.Players.Sum(p => p.Goals)}");
    Console.WriteLine($"Max player speed={snapshot.Players.Max(p => p.MaxSpeed):F3} m/s. Synthetic events only. Saved validated JSON to {output}");
    return 0;
}
catch (Exception e) when (e is CopilotGenerationException or ScenarioValidationException or OperationCanceledException or IOException)
{
    Console.Error.WriteLine($"LIVE SDK NOT VERIFIED: {e.GetType().Name}: {e.Message}");
    if (e.InnerException is System.Text.Json.JsonException jsonError)
        Console.Error.WriteLine($"SDK JSON compatibility diagnostic: {jsonError.Message}");
    return 1;
}
