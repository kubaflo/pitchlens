using PitchLens.Generation;

internal sealed class FullRequestCapture(CopilotScenarioSource source, string? preservedPaths = null) : IScenarioSource
{
    public string? Response { get; private set; }
    public async Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
        Response = await (preservedPaths is null ? source.GenerateAsync(progress, cancellationToken) :
            source.RefinePreservedAsync(preservedPaths, progress, cancellationToken));
}
