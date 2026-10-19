using System.Text;

namespace PitchLens.Generation;

public sealed class GenerationResponseArchive(string directory)
{
    private readonly string requestId = Guid.NewGuid().ToString("N");
    public async Task SaveAsync(string stage, string response)
    {
        if (stage is not ("paths" or "correction" or "corrected" or "commentary" or "validated"))
            throw new ArgumentException("Unknown generation artifact stage.", nameof(stage));
        if (response.Length > 1_000_000)
            throw new CopilotGenerationException("Model response exceeded the bounded diagnostic archive limit. Previous match kept.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{requestId}-{stage}.json");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        // Preserve the model's exact response, even when subsequent JSON/quality validation rejects it.
        await writer.WriteAsync(response).ConfigureAwait(false);
    }
}
