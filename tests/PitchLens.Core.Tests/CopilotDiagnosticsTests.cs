using System.ComponentModel;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class CopilotDiagnosticsTests
{
    [Fact]
    public void Sanitized_cause_chain_identifies_transport_permissions_stage_and_codes_without_private_text()
    {
        var error = new IOException("Process exited with code 37. EACCES /Users/private-user/.config/private-file; " +
            "Bearer secret-value github_pat_private-value request={confidential football prompt}",
            new Win32Exception(1));
        var diagnostic = CopilotDiagnostics.Describe("startup", error);
        Assert.Contains("runtime startup", diagnostic.Message);
        Assert.Contains("permission denied", diagnostic.Message);
        Assert.Contains("child exit=37", diagnostic.Trace);
        Assert.Contains("native=1", diagnostic.Trace);
        Assert.Contains("HRESULT=", diagnostic.Trace);
        foreach (var value in new[] { "private-user", "private-file", "secret-value", "github_pat_", "Bearer", "confidential", "/Users/" })
        {
            Assert.DoesNotContain(value, diagnostic.Trace);
            Assert.DoesNotContain(value, diagnostic.Message);
        }
    }

    [Theory]
    [InlineData("dlopen(runtime.node): secret-path", "native runtime load failure")]
    [InlineData("broken pipe https://private.example?token=private", "broken runtime pipe")]
    [InlineData("Sandbox restriction: /private/path", "sandbox restriction")]
    [InlineData("Connection reset by peer. private", "runtime transport closed")]
    public void Known_runtime_failure_categories_are_actionable_without_copying_stderr(string message, string expected)
    {
        var result = CopilotDiagnostics.Describe("response", new IOException(message));
        Assert.Contains(expected, result.Message);
        Assert.Contains("model response", result.Trace);
        Assert.DoesNotContain("private", result.Message);
        Assert.DoesNotContain("private", result.Trace);
    }

    [Fact]
    public void Unknown_exception_messages_and_untrusted_stage_are_never_logged()
    {
        var result = CopilotDiagnostics.Describe("authorization-secret", new Exception("personal@example.invalid arbitrary request body"));
        Assert.DoesNotContain("authorization-secret", result.Trace);
        Assert.DoesNotContain("personal", result.Trace);
        Assert.DoesNotContain("request body", result.Trace);
        Assert.Contains("SDKException", result.Trace);
        Assert.Contains("connection setup", result.Message);
    }

    [Fact]
    public async Task Child_runtime_exiting_before_rpc_is_reported_without_a_generation_request()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "pitchlens-runtime-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "failed-runtime");
        try
        {
            await File.WriteAllTextAsync(executable, "#!/bin/sh\necho 'EACCES permission denied secret-not-for-ui' >&2\nexit 37\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var source = new CopilotScenarioSource(executable, directory);
            var error = await Assert.ThrowsAsync<CopilotGenerationException>(() => source.VerifyConnectionAsync(null, timeout.Token));
            Assert.Contains("runtime startup", error.Message);
            Assert.Contains("permission denied", error.Message);
            Assert.NotNull(error.InnerException);
            Assert.NotNull(error.Diagnostic);
            Assert.DoesNotContain("secret-not-for-ui", error.Message);
            Assert.DoesNotContain("secret-not-for-ui", error.Diagnostic);
            var initial = AuthoredMatchRecording.Create();
            var service = new GenerationService(source, initial, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
            await service.GenerateAsync(null, timeout.Token);
            Assert.Equal(GenerationState.Failed, service.State);
            Assert.Same(initial, service.Current);
            Assert.Same(initial.Commentary, service.Current.Commentary);
            Assert.Contains("runtime startup", service.Status);
            Assert.DoesNotContain("secret-not-for-ui", service.Status);
        }
        finally
        {
            File.Delete(executable);
            Directory.Delete(directory);
        }
    }
}
