using System.ComponentModel;
using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace PitchLens.Generation;

// SDK 1.0.22 marks permission decisions and response schemas experimental.
#pragma warning disable GHCP001
public sealed class CopilotScenarioSource(string cliPath, string workingDirectory) : IScenarioSource
{
    public const string Model = "gpt-6.1-sol";
    public static string Prompt => FiveMinutePrompt.Text;
    public async Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(cliPath) || !File.Exists(cliPath))
            throw new CopilotGenerationException("Copilot runtime executable is missing. Rebuild with bundled runtime acquisition enabled, or select a compatible CLI.");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var client = new CopilotClient(new CopilotClientOptions
            {
                Connection = RuntimeConnection.ForStdio(cliPath), WorkingDirectory = workingDirectory,
                UseLoggedInUser = true, LogLevel = CopilotLogLevel.Error
            });
            await using var owner = new BoundedClient(client);
            progress?.Report("Starting local Copilot CLI…");
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report("Checking Copilot authentication…");
            var auth = await client.GetAuthStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!auth.IsAuthenticated) throw new CopilotGenerationException("Run copilot login in Terminal with a Copilot-enabled account.");
            progress?.Report("Checking required GPT model availability…");
            RequireModel(await client.ListModelsAsync(cancellationToken).ConfigureAwait(false));
            progress?.Report($"Planning a five-minute synthetic match with {Model}…");
            var session = await client.CreateSessionAsync(CreateSessionConfig(), cancellationToken).ConfigureAwait(false);
            try
            {
                using var schema = JsonDocument.Parse(FiveMinutePrompt.Schema);
                var response = await session.SendAndWaitAsync(new MessageOptions
                {
                    Prompt = Prompt, ResponseSchema = schema.RootElement.Clone()
                }, TimeSpan.FromMinutes(4.5), cancellationToken).ConfigureAwait(false);
                return response?.Data.Content ?? throw new CopilotGenerationException("Copilot returned no match plan.");
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await session.AbortAsync(cleanup.Token).ConfigureAwait(false); }
                catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
                { throw new CopilotGenerationException("Session cleanup exceeded five seconds; the owned runtime will be disposed.", e); }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotGenerationException) { throw; }
        catch (JsonException e)
        { throw new CopilotGenerationException("SDK/runtime protocol mismatch. Clear the override to use bundled runtime 1.0.98.", e); }
        catch (Exception e) when (e is IOException or InvalidOperationException or Win32Exception or TimeoutException or RemoteRpcException or UnauthorizedAccessException or AggregateException)
        { throw new CopilotGenerationException($"Copilot SDK request failed ({e.GetType().Name}). Check login, network and access to {Model}.", e); }
    }
    public static ModelInfo RequireModel(IEnumerable<ModelInfo> models)
    {
        var selected = models.SingleOrDefault(m => m.Id == Model);
        if (selected is null || selected.Policy?.State == "disabled")
            throw new CopilotGenerationException($"Required GPT model {Model} is unavailable or disabled. No model fallback is allowed.");
        return selected;
    }
    public static SessionConfig CreateSessionConfig() => new()
    {
        Model = Model, ReasoningEffort = "medium", AvailableTools = [],
        OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Match planning does not permit tool access.")),
        EnableConfigDiscovery = false, EnableSkills = false, EnableFileHooks = false,
        EnableHostGitOperations = false, EnableSessionStore = false,
        EnableOnDemandInstructionDiscovery = false, EnableSessionTelemetry = false,
        InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
        SystemMessage = new()
        {
            Mode = SystemMessageMode.Replace,
            Content = "Plan an original sustained five-minute synthetic football match using compact causal actions and tactical positions. Only structured JSON. No tools, agents, files or external services. Never claim actual athlete performance."
        }
    };
    private sealed class BoundedClient(CopilotClient client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var disposal = client.DisposeAsync().AsTask();
            try { await disposal.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
            catch (TimeoutException e)
            {
                await client.ForceStopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await disposal.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                throw new CopilotGenerationException("Cleanup timed out; its owned runtime was stopped. Match not replaced.", e);
            }
        }
    }
}
#pragma warning restore GHCP001
