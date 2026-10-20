using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.App;

public sealed record NativeView(PresentationFrame Frame, string? Model, bool Fresh, string Status, bool Generating, ScenarioOrigin Origin);

public sealed class MatchService : IDisposable
{
    private readonly object gate = new();
    private readonly ReplayPresentation presentation = new(AuthoredMatchRecording.Create(), new MonotonicClock());
    private readonly GenerationService generation;
    private CancellationTokenSource? cancellation;
    private CancellationTokenSource? connectionCancellation;
    private bool disposed;
    public event Action? Changed;
    public bool Active { get; private set; } = true;
    public string CliOverride { get; set; } = "";
    public string Status { get; private set; } = "Recorded AI-authored 11v11 Sol example · Not a fresh request";
    public bool Generating => cancellation is not null;
    public bool CheckingConnection => connectionCancellation is not null;
    public string ConnectionStatus { get; private set; } = "Connection not checked. This check makes no generation request.";
    public string? CurrentModel { get; private set; } = AuthoredMatchRecording.Model;
    public ScenarioOrigin Origin { get; private set; } = ScenarioOrigin.RecordedSdkExample;
    public bool Fresh { get; private set; }
    public GenerationState RequestState => generation.State;
    public Match Match => presentation.Match;

    public MatchService()
    {
        generation = new(new NativeSource(this), presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        presentation.Select("arsenal-odegaard");
    }

    public NativeView? Sample(bool force = false)
    {
        lock (gate)
        {
            var frame = disposed ? null : presentation.Sample(force);
            return frame is null ? null : new(frame, CurrentModel, Fresh, Status, Generating, Origin);
        }
    }
    public void Command(string command, double value = 0, string? id = null)
    {
        lock (gate)
        {
            if (disposed) return;
            switch (command)
            {
                case "play": if (Active) presentation.Play(); break;
                case "pause": presentation.Pause(); break;
                case "restart": presentation.Restart(); break;
                case "toggle": if (Active) presentation.Toggle(); break;
                case "rate": presentation.SetRate(value); break;
                case "highlights": presentation.SetHighlights(value != 0); break;
                case "watch-goal": if (Active) presentation.WatchGoal(id ?? throw new ArgumentException("Goal ID required.")); break;
                case "seek": presentation.Seek(value); break;
                case "seek-by": presentation.Seek(presentation.Position + value); break;
                case "select": presentation.Select(id ?? throw new ArgumentException("Player ID required.")); break;
                case "mode":
                    presentation.SetMode(value switch
                    {
                        0 => ExplanationMode.Casual,
                        1 => ExplanationMode.Analyst,
                        _ => throw new ArgumentOutOfRangeException(nameof(value))
                    });
                    break;
                default: throw new ArgumentException("Unknown replay command.");
            }
        }
        Changed?.Invoke();
    }
    public void Activate() { lock (gate) { if (!disposed) Active = true; } }
    public void Suspend()
    {
        lock (gate)
        {
            if (disposed) return;
            presentation.Pause();
            Active = false;
        }
        Changed?.Invoke();
    }
    public void Cancel() { lock (gate) cancellation?.Cancel(); }
    public async Task CheckConnectionAsync()
    {
        using var token = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        lock (gate)
        {
            if (disposed || Generating || CheckingConnection) return;
            connectionCancellation = token;
            ConnectionStatus = "Checking bundled runtime, normal login and exact Sol availability. No AI generation.";
        }
        Changed?.Invoke();
        var progress = new Progress<string>(message =>
        {
            lock (gate) { if (disposed || connectionCancellation != token) return; ConnectionStatus = message; }
            Changed?.Invoke();
        });
        try
        {
            await new NativeSource(this).CheckAsync(progress, token.Token).ConfigureAwait(false);
            lock (gate) ConnectionStatus = "Connection verified: runtime startup, normal authentication, gpt-6.1-sol enabled and empty session creation. No generation request made.";
        }
        catch (OperationCanceledException) { lock (gate) ConnectionStatus = "Connection check cancelled or exceeded 45 seconds. Previous match unchanged."; }
        catch (CopilotGenerationException e) { lock (gate) ConnectionStatus = $"{e.Message} {e.Diagnostic}"; }
        finally
        {
            lock (gate) connectionCancellation = null;
            if (!disposed) Changed?.Invoke();
        }
    }
    public async Task GenerateAsync()
    {
        var token = new CancellationTokenSource();
        lock (gate)
        {
            if (disposed || cancellation is not null || CheckingConnection) { token.Dispose(); return; }
            cancellation = token;
        }
        Command("pause");
        var progress = new Progress<string>(message =>
        {
            lock (gate)
            {
                if (disposed || generation.State != GenerationState.Generating) return;
                Status = message;
            }
            Changed?.Invoke();
        });
        try
        {
            await generation.GenerateAsync(progress, token.Token).ConfigureAwait(false);
            lock (gate)
            {
                if (disposed) return;
                Status = generation.Status;
                if (generation.State == GenerationState.Succeeded)
                {
                    presentation.Replace(generation.Current);
                    CurrentModel = generation.CurrentModel;
                    Fresh = true;
                    Origin = generation.Origin;
                }
            }
        }
        finally
        {
            lock (gate)
            {
                cancellation = null;
                token.Dispose();
            }
            if (!disposed) Changed?.Invoke();
        }
    }
    public void Freeze(double time, long revision)
    {
        lock (gate)
        {
            if (disposed || presentation.Revision != revision) return;
            if (!Active) time = Math.Min(time, presentation.Position);
            presentation.Pause();
            presentation.Seek(time);
        }
        Changed?.Invoke();
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            cancellation?.Cancel();
            connectionCancellation?.Cancel();
            presentation.Dispose();
            disposed = true;
        }
    }
    private sealed class NativeSource(MatchService owner) : IScenarioSource
    {
        private CopilotScenarioSource Source()
        {
            var executable = string.IsNullOrWhiteSpace(owner.CliOverride)
                ? Path.Combine(Foundation.NSBundle.MainBundle.ResourcePath
                    ?? throw new CopilotGenerationException("App resources are missing."),
                    "CopilotRuntime", "prebuilds", "darwin-arm64", "copilot-runtime") : owner.CliOverride.Trim();
            var directory = FileSystem.AppDataDirectory;
            return new(executable, directory);
        }
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().GenerateAsync(progress, token), token);
        public Task CheckAsync(IProgress<string>? progress, CancellationToken token) =>
            Task.Run(() => Source().VerifyConnectionAsync(progress, token, includeSession: true), token);
    }
}
