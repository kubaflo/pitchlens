using GitHub.Copilot;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class ModelSelectionTests
{
    [Fact]
    public void Actual_session_configuration_explicitly_requests_Sol_with_existing_reasoning()
    {
        var config = CopilotScenarioSource.CreateSessionConfig();
        Assert.Equal("gpt-6.1-sol", config.Model);
        Assert.Equal("medium", config.ReasoningEffort);
        Assert.Empty(config.AvailableTools!);
    }

    [Fact]
    public void Availability_check_does_not_substitute_another_GPT_model()
    {
        var error = Assert.Throws<CopilotGenerationException>(() =>
            CopilotScenarioSource.RequireModel([new ModelInfo { Id = "gpt-5.4" }]));
        Assert.Contains("gpt-6.1-sol", error.Message);
        Assert.Contains("No model fallback", error.Message);
    }

    [Fact]
    public void Exact_enabled_model_is_accepted_and_disabled_model_is_rejected()
    {
        var model = new ModelInfo { Id = "gpt-6.1-sol", Policy = new ModelPolicy { State = "enabled" } };
        Assert.Same(model, CopilotScenarioSource.RequireModel([model]));
        model.Policy.State = "disabled";
        Assert.Throws<CopilotGenerationException>(() => CopilotScenarioSource.RequireModel([model]));
    }

    [Fact]
    public void Recorded_example_retains_its_own_model_identity()
    {
        var service = new GenerationService(new NoRequest(), RecordedScenario.Create(), ScenarioOrigin.RecordedSdkExample);
        Assert.Equal(RecordedScenario.Model, service.CurrentModel);
        Assert.Contains(RecordedScenario.Model, service.Status);
        Assert.False(service.IsSdkGenerated);
    }

    private sealed class NoRequest : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No request should be made.");
    }
}
