using System.ComponentModel;
using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using PitchLens.Core;

namespace PitchLens.Generation;

// SDK 1.0.22 marks permission decisions and response schemas experimental.
#pragma warning disable GHCP001
public sealed class CopilotScenarioSource(string cliPath, string workingDirectory, string? responseArchiveDirectory = null) : IScenarioSource
{
    private readonly GenerationResponseArchive? archive = responseArchiveDirectory is null ? null : new(responseArchiveDirectory);
    public const string Model = "gpt-6.1-sol";
    public static string Prompt => AuthoredMatchPrompt.Text;
    public Task<string> GeneratePlanAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
        RequestAsync(Prompt, AuthoredMatchPrompt.Schema, "paths", progress, cancellationToken);
    public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
        PipelineAsync(token => GeneratePlanAsync(progress, token), progress, cancellationToken);
    public async Task<string> RefinePreservedAsync(string rawPaths, IProgress<string>? progress, CancellationToken token)
    {
        if (archive is not null) await archive.SaveAsync("paths", rawPaths).ConfigureAwait(false);
        return await PipelineAsync(_ => Task.FromResult(rawPaths), progress, token).ConfigureAwait(false);
    }
    private Task<string> PipelineAsync(Func<CancellationToken, Task<string>> paths, IProgress<string>? progress, CancellationToken token) =>
        AuthoredGenerationPipeline.RunAsync(paths,
            (document, failure, cancellation) => RequestAsync(AuthoredPathRefinement.Prompt(document, failure),
                AuthoredPathRefinement.Schema, "correction", progress, cancellation),
            (match, cancellation) => GenerateNarrationAsync(match, progress, cancellation),
            progress, token, archive);
    public Task<string> GenerateCommentaryAsync(Match match, IProgress<string>? progress, CancellationToken cancellationToken) =>
        RequestAsync(CommentaryPrompt.For(match), CommentaryPrompt.Schema, "commentary", progress, cancellationToken);
    public Task<string> GenerateNarrationAsync(Match match, IProgress<string>? progress, CancellationToken cancellationToken) =>
        RequestAsync(NarrationPrompt.For(match), NarrationPrompt.Schema, "commentary", progress, cancellationToken);
    internal CopilotClient CreateClient()
    {
        if (!Path.IsPathFullyQualified(cliPath) || !File.Exists(cliPath))
            throw new CopilotGenerationException("Copilot runtime executable is missing. Rebuild with bundled runtime acquisition enabled, or select a compatible CLI.");
        Directory.CreateDirectory(workingDirectory);
        return new(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(cliPath),
            WorkingDirectory = workingDirectory,
            UseLoggedInUser = true,
            LogLevel = CopilotLogLevel.Error
        });
    }
    internal static async Task ConnectAsync(CopilotClient client, IProgress<string>? progress, Action<string> stage, CancellationToken token)
    {
        stage("startup"); progress?.Report("Starting local Copilot CLI…");
        await client.StartAsync(token).ConfigureAwait(false);
        stage("auth"); progress?.Report("Checking Copilot authentication…");
        var auth = await client.GetAuthStatusAsync(token).ConfigureAwait(false);
        if (!auth.IsAuthenticated) throw new CopilotGenerationException("Run copilot login in Terminal with a Copilot-enabled account.");
        stage("models"); progress?.Report("Checking required GPT model availability…");
        RequireModel(await client.ListModelsAsync(token).ConfigureAwait(false));
    }
    public async Task VerifyConnectionAsync(IProgress<string>? progress, CancellationToken token, bool includeSession = false)
    {
        var stage = "setup";
        try
        {
            var client = CreateClient();
            await using var owner = new BoundedClient(client, () => progress?.Report("Cleaning up the owned SDK runtime…"));
            await ConnectAsync(client, progress, value => stage = value, token).ConfigureAwait(false);
            if (includeSession)
            {
                stage = "session";
                progress?.Report("Checking empty SDK session creation (no model message)…");
                var session = await client.CreateSessionAsync(CreateSessionConfig(), token).ConfigureAwait(false);
                stage = "cleanup";
                await session.AbortAsync(token).ConfigureAwait(false);
            }
            stage = "cleanup";
        }
        catch (JsonException e)
        {
            var diagnostic = Report(stage, e);
            throw new CopilotGenerationException("SDK/runtime protocol mismatch. Clear the override to use bundled runtime 1.0.98.", e, diagnostic.Diagnostic);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or Win32Exception or TimeoutException or RemoteRpcException or UnauthorizedAccessException or AggregateException)
        { throw Report(stage, e); }
    }
    private static CopilotGenerationException Report(string stage, Exception error)
    {
        var diagnostic = CopilotDiagnostics.Describe(stage, error);
        Console.Error.WriteLine(diagnostic.Trace);
        return new(diagnostic.Message, error, diagnostic.Trace);
    }
    private async Task<string> RequestAsync(string prompt, string responseSchema, string responseStage, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var stage = "setup";
        try
        {
            var client = CreateClient();
            await using var owner = new BoundedClient(client, () => progress?.Report("Cleaning up the owned SDK runtime…"));
            await ConnectAsync(client, progress, value => stage = value, cancellationToken).ConfigureAwait(false);
            progress?.Report(responseStage == "correction"
                ? $"AI refining rejected paths with {Model} (one correction maximum; events unchanged)…"
                : $"Generating synthetic match content with {Model}…");
            stage = "session";
            progress?.Report("Creating the Copilot SDK session…");
            var session = await client.CreateSessionAsync(CreateSessionConfig(), cancellationToken).ConfigureAwait(false);
            try
            {
                using var schema = JsonDocument.Parse(responseSchema);
                stage = "response";
                progress?.Report(responseStage switch
                {
                    "correction" => "Sol is refining player paths (one correction maximum)…",
                    "commentary" => "Sol is composing contextual narration and match story…",
                    _ => "Sol is authoring all 22 player paths and match events…"
                });
                var response = await session.SendAndWaitAsync(new MessageOptions
                {
                    Prompt = prompt,
                    ResponseSchema = schema.RootElement.Clone()
                }, TimeSpan.FromMinutes(4.5), cancellationToken).ConfigureAwait(false);
                var content = response?.Data.Content ?? throw new CopilotGenerationException("Copilot returned no match plan.");
                if (archive is not null)
                {
                    stage = "archive";
                    await archive.SaveAsync(responseStage, content).ConfigureAwait(false);
                }
                return content;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or Win32Exception or TimeoutException or RemoteRpcException or UnauthorizedAccessException or AggregateException)
            { throw Report(stage, e); }
            finally
            {
                stage = "cleanup";
                progress?.Report("Cleaning up the SDK session and owned runtime…");
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
        { throw Report(stage, e); }
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
        Model = Model,
        ReasoningEffort = "medium",
        AvailableTools = [],
        OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Match planning does not permit tool access.")),
        EnableConfigDiscovery = false,
        EnableSkills = false,
        EnableFileHooks = false,
        EnableHostGitOperations = false,
        EnableSessionStore = false,
        EnableOnDemandInstructionDiscovery = false,
        EnableSessionTelemetry = false,
        InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
        SystemMessage = new()
        {
            Mode = SystemMessageMode.Replace,
            Content = "Generate original synthetic football match plans or evidence-anchored commentary as requested. Only structured JSON. No tools, agents, files or external services. Never claim actual athlete performance or reveal future outcomes in timed commentary."
        }
    };
    internal sealed class BoundedClient(CopilotClient client, Action? onCleanup = null) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            onCleanup?.Invoke();
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
