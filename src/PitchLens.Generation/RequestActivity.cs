using PitchLens.Core;

namespace PitchLens.Generation;

public enum RequestStage { Starting, Connecting, Authenticating, Model, Session, Thinking, Evidence, Validating, Cleanup, Cancelling, Complete, Cancelled, Failed }
public sealed record RequestProgress(RequestStage Stage, string Message, int ToolCalls = 0)
{
    public static RequestProgress FromSdkStatus(string message) => new(
        message.StartsWith("Cleaning", StringComparison.Ordinal) ? RequestStage.Cleanup :
        message.StartsWith("Validating", StringComparison.Ordinal) ? RequestStage.Validating :
        message.StartsWith("Starting", StringComparison.Ordinal) ? RequestStage.Connecting :
        message.StartsWith("Checking Copilot authentication", StringComparison.Ordinal) ? RequestStage.Authenticating :
        message.StartsWith("Checking required GPT", StringComparison.Ordinal) ? RequestStage.Model :
        message.Contains("session", StringComparison.OrdinalIgnoreCase) ? RequestStage.Session : RequestStage.Thinking, message);
}
public sealed record ActivitySnapshot(bool Active, RequestStage Stage, string Message, double ElapsedSeconds, int ToolCalls);

public sealed class RequestActivity(IMonotonicClock? clock = null)
{
    private readonly object gate = new();
    private readonly IMonotonicClock clock = clock ?? new MonotonicClock();
    private long request;
    private double started, ended;
    private bool active;
    private RequestProgress progress = new(RequestStage.Complete, "");
    public event Action? Changed;
    public void Reset()
    {
        lock (gate) { request++; active = false; started = ended = clock.Seconds; progress = new(RequestStage.Complete, ""); }
        Changed?.Invoke();
    }
    public ActivitySnapshot Snapshot
    {
        get { lock (gate) return new(active, progress.Stage, progress.Message, Math.Max(0, (active ? clock.Seconds : ended) - started), progress.ToolCalls); }
    }
    public long Begin(string message)
    {
        long id;
        lock (gate) { id = ++request; active = true; started = ended = clock.Seconds; progress = new(RequestStage.Starting, message); }
        Changed?.Invoke();
        return id;
    }
    public void Report(long id, RequestProgress value)
    {
        lock (gate)
        {
            if (id != request || !active) return;
            value = value with { ToolCalls = Math.Max(progress.ToolCalls, value.ToolCalls) };
            if (value == progress) return;
            progress = value;
        }
        Changed?.Invoke();
    }
    public void Finish(long id, RequestStage stage, string message)
    {
        lock (gate)
        {
            if (id != request || !active) return;
            progress = progress with { Stage = stage, Message = message };
            active = false; ended = clock.Seconds;
        }
        Changed?.Invoke();
    }
}

public sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
