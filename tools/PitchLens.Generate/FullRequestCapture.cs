using PitchLens.Generation;
using PitchLens.Core;

internal sealed class FullRequestCapture(CopilotScenarioSource source, string? preservedPaths = null) : IScenarioSource
{
    public string? Response { get; private set; }
    public int SendCalls=>source.SendCalls;
    public int Responses=>source.Responses;
    public async Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
        Response = await (preservedPaths is null ? source.GenerateAsync(progress, cancellationToken) :
            source.RefinePreservedAsync(preservedPaths, progress, cancellationToken));
    public async Task<string> GenerateAsync(VarietyRequest request, IProgress<string>? progress, CancellationToken token) =>
        Response = await source.GenerateAsync(request, progress, token);
}

internal sealed class CapturedDirectorSource(CopilotScenarioSource source):IDirectorSource
{
    public string? Response{get;private set;}
    public async Task<string> CreateAsync(DirectorEvidence evidence,IProgress<string>? progress,CancellationToken token)=>
        Response=await source.GenerateDirectorAsync(evidence,progress,token);
}
internal sealed class WitnessClock:IMonotonicClock
{
    public double Seconds{get;set;}
}
