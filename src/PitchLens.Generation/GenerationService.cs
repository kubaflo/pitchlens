using PitchLens.Core;

namespace PitchLens.Generation;

public interface IScenarioSource
{
    Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken);
    Task<string> GenerateAsync(VarietyRequest request, IProgress<string>? progress, CancellationToken token) => GenerateAsync(progress, token);
}

public enum GenerationState { Ready, Generating, Succeeded, Failed, Cancelled }
public enum ScenarioOrigin { DeterministicDemo, RecordedSdkExample, FreshSdkGeneration }

public sealed class GenerationService(IScenarioSource source, Match initial, ScenarioOrigin initialOrigin = ScenarioOrigin.DeterministicDemo,
    string? initialModel = null, MatchVariety? variety = null)
{
    private int running;
    public Match Current { get; private set; } = initial;
    public GenerationState State { get; private set; }
    public string Status { get; private set; } = initialOrigin == ScenarioOrigin.RecordedSdkExample
        ? $"Recorded SDK example · {initialModel ?? RecordedScenario.Model} · Not a fresh request" : "Deterministic demo · Not SDK-generated";
    public ScenarioOrigin Origin { get; private set; } = initialOrigin;
    public string? CurrentModel { get; private set; } = initialOrigin == ScenarioOrigin.RecordedSdkExample ? initialModel ?? RecordedScenario.Model : null;
    public bool IsSdkGenerated => Origin == ScenarioOrigin.FreshSdkGeneration;
    public MatchFixture? RequestedFixture { get; private set; }
    public string? TerminalIssue { get; private set; }

    public async Task GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("A generation is already running.");
        State = GenerationState.Generating;
        TerminalIssue = null;
        Status = "Requesting synthetic scenario through Copilot SDK…";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            var request=variety?.Next();
            RequestedFixture=request?.Fixture;
            if(RequestedFixture is { } fixture) progress?.Report($"Creating {fixture.Home.Name} v {fixture.Away.Name} with verified selected players…");
            var json = request is null ? await source.GenerateAsync(progress, timeout.Token).ConfigureAwait(false) :
                await source.GenerateAsync(request, progress, timeout.Token).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            progress?.Report("Validating roster, trajectories, possession and event causality…");
            var candidate = AuthoredMatchPlan.Parse(json,expectedFixture:RequestedFixture);
            if (candidate.Narrative is null) throw new ScenarioValidationException("New matches require a matching AI narration/story package, not historical captions alone.");
            if(candidate.Commentary?.Contract!=2)throw new ScenarioValidationException("New games require matching dense caption slots, not historical sparse subtitles.");
            timeout.Token.ThrowIfCancellationRequested();
            variety?.Accept(candidate);
            Current = candidate;
            Origin = ScenarioOrigin.FreshSdkGeneration;
            CurrentModel = CopilotScenarioSource.Model;
            State = GenerationState.Succeeded;
            Status = "Copilot SDK · GPT-generated synthetic match · Not a real match";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = GenerationState.Cancelled;
            TerminalIssue = "Generation cancelled.";
            Status = "Generation cancelled. Previous match kept.";
        }
        catch (OperationCanceledException)
        {
            State = GenerationState.Failed;
            TerminalIssue = "Generation timed out after fifteen minutes (paths, optional one AI correction, narration/story).";
            Status = "Generation timed out after fifteen minutes (paths, optional one AI correction, narration/story). Previous match kept.";
        }
        catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or TimeoutException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            State = cancellationToken.IsCancellationRequested ? GenerationState.Cancelled : GenerationState.Failed;
            TerminalIssue = cancellationToken.IsCancellationRequested
                ? $"Generation cancelled. Runtime reported: {e.Message}"
                : $"Generation failed: {e.Message}";
            Status = cancellationToken.IsCancellationRequested
                ? $"Generation cancelled. Previous match kept. Runtime reported: {e.Message}"
                : $"Generation failed: {e.Message} Previous match kept.";
        }
        finally
        {
            Volatile.Write(ref running, 0);
        }
    }
}

public sealed class CopilotGenerationException(string message, Exception? inner = null, string? diagnostic = null) : Exception(message, inner)
{
    public string? Diagnostic { get; } = diagnostic;
}
