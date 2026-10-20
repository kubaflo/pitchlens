using PitchLens.Generation;

namespace PitchLens.App;

public sealed partial class MatchService
{
    public MatchService()
    {
        generation = new(new NativeSource(this), presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        Explainer = new(new NativeMomentSource(this));
        checkConnection = (progress, token) => new NativeSource(this).CheckAsync(progress, token);
        Initialize();
    }
    private static string Executable(MatchService owner) => string.IsNullOrWhiteSpace(owner.CliOverride)
        ? Path.Combine(Foundation.NSBundle.MainBundle.ResourcePath ?? throw new CopilotGenerationException("App resources missing."),
            "CopilotRuntime", "prebuilds", "darwin-arm64", "copilot-runtime") : owner.CliOverride.Trim();

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
        private CopilotScenarioSource Source() => new(Executable(owner), FileSystem.AppDataDirectory);
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().GenerateAsync(progress, token), token);
        public Task CheckAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().VerifyConnectionAsync(progress, token, includeSession: true), token);
    }
}
