using System.ComponentModel;
using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace PitchLens.Generation;

// SDK 1.0.22 marks permission decisions and response schemas experimental.
#pragma warning disable GHCP001
public sealed class CopilotScenarioSource(string cliPath, string workingDirectory) : IScenarioSource
{
    public const string Model = "gpt-5.4-mini";

    public async Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(cliPath) || !File.Exists(cliPath))
            throw new CopilotGenerationException("Copilot runtime executable is missing. Rebuild with bundled runtime acquisition enabled, or select a compatible installed CLI. Run copilot login in Terminal first.");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var client = new CopilotClient(new CopilotClientOptions
            {
                Connection = RuntimeConnection.ForStdio(cliPath),
                WorkingDirectory = workingDirectory,
                UseLoggedInUser = true,
                LogLevel = CopilotLogLevel.Error
            });
            await using var owner = new BoundedClient(client);
            progress?.Report("Starting local Copilot CLI…");
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report("Checking Copilot authentication…");
            var auth = await client.GetAuthStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!auth.IsAuthenticated)
                throw new CopilotGenerationException("Copilot is not authenticated. Run copilot login in Terminal with a Copilot-enabled account.");
            progress?.Report("Checking required GPT model availability…");
            var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);
            var selected = models.SingleOrDefault(m => m.Id == Model);
            if (selected is null || selected.Policy?.State == "disabled")
                throw new CopilotGenerationException($"Required GPT model {Model} is unavailable or disabled for your Copilot account. No model fallback is allowed.");
            progress?.Report($"Generating one structured synthetic match with {Model}…");
            var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = Model,
                AvailableTools = [],
                OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Scenario generation does not permit tool access.")),
                EnableConfigDiscovery = false,
                EnableSkills = false,
                EnableFileHooks = false,
                EnableHostGitOperations = false,
                EnableSessionStore = false,
                EnableOnDemandInstructionDiscovery = false,
                EnableSessionTelemetry = false,
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = "You generate only structured synthetic football replay JSON. Do not use tools, agents, files or external services. Follow the schema and causal constraints exactly. These events are invented, not real athlete performance."
                }
            }, cancellationToken).ConfigureAwait(false);
            try
            {
                using var schema = JsonDocument.Parse(Schema);
                var response = await session.SendAndWaitAsync(new MessageOptions
                {
                    Prompt = Prompt,
                    ResponseSchema = schema.RootElement.Clone()
                }, TimeSpan.FromMinutes(2.5), cancellationToken).ConfigureAwait(false);
                return response?.Data.Content ?? throw new CopilotGenerationException("Copilot returned no scenario response.");
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await session.AbortAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new CopilotGenerationException("Copilot session cleanup exceeded five seconds. The owned runtime will be disposed.", e);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotGenerationException) { throw; }
        catch (JsonException e)
        {
            throw new CopilotGenerationException("Copilot SDK/runtime protocol mismatch. Clear the CLI override to use the bundled 1.0.98 runtime, or install a compatible version.", e);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or Win32Exception or TimeoutException or RemoteRpcException or UnauthorizedAccessException or AggregateException)
        {
            throw new CopilotGenerationException($"Copilot SDK/CLI request failed ({e.GetType().Name}). Check CLI compatibility, copilot login, network and access to {Model}.", e);
        }
    }

    private sealed class BoundedClient(CopilotClient client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Client disposal owns its sessions; their detach RPC otherwise has no deadline.
            var disposal = client.DisposeAsync().AsTask();
            try
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                await client.ForceStopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await disposal.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                throw new CopilotGenerationException("Copilot cleanup timed out; its owned runtime was stopped. The match was not replaced.", e);
            }
        }
    }

    public static string Prompt => """
        Generate an ORIGINAL synthetic 72-second 6-a-side football replay using EVERY roster ID below exactly once.
        2026/27 names and clubs are factual; all match actions are invented. No real match claims.
        Return only JSON matching the schema. You must invent player keyframes AND event choices/times, not a seed.
        Coordinates are metres: x 0..105, y 0..68. Arsenal attacks x=105, City x=0.
        Each player's 2..12 keyframes must start at time 0, end at 72, strictly increase.
        Use realistic movement: every segment <=8 metres/second. Background players can stay stationary.
        All field players must initially be spread over the pitch, not stacked at one point.
        Include 5..12 chronologically ordered resolved events: at least 2 passes, a moving carry,
        one shot and its goal. These are illustrative small-sided selections, NOT actual starting XIs.
        kind is pass/carry/shot/goal/kickoff. IDs unique short ASCII. order is 0 normally.
        A shot and its goal have identical time, order 0 for shot and 1 for goal.
        Each pass/carry/shot has startTime >=0, duration >=1 second, no overlapping actions.
        Actor MUST own the ball continuously immediately before each action.
        Initial owner is initialOwnerId. Pass transfers ownership to receiver at resolved time.
        completed=true ONLY if receiver is same team; false ONLY for opposing-team interception.
        Pass requires receiverId and completed; all other kinds require both null.
        Carry must move >=1m along its player's trajectory between startTime and time.
        Shot is followed immediately by one goal at the same time/actor. Only scoring shots are supported.
        After a goal, either end the action sequence OR kickoff by OPPOSING team at least 2 seconds later.
        Kickoff player MUST be at (52.5,34) at kickoff time; kickoff startTime=null, goal startTime=null.
        Flight speed from actor at startTime to receiver at time (or goal x=105/0,y=34) <=40 m/s.
        A flight must start strictly after the prior possession change (leave at least 1 second).
        Easier valid structure: initial owner passes at 8 (start6), receiver carries 12..18,
        receiver passes at24 (start22), receiver passes at34(start32), receiver shoots at48(start46),
        same-actor goal48/order1; no further events. You choose actors, positions and times coherently.
        Pick shot actor near their attacking goal by shot start, keep later trajectories within speed limit.
        For each event output every schema field including nullable receiverId,completed,startTime.
        Verified roster:
        """ + JsonSerializer.Serialize(PremierLeagueRoster.Players.ToArray(), ScenarioJsonContext.Default.RosterPlayerArray);

    private const string Schema = """
        {"type":"object","additionalProperties":false,"required":["duration","initialOwnerId","players","events"],"properties":{
        "duration":{"type":"number","const":72},"initialOwnerId":{"type":"string"},
        "players":{"type":"array","minItems":12,"maxItems":12,"items":{"type":"object","additionalProperties":false,
          "required":["id","frames"],"properties":{"id":{"type":"string"},"frames":{"type":"array","minItems":2,"maxItems":12,
          "items":{"type":"object","additionalProperties":false,"required":["time","x","y"],"properties":{
          "time":{"type":"number"},"x":{"type":"number"},"y":{"type":"number"}}}}}}},
        "events":{"type":"array","minItems":5,"maxItems":12,"items":{"type":"object","additionalProperties":false,
          "required":["id","time","order","kind","actorId","receiverId","completed","startTime"],"properties":{
          "id":{"type":"string"},"time":{"type":"number"},"order":{"type":"integer"},"kind":{"type":"string","enum":["pass","carry","shot","goal","kickoff"]},
          "actorId":{"type":"string"},"receiverId":{"type":["string","null"]},"completed":{"type":["boolean","null"]},"startTime":{"type":["number","null"]}}}}
        }}
        """;
}
#pragma warning restore GHCP001
