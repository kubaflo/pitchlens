using PitchLens.Core;

namespace PitchLens.Generation;

public interface IScenarioSource
{
    Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken);
}

public enum GenerationState { Ready, Generating, Succeeded, Failed, Cancelled }

public sealed class GenerationService(IScenarioSource source, Match initial)
{
    private int running;
    public Match Current { get; private set; } = initial;
    public GenerationState State { get; private set; }
    public string Status { get; private set; } = "Deterministic demo · Not SDK-generated";
    public bool IsSdkGenerated { get; private set; }

    public async Task GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("A generation is already running.");
        State = GenerationState.Generating;
        Status = "Requesting synthetic scenario through Copilot SDK…";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            var json = await source.GenerateAsync(progress, timeout.Token).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            progress?.Report("Validating roster, trajectories, possession and event causality…");
            var candidate = ScenarioValidator.Parse(json);
            timeout.Token.ThrowIfCancellationRequested();
            Current = candidate;
            IsSdkGenerated = true;
            State = GenerationState.Succeeded;
            Status = "Copilot SDK · GPT-generated synthetic match · Not a real match";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = GenerationState.Cancelled;
            Status = "Generation cancelled. Previous match kept.";
        }
        catch (OperationCanceledException)
        {
            State = GenerationState.Failed;
            Status = "Generation timed out after three minutes. Previous match kept.";
        }
        catch (Exception e) when (e is ScenarioValidationException or CopilotGenerationException or TimeoutException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            State = cancellationToken.IsCancellationRequested ? GenerationState.Cancelled : GenerationState.Failed;
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

public sealed class CopilotGenerationException(string message, Exception? inner = null) : Exception(message, inner);
