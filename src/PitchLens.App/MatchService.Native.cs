using PitchLens.Generation;

namespace PitchLens.App;

public sealed partial class MatchService
{
    private string? varietyMetadataError;
    public MatchService()
    {
        generation = new(new NativeSource(this), presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model, LoadVariety());
        Explainer = new(new NativeMomentSource(this));
        Director = new(new NativeDirectorSource(this));
        checkConnection = (progress, token) => new NativeSource(this).CheckAsync(progress, token);
        Initialize();
    }
    private MatchVariety LoadVariety()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "recent-gameplay.json");
        try
        {
            GameplaySignature[]? history = null;
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 250000) throw new InvalidOperationException("Recent gameplay metadata is oversized.");
                history = System.Text.Json.JsonSerializer.Deserialize(File.ReadAllText(path), VarietyJsonContext.Default.GameplaySignatureArray)
                    ?? throw new InvalidOperationException("Recent gameplay metadata is invalid.");
            }
            return new(history, entries =>
            {
                Directory.CreateDirectory(FileSystem.AppDataDirectory);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(entries.ToArray(), VarietyJsonContext.Default.GameplaySignatureArray));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, path, true);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            varietyMetadataError = $"Recent gameplay history could not be read: {e.Message} Resolve the metadata file at {path} and reopen the app before generating; no model request was made.";
            return new();
        }
    }
    private static string Executable(MatchService owner) => string.IsNullOrWhiteSpace(owner.CliOverride)
        ? Path.Combine(Foundation.NSBundle.MainBundle.ResourcePath ?? throw new CopilotGenerationException("App resources missing."),
            "CopilotRuntime", "prebuilds", "darwin-arm64", "copilot-runtime") : owner.CliOverride.Trim();
    private sealed class NativeDirectorSource(MatchService owner) : IDirectorSource
    {
        public Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => new CopilotScenarioSource(Executable(owner), FileSystem.AppDataDirectory).GenerateDirectorAsync(evidence, progress, token), token);
    }

    private sealed class NativeMomentSource(MatchService owner) : IMomentSource
    {
        private CopilotMomentSource? source;
        public int ToolCalls => source?.ToolCalls ?? 0;
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) => ExplainAsync(evidence, token, null);
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token, IProgress<RequestProgress>? progress)
        {
            source = new(Executable(owner), FileSystem.AppDataDirectory);
            return Task.Run(() => source.ExplainAsync(evidence, token, progress), token);
        }
    }
    private sealed class NativeSource(MatchService owner) : IScenarioSource
    {
        private CopilotScenarioSource Source()
        {
            if (owner.varietyMetadataError is { } error) throw new CopilotGenerationException(error);
            return new(Executable(owner), FileSystem.AppDataDirectory);
        }
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().GenerateAsync(progress, token), token);
        public Task<string> GenerateAsync(VarietyRequest request, IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().GenerateAsync(request, progress, token), token);
        public Task CheckAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().VerifyConnectionAsync(progress, token, includeSession: true), token);
    }
}
