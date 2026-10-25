using PitchLens.Generation;

namespace PitchLens.App;

public sealed partial class MatchService
{
#if !IOS
    private string? varietyMetadataError;
#endif
    public MatchService()
    {
#if IOS
        LiveGenerationAvailable=false;
        Mode=SessionMode.Test;
#else
        Mode=Enum.TryParse<SessionMode>(Preferences.Default.Get("session-mode",""),out var stored)&&Enum.IsDefined(stored)&&stored!=SessionMode.Unchosen?stored:SessionMode.Unchosen;
#endif
        saveMode=mode=>Preferences.Default.Set("session-mode",mode.ToString());
        ViewSettings=PitchViewSettings.Load(Preferences.Default.Get("pitch-view-settings",""),out var viewNotice);
        ViewSettingsNotice=viewNotice;
        saveViewSettings=value=>Preferences.Default.Set("pitch-view-settings",PitchViewSettings.Serialize(value));
        Reading=ReadingSettings.Load(Preferences.Default.Get("reading-settings",""),out var readingNotice);
        ReadingNotice=readingNotice;saveReading=value=>Preferences.Default.Set("reading-settings",ReadingSettings.Serialize(value));
#if IOS
        var unavailable = new UnavailableMobileSource();
        generation = new(unavailable, presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        Explainer = new(unavailable);
        Director = new(unavailable);
        checkConnection = (_, _) => throw new InvalidOperationException(MobileBackendRequirement);
#else
        generation = new(new NativeSource(this), presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model, LoadVariety());
        Explainer = new(new NativeMomentSource(this));
        Director = new(new NativeDirectorSource(this));
        checkConnection = (progress, token) => new NativeSource(this).CheckAsync(progress, token);
#endif
        Initialize();
    }
#if IOS
    private sealed class UnavailableMobileSource : IScenarioSource, IMomentSource, IDirectorSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) =>
            throw new InvalidOperationException(MobileBackendRequirement);
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) =>
            throw new InvalidOperationException(MobileBackendRequirement);
        public Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token) =>
            throw new InvalidOperationException(MobileBackendRequirement);
    }
#else
    private MatchVariety LoadVariety()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "recent-gameplay.json");
        try
        {
            GameplaySignature[]? history = null;
            string? lastPair=null;
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 250000) throw new InvalidOperationException("Recent gameplay metadata is oversized.");
                var json=File.ReadAllText(path);
                if(json.TrimStart().StartsWith('['))
                    history=System.Text.Json.JsonSerializer.Deserialize(json,VarietyJsonContext.Default.GameplaySignatureArray);
                else
                {
                    var stored=System.Text.Json.JsonSerializer.Deserialize(json,VarietyJsonContext.Default.FixtureHistory)
                        ?? throw new InvalidOperationException("Recent fixture metadata is invalid.");
                    history=stored.Games;lastPair=stored.LastPair;
                }
                if(history is null) throw new InvalidOperationException("Recent gameplay metadata is invalid.");
            }
            var games=history??[];
            void Save()
            {
                Directory.CreateDirectory(FileSystem.AppDataDirectory);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary,System.Text.Json.JsonSerializer.Serialize(new FixtureHistory(lastPair,games),VarietyJsonContext.Default.FixtureHistory));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, path, true);
            }
            return new(history,entries=>{games=entries.ToArray();Save();},lastPair,pair=>{lastPair=pair;Save();});
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
        private ExplanationTeamSource? source;
        public string PipelineVersion=>ExplanationTeamContract.Pipeline;
        public TeamReport? Collaboration=>source?.Collaboration;
        public int ToolCalls => source?.ToolCalls ?? 0;
        public IReadOnlyList<MomentReadAudit>? ReadLedger=>source?.ReadLedger;
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) => ExplainAsync(evidence, token, null);
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token, IProgress<RequestProgress>? progress)
        {
            source = new(new CopilotExplanationRoleRunner(Executable(owner),FileSystem.AppDataDirectory));
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
#endif
}
