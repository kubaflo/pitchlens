namespace PitchLens.Generation;

public interface IMomentSource
{
    int ToolCalls => 0;
    Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token);
}

public sealed record ExplainedMoment(MomentEvidence Evidence, MomentAnswer Answer, int ToolCalls, bool Fresh = true);

public sealed class MomentExplanationService(IMomentSource source, TimeSpan? timeout = null)
{
    private readonly object gate = new();
    private CancellationTokenSource? running;
    private long revision;
    private bool userCancelled;
    public ExplainedMoment? Current { get; private set; }
    public bool Busy { get { lock (gate) return running is not null; } }
    public string Status { get; private set; } = "Choose an observed moment. Opening this panel makes no AI request.";
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
            Status = "New match. Previous match explanations cleared.";
        }
    }
    public void Cancel() { lock (gate) { userCancelled = true; running?.Cancel(); } }
    public void UseRecorded(ExplainedMoment recorded)
    {
        lock (gate)
        {
            if (running is not null || recorded.Fresh || recorded.Evidence.Revision != revision)
                throw new InvalidOperationException("Recorded analysis cannot replace an active request or another match.");
            Current = recorded;
            Status = "Recorded SDK explanation · gpt-6.1-sol · Not a fresh request. No usage consumed.";
        }
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
            userCancelled = false;
            Status = "Requesting Sol analysis of this captured evidence. One request; up to six read-only tool calls; no retries.";
        }
        try
        {
            var raw = await source.ExplainAsync(evidence, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            var answer = evidence.Validate(raw);
            lock (gate)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (revision != evidence.Revision) return;
                Current = new(evidence, answer, source.ToolCalls);
                Status = "Fresh Copilot SDK explanation · gpt-6.1-sol · Synthetic match.";
            }
        }
        catch (OperationCanceledException)
        {
            lock (gate) if (revision == evidence.Revision)
                Status = token.IsCancellationRequested || userCancelled
                    ? "Explanation cancelled. Previous valid explanation kept."
                    : "Explanation timed out. Previous valid explanation kept.";
        }
        catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        {
            lock (gate) if (revision == evidence.Revision) Status = $"Explanation failed: {e.Message} Previous valid explanation kept.";
        }
        finally { lock (gate) if (running == deadline) running = null; }
    }
}
