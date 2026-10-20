namespace PitchLens.Generation;

public interface IMomentSource
{
    int ToolCalls => 0;
    Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token);
    Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token, IProgress<RequestProgress>? progress) =>
        ExplainAsync(evidence, token);
}

public sealed record ExplainedMoment(MomentEvidence Evidence, MomentAnswer Answer, int ToolCalls, bool Fresh = true, bool Cached = false);

public sealed class MomentExplanationService(IMomentSource source, TimeSpan? timeout = null)
{
    private readonly object gate = new();
    private CancellationTokenSource? running;
    private long revision;
    private bool userCancelled;
    private long activityId;
    public RequestActivity Activity { get; } = new();
    public event Action? Changed;
    public string? LastRequestedHash { get; private set; }
    public ExplainedMoment? Current { get; private set; }
    public bool Busy { get { lock (gate) return running is not null; } }
    public string Status { get; private set; } = "Explain the current observed moment with Sol.";
    public ExplainedMoment? VisibleAt(long currentRevision, double playhead)
    {
        lock (gate) return Current is { } result && result.Evidence.Revision == currentRevision &&
            double.IsFinite(playhead) && result.Evidence.Cutoff <= playhead ? result : null;
    }

    public void ChangeScene(long nextRevision)
    {
        lock (gate)
        {
            revision = nextRevision;
            running?.Cancel();
            Current = null;
            LastRequestedHash = null;
            Status = "New match. Previous match explanations cleared.";
            Activity.Reset();
        }
        Changed?.Invoke();
    }
    public void Cancel()
    {
        lock (gate)
        {
            userCancelled = true;
            if (running is not null) Activity.Report(activityId, new(RequestStage.Cancelling, "Cancelling request; waiting for bounded cleanup…"));
            running?.Cancel();
        }
    }
    public bool TryReuseFresh(MomentEvidence evidence)
    {
        lock (gate)
        {
            if (running is not null || Current is not { Fresh: true } current || evidence.Revision != revision ||
                current.Evidence.Hash != evidence.Hash) return false;
            Current = current with { Cached = true };
            Status = "Cached from the earlier successful Sol request for this exact snapshot. No new usage.";
            Activity.Reset();
        }
        Changed?.Invoke();
        return true;
    }
    public void UseRecorded(ExplainedMoment recorded)
    {
        lock (gate)
        {
            if (running is not null || recorded.Fresh || recorded.Evidence.Revision != revision)
                throw new InvalidOperationException("Recorded analysis cannot replace an active request or another match.");
            Current = recorded;
            Status = "Recorded SDK explanation · gpt-6.1-sol · Not a fresh request. No usage consumed.";
            Activity.Reset();
        }
        Changed?.Invoke();
    }
    public async Task GenerateAsync(MomentEvidence evidence, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(270));
        lock (gate)
        {
            if (running is not null) throw new InvalidOperationException("An explanation is already running.");
            if (evidence.Revision != revision) throw new InvalidOperationException("Captured match is no longer current.");
            running = deadline;
            LastRequestedHash = evidence.Hash;
            userCancelled = false;
            Status = "Requesting Sol analysis of this captured evidence. One request; up to six read-only tool calls; no retries.";
            activityId = Activity.Begin("Preparing captured evidence…");
        }
        Changed?.Invoke();
        var id = activityId;
        var progress = new InlineProgress<RequestProgress>(value =>
        {
            lock (gate)
            {
                if (running != deadline || revision != evidence.Revision) return;
                Activity.Report(id, value);
            }
        });
        var terminal = RequestStage.Failed;
        try
        {
            var raw = await source.ExplainAsync(evidence, deadline.Token, progress).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            progress.Report(new(RequestStage.Validating, "Checking snapshot identity and observed evidence references…", source.ToolCalls));
            var answer = evidence.Validate(raw);
            lock (gate)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (revision != evidence.Revision) return;
                Current = new(evidence, answer, source.ToolCalls);
                Status = "Fresh Copilot SDK explanation · gpt-6.1-sol · Synthetic match.";
                terminal = RequestStage.Complete;
            }
        }
        catch (OperationCanceledException)
        {
            lock (gate) if (revision == evidence.Revision)
                Status = token.IsCancellationRequested || userCancelled
                    ? "Explanation cancelled. Previous valid explanation kept."
                    : "Explanation timed out. Previous valid explanation kept.";
            terminal = userCancelled || token.IsCancellationRequested ? RequestStage.Cancelled : RequestStage.Failed;
        }
        catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            lock (gate) if (revision == evidence.Revision) Status = $"Explanation failed: {e.Message} Previous valid explanation kept.";
        }
        finally
        {
            lock (gate)
            {
                if (running == deadline) running = null;
                if (revision == evidence.Revision) Activity.Finish(id, terminal, Status);
            }
            Changed?.Invoke();
        }
    }
}
