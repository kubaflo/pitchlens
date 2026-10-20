using PitchLens.Core;

namespace PitchLens.Generation;

public interface IDirectorSource
{
    Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token);
}
public sealed record DirectedFilm(DirectorEvidence Evidence, FilmPlan Plan, bool Fresh, bool Cached = false);

public sealed class DirectorService(IDirectorSource source, TimeSpan? timeout = null)
{
    private readonly object gate = new();
    private CancellationTokenSource? request;
    private long revision;
    private long activityId;
    private bool userCancelled;
    public RequestActivity Activity { get; } = new();
    public event Action? Changed;
    public bool Busy { get { lock (gate) return request is not null; } }
    public DirectedFilm? Current { get; private set; }
    public string? LastRequestedHash { get; private set; }
    public string Status { get; private set; } = "Create a45–60second AI-directed in-app film from observed match evidence.";
    public void ChangeScene(long value)
    {
        lock (gate) { revision = value; request?.Cancel(); Current = null; LastRequestedHash = null; Activity.Reset(); Status = "New match. Previous film cleared."; }
        Changed?.Invoke();
    }
    public void Cancel()
    {
        lock (gate)
        {
            userCancelled = true;
            if (request is not null) Activity.Report(activityId, new(RequestStage.Cancelling, "Cancelling editorial request; waiting for cleanup…"));
            request?.Cancel();
        }
    }
    public void UseRecorded(DirectedFilm film)
    {
        lock (gate)
        {
            if (Busy || film.Fresh || film.Evidence.Revision != revision) throw new InvalidOperationException("Recorded film does not belong to this idle scene.");
            Current = film; Activity.Reset(); Status = "Recorded SDK Director's Cut / Not fresh / No AI call.";
        }
        Changed?.Invoke();
    }
    public async Task CreateAsync(DirectorEvidence evidence, bool retry = false)
    {
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(270));
        long id;
        lock (gate)
        {
            if (request is not null) return;
            if (evidence.Revision != revision) throw new InvalidOperationException("Captured film scene is stale.");
            if (!retry && LastRequestedHash == evidence.Hash && Activity.Snapshot.Stage is RequestStage.Failed or RequestStage.Cancelled) return;
            if (!retry && Current is { Fresh: true } current && current.Evidence.Hash == evidence.Hash)
            { Current = current with { Cached = true }; Status = "Cached exact-snapshot AI edit. No new usage."; return; }
            request = cancellation; LastRequestedHash = evidence.Hash;
            userCancelled = false;
            Status = "Requesting one AI editorial plan…"; id = activityId = Activity.Begin(Status);
        }
        Changed?.Invoke();
        var progress = new InlineProgress<string>(message =>
        {
            lock (gate) if (request == cancellation && evidence.Revision == revision) Activity.Report(id, RequestProgress.FromSdkStatus(message));
        });
        var terminal = RequestStage.Failed;
        try
        {
            var raw = await source.CreateAsync(evidence, progress, cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();
            progress.Report("Validating exact source clips, evidence and measured film duration…");
            var plan = evidence.Validate(raw);
            lock (gate)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (revision != evidence.Revision) return;
                Current = new(evidence, plan, true); Status = "Fresh SDK Director's Cut / gpt-6.1-sol / Synthetic footage.";
                terminal = RequestStage.Complete;
            }
        }
        catch (OperationCanceledException)
        {
            lock (gate) if (revision == evidence.Revision)
                Status = userCancelled ? "Director request cancelled. Match and earlier film kept." : "Director request timed out. Match and earlier film kept.";
            terminal = userCancelled ? RequestStage.Cancelled : RequestStage.Failed;
        }
        catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or IOException or InvalidOperationException or TimeoutException)
        { lock (gate) if (revision == evidence.Revision) Status = $"Director request failed: {e.Message} Match and earlier film kept."; }
        finally
        {
            lock (gate) { if (request == cancellation) request = null; if (revision == evidence.Revision) Activity.Finish(id, terminal, Status); }
            Changed?.Invoke();
        }
    }
}
