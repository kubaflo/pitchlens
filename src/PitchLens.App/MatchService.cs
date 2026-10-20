using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.App;

public sealed record NativeView(PresentationFrame Frame, string? Model, bool Fresh, string Status, bool Generating, ScenarioOrigin Origin);

public sealed partial class MatchService : IDisposable
{
    private readonly object gate = new();
    private readonly ReplayPresentation presentation = new(NarrationRecording.Create(), new MonotonicClock());
    private readonly GenerationService generation;
    private readonly Func<IProgress<string>, CancellationToken, Task> checkConnection;
    public RequestActivity GenerationActivity { get; } = new();
    public RequestActivity ConnectionActivity { get; } = new();
    private long generationActivityId, connectionActivityId;
    public MomentExplanationService Explainer { get; }
    public MomentEvidence? ExplanationSnapshot { get; private set; }
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

    public MatchService(IScenarioSource scenarios, IMomentSource moments, Func<IProgress<string>, CancellationToken, Task> connection)
    {
        generation = new(scenarios, presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        Explainer = new(moments);
        checkConnection = connection;
        Initialize();
    }
    private void Initialize()
    {
        Explainer.ChangeScene(presentation.Revision);
        Explainer.Changed += NotifyChanged;
        presentation.Select("arsenal-odegaard");
    }
    private void NotifyChanged() { if (!disposed) Changed?.Invoke(); }

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
    public void Cancel()
    {
        lock (gate)
        {
            if (cancellation is not null) GenerationActivity.Report(generationActivityId, new(RequestStage.Cancelling, "Cancelling match creation; waiting for cleanup…"));
            cancellation?.Cancel();
        }
    }
    public void CancelConnection() { lock (gate) connectionCancellation?.Cancel(); }
    public void CaptureMoment(string? eventId = null)
    {
        lock (gate)
        {
            if (disposed || Explainer.Busy) return;
            presentation.Pause();
            ExplanationSnapshot = MomentEvidence.Capture(presentation.Match, presentation.Revision, presentation.Position, eventId);
        }
        Changed?.Invoke();
    }
    public void InspectEvidence(MomentEvidence captured, string id)
    {
        lock (gate)
        {
            if (disposed || presentation.Revision != captured.Revision)
                throw new InvalidOperationException("This evidence belongs to an earlier match. The current replay was not changed.");
            var fact = captured.Resolve(id);
            presentation.Pause();
            presentation.Seek(fact.Time);
        }
        Changed?.Invoke();
    }
    public async Task ExplainAsync()
    {
        MomentEvidence captured;
        lock (gate)
        {
            if (disposed || Generating || CheckingConnection || Explainer.Busy) return;
            captured = ExplanationSnapshot ?? throw new InvalidOperationException("Capture an observed moment first.");
        }
        await Explainer.GenerateAsync(captured, CancellationToken.None).ConfigureAwait(false);
    }
    public void LoadRecordedMoment()
    {
        lock (gate)
        {
            if (disposed) return;
            Explainer.UseRecorded(MomentRecording.Load(presentation.Match, presentation.Revision, presentation.Position));
        }
        Changed?.Invoke();
    }
    public async Task CheckConnectionAsync()
    {
        using var token = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        lock (gate)
        {
            if (disposed || Generating || CheckingConnection || Explainer.Busy) return;
            connectionCancellation = token;
            ConnectionStatus = "Checking bundled runtime, normal login and exact Sol availability. No AI generation.";
            connectionActivityId = ConnectionActivity.Begin(ConnectionStatus);
        }
        Changed?.Invoke();
        var activityId = connectionActivityId;
        var progress = new InlineProgress<string>(message =>
        {
            lock (gate) { if (disposed || connectionCancellation != token) return; ConnectionStatus = message; }
            ConnectionActivity.Report(activityId, RequestProgress.FromSdkStatus(message));
        });
        try
        {
            await checkConnection(progress, token.Token).ConfigureAwait(false);
            lock (gate) ConnectionStatus = "Connection verified: runtime startup, normal authentication, gpt-6.1-sol enabled and empty session creation. No generation request made.";
        }
        catch (OperationCanceledException) { lock (gate) ConnectionStatus = "Connection check cancelled or exceeded 45 seconds. Previous match unchanged."; }
        catch (CopilotGenerationException e) { lock (gate) ConnectionStatus = $"{e.Message} {e.Diagnostic}"; }
        finally
        {
            lock (gate) connectionCancellation = null;
            ConnectionActivity.Finish(activityId, ConnectionStatus.StartsWith("Connection verified", StringComparison.Ordinal) ? RequestStage.Complete :
                token.IsCancellationRequested ? RequestStage.Cancelled : RequestStage.Failed, ConnectionStatus);
            if (!disposed) Changed?.Invoke();
        }
    }
    public async Task GenerateAsync()
    {
        var token = new CancellationTokenSource();
        lock (gate)
        {
            if (disposed || cancellation is not null || CheckingConnection || Explainer.Busy) { token.Dispose(); return; }
            cancellation = token;
            generationActivityId = GenerationActivity.Begin("Preparing the match-generation request…");
        }
        Command("pause");
        var activityId = generationActivityId;
        var progress = new InlineProgress<string>(message =>
        {
            lock (gate)
            {
                if (disposed || cancellation != token || generation.State != GenerationState.Generating) return;
                Status = message;
            }
            GenerationActivity.Report(activityId, RequestProgress.FromSdkStatus(message));
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
                    Explainer.ChangeScene(presentation.Revision);
                    ExplanationSnapshot = null;
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
            GenerationActivity.Finish(activityId, generation.State == GenerationState.Succeeded ? RequestStage.Complete :
                generation.State == GenerationState.Cancelled ? RequestStage.Cancelled : RequestStage.Failed, Status);
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
            Explainer.Cancel();
            Explainer.Changed -= NotifyChanged;
            presentation.Dispose();
            disposed = true;
        }
    }
}
