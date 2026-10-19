using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class GenerationResponseArchiveTests
{
    [Fact]
    public async Task Raw_invalid_output_is_preserved_exactly_for_offline_validation_without_logging_requests()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pitchlens-response-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string invalid = "```json\n{\"duration\":300,\"players\":[]}\n```";
            var archive = new GenerationResponseArchive(directory);
            await archive.SaveAsync("paths", invalid);
            var path = Assert.Single(Directory.GetFiles(directory));
            Assert.EndsWith("-paths.json", path);
            Assert.Equal(invalid, await File.ReadAllTextAsync(path));
            Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Parse(invalid));
            Assert.Equal(invalid, await File.ReadAllTextAsync(path));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public async Task Request_stage_names_are_bounded_and_do_not_allow_path_traversal_or_overwrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pitchlens-response-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var archive = new GenerationResponseArchive(directory);
            await Assert.ThrowsAsync<ArgumentException>(() => archive.SaveAsync("../request", "{}"));
            await Assert.ThrowsAsync<CopilotGenerationException>(() => archive.SaveAsync("paths", new string('x', 1_000_001)));
            Assert.False(Directory.Exists(directory));
            await archive.SaveAsync("paths", "original");
            await Assert.ThrowsAsync<IOException>(() => archive.SaveAsync("paths", "replacement"));
            Assert.Equal("original", await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(directory))));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }
}
