namespace PitchLens.Generation;

public interface IMomentSource
{
    string PipelineVersion=>"single-v1";
    TeamReport? Collaboration=>null;
    int ToolCalls => 0;
    IReadOnlyList<MomentReadAudit>? ReadLedger=>null;
    Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token);
    Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token, IProgress<RequestProgress>? progress) =>
        ExplainAsync(evidence, token);
}

public sealed record ExplainedMoment(MomentEvidence Evidence, MomentAnswer Answer, int ToolCalls, bool Fresh = true, bool Cached = false,
    MomentReadAudit[]? Ledger=null,string PipelineVersion="single-v1",TeamReport? Collaboration=null);

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
    public bool UsesTeam=>source.PipelineVersion==ExplanationTeamContract.Pipeline;
    public TeamReport? Team{get{lock(gate)return source.Collaboration is { } team&&team.Identity.Revision==revision?team:null;}}
    public string RequestKey(MomentEvidence evidence)=>source.PipelineVersion=="single-v1"?evidence.Hash:
        ExplanationTeamContract.Hash(source.PipelineVersion+"|"+evidence.Hash);
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
                current.Evidence.Hash != evidence.Hash||current.PipelineVersion!=source.PipelineVersion) return false;
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
            Status = "Single-agent recorded explanation · gpt-6.1-sol · Not a fresh request. No usage consumed.";
            Activity.Reset();
        }
        Changed?.Invoke();
    }
    public async Task GenerateAsync(MomentEvidence evidence, CancellationToken token)
    {
        if(!evidence.HasRequestedContribution)
        {
            lock(gate)Status="No observed involvement for that player in this captured event window. No AI request sent; choose another focus or task.";
            Changed?.Invoke();return;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(270));
        lock (gate)
        {
            if (running is not null) throw new InvalidOperationException("An explanation is already running.");
            if (evidence.Revision != revision) throw new InvalidOperationException("Captured match is no longer current.");
            running = deadline;
            LastRequestedHash = RequestKey(evidence);
            userCancelled = false;
            Status = UsesTeam?"Three specialist sessions: analyst → editor → independent reviewer. Up to three responses / twelve reads / 270s total; no retries.":
                "Requesting Sol analysis of this captured evidence. One request; up to six read-only tool calls; no retries.";
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
            if(UsesTeam)Changed?.Invoke();
        });
        var terminal = RequestStage.Failed;
        try
        {
            var raw = await source.ExplainAsync(evidence, deadline.Token, progress).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            progress.Report(new(RequestStage.Validating, "Checking snapshot identity and observed evidence references…", source.ToolCalls));
            var answer = evidence.Validate(raw);
            var team=source.Collaboration;
            if(UsesTeam&&(team is null||!team.Approved||team.Pipeline!=ExplanationTeamContract.Pipeline||
                team.Identity.FactsHash!=evidence.Hash||team.Identity.Revision!=evidence.Revision||team.Identity.Cutoff!=evidence.Cutoff||
                team.Identity.Version!=1||team.Identity.FocusId!=evidence.FocusId||team.Identity.Task!=evidence.Preferences.Task||
                team.Identity.Scope!=evidence.Preferences.Scope||team.Identity.SubjectId!=evidence.Preferences.SubjectId||
                team.CandidateHash!=ExplanationTeamContract.Hash(ExplanationTeamContract.AnswerJson(answer))||
                team.Stages.Length!=3||team.Stages.Any(s=>s.Status!="Validated")||
                !team.Stages.Select(s=>s.Role).SequenceEqual(Enum.GetNames<ExplanationRole>())||
                team.Stages.Any(s=>s.Model!=CopilotScenarioSource.Model||s.SessionsCreated!=1||s.SdkSends!=1||s.Responses!=1)||
                team.Checks.Length!=ExplanationTeamContract.Claims(answer).Length||team.Checks.Any(c=>!c.Approved)))
                throw new ScenarioValidationException("A complete independent final-answer review is required; no single-agent fallback.");
            if(UsesTeam)
            {
                var indexed=ExplanationTeamContract.Claims(answer);
                if(team!.Checks.Where((c,i)=>c.ClaimId!=indexed[i].Id||!c.EvidenceIds.SequenceEqual(indexed[i].EvidenceIds)).Any())
                    throw new ScenarioValidationException("Final reviewed claim identity/evidence mismatch.");
            }
            if(source.PipelineVersion is not("single-v1" or ExplanationTeamContract.Pipeline))
                throw new ScenarioValidationException("Unknown explanation pipeline version.");
            var ledger=source.ReadLedger?.ToArray();
            if(source.ToolCalls<0||source.ToolCalls>(UsesTeam?12:6)||ledger is {Length:>6}||ledger is not null&&
                (ledger.Length>source.ToolCalls||ledger.Where((r,i)=>r.Sequence!=i+1||r.Cutoff!=evidence.Cutoff||
                    (r.Tool=="observed_match"?r.EventId is not null:r.Tool!="observed_event"||!evidence.Facts.Events.Any(e=>e.Id==r.EventId))).Any()))
                throw new ScenarioValidationException("Tool ledger does not match the bounded captured evidence.");
            lock (gate)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (revision != evidence.Revision) return;
                Current = new(evidence, answer, source.ToolCalls,Ledger:ledger,PipelineVersion:source.PipelineVersion,Collaboration:team);
                Status = UsesTeam?"Fresh three-agent explanation · gpt-6.1-sol · Final candidate reviewed, not certified true.":
                    "Fresh Copilot SDK explanation · gpt-6.1-sol · Synthetic match.";
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
