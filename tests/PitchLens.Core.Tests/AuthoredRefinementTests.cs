using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class AuthoredRefinementTests
{
    private static string Rejected() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rejected-depth-paths.json"));
    private static string Correction() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-depth-correction.json"));
    private static string Commentary() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "corrected-commentary.json"));

    [Fact]
    public async Task Actual_saved_original_red_to_AI_corrected_green_commits_atomically_in_at_most_three_responses()
    {
        var initial = AuthoredMatchRecording.Create();
        var original = AuthoredMatchPlan.Document(Rejected());
        var correctedDocument = AuthoredPathRefinement.Apply(original, Correction());
        var expected = AuthoredMatchPlan.Build(correctedDocument, false);
        Assert.Equal("71995829a1fab9c301437c5a54d18de7ca9367125b189fe81e14b37129b8fca9", CommentaryPrompt.Hash(expected));
        var paths = 0; var refinements = 0; var captions = 0;
        GenerationService? service = null;
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => { paths++; return Task.FromResult(Rejected()); },
            (document, failure, _) =>
            {
                refinements++;
                Assert.Equal(GenerationState.Generating, service!.State);
                Assert.Same(initial, service.Current);
                Assert.Equal(original.Events, document.Events);
                Assert.Equal(13, failure.Issues.Count);
                return Task.FromResult(Correction());
            },
            (match, _) =>
            {
                captions++;
                Assert.Equal(GenerationState.Generating, service!.State);
                Assert.Same(initial, service.Current);
                Assert.Equal(expected.Players.SelectMany(p => p.Path.Frames), match.Players.SelectMany(p => p.Path.Frames));
                Assert.Equal(CommentaryPrompt.Hash(expected), CommentaryPrompt.Hash(match));
                return Task.FromResult(Commentary());
            }, null, token));
        service = new(source, initial, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Succeeded, service.State);
        Assert.Equal(ScenarioOrigin.FreshSdkGeneration, service.Origin);
        Assert.Equal("gpt-6.1-sol", service.CurrentModel);
        Assert.Equal(1, paths); Assert.Equal(1, refinements); Assert.Equal(1, captions);
        Assert.Equal(812, service.Current.Players.Sum(p => p.Path.Frames.Count));
        Assert.Equal(74, service.Current.Events.Count);
        Assert.Equal(original.Events.Select(e => (e.Id, e.Time, e.ActorId, e.ReceiverId, e.Completed)),
            service.Current.Events.Select(e => (e.Id, e.Time, e.ActorId, e.ReceiverId, e.Completed)));
        Assert.Equal(42, service.Current.Commentary!.Cues.Count);
        foreach (var time in new[] { 300d, 150, 0, 126, 246, 0 })
        {
            Assert.All(Replay.At(service.Current, time).Events, e => Assert.True(e.Time <= time));
            Assert.All(StoryReplay.At(service.Current, time), e => Assert.True(e.Time <= time));
        }
    }

    [Fact]
    public async Task Valid_first_response_needs_no_correction_and_only_two_model_responses()
    {
        var refinements = 0; var captions = 0;
        var json = await AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(AuthoredMatchRecording.Json()),
            (_, _, _) => { refinements++; throw new InvalidOperationException("Valid paths must not need correction."); },
            (_, _) => { captions++; return Task.FromResult(AuthoredMatchRecording.CommentaryJson()); },
            null, CancellationToken.None);
        Assert.Equal(0, refinements); Assert.Equal(1, captions);
        Assert.Equal(22, AuthoredMatchPlan.Parse(json).Players.Count);
    }

    [Fact]
    public async Task Invalid_caption_after_valid_correction_keeps_old_match_and_never_retries()
    {
        var refinements = 0; var captions = 0;
        var initial = AuthoredMatchRecording.Create();
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(Rejected()),
            (_, _, _) => { refinements++; return Task.FromResult(Correction()); },
            (_, _) => { captions++; return Task.FromResult(Commentary().Replace("71995829", "bad-hash")); },
            null, token));
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Same(initial, service.Current);
        Assert.Equal(1, refinements); Assert.Equal(1, captions);
    }

    [Fact]
    public async Task Correction_timeout_is_visible_and_does_not_call_another_stage()
    {
        var refinements = 0; var captions = 0;
        var initial = AuthoredMatchRecording.Create();
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(Rejected()),
            (_, _, _) => { refinements++; return Task.FromException<string>(new TimeoutException("AI correction response timed out.")); },
            (_, _) => { captions++; return Task.FromResult("{}"); }, null, token));
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Contains("timed out", service.Status);
        Assert.Same(initial, service.Current);
        Assert.Equal(1, refinements); Assert.Equal(0, captions);
    }

    [Fact]
    public void Actual_saved_failure_remains_rejected_and_reports_all_original_geometry_violations()
    {
        var document = AuthoredMatchPlan.Document(Rejected());
        Assert.Equal(812, document.Players.Sum(p => p.Frames.Length));
        Assert.Equal(74, document.Events.Length);
        var failure = Assert.Throws<AuthoredPathValidationException>(() => AuthoredMatchPlan.Build(document, false));
        Assert.Equal(13, failure.Issues.Count);
        Assert.Contains(failure.Issues, p => p.TeamId == "arsenal" && p.Time == 150 && p.Depth == 20);
        Assert.Contains(failure.Issues, p => p.TeamId == "manchester-city" && p.Time == 252 && p.Depth == 21);
    }

    [Fact]
    public async Task Invalid_correction_never_retries_or_calls_commentary_and_keeps_last_valid_match()
    {
        var planCalls = 0; var correctionCalls = 0; var commentaryCalls = 0;
        var initial = AuthoredMatchRecording.Create();
        var messages = new Messages();
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => { planCalls++; return Task.FromResult(Rejected()); },
            (document, failure, _) =>
            {
                correctionCalls++;
                Assert.Equal(13, failure.Issues.Count);
                return Task.FromResult(JsonSerializer.Serialize(new AuthoredPathCorrection(document.Players), CorrectionJsonContext.Default.AuthoredPathCorrection));
            },
            (_, _) => { commentaryCalls++; throw new InvalidOperationException("Invalid paths must not reach commentary."); },
            messages, token));
        var service = new GenerationService(source, initial, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        await service.GenerateAsync(messages, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Equal(1, planCalls); Assert.Equal(1, correctionCalls); Assert.Equal(0, commentaryCalls);
        Assert.Same(initial, service.Current);
        Assert.Same(initial.Commentary, service.Current.Commentary);
        Assert.Contains(messages.Items, text => text.Contains("AI refining"));
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("transport")]
    public async Task Connection_failure_does_not_trigger_any_model_correction(string failure)
    {
        var corrections = 0; var captions = 0;
        await Assert.ThrowsAsync<CopilotGenerationException>(() => AuthoredGenerationPipeline.RunAsync(
            _ => throw new CopilotGenerationException(failure),
            (_, _, _) => { corrections++; return Task.FromResult("{}"); },
            (_, _) => { captions++; return Task.FromResult("{}"); }, null, CancellationToken.None));
        Assert.Equal(0, corrections); Assert.Equal(0, captions);
    }

    [Fact]
    public async Task Cancelling_during_correction_drops_candidate_and_does_not_start_commentary()
    {
        using var cancellation = new CancellationTokenSource();
        var corrections = 0; var captions = 0;
        var initial = AuthoredMatchRecording.Create();
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(Rejected()),
            (document, _, _) =>
            {
                corrections++; cancellation.Cancel();
                return Task.FromResult(JsonSerializer.Serialize(new AuthoredPathCorrection(document.Players), CorrectionJsonContext.Default.AuthoredPathCorrection));
            },
            (_, _) => { captions++; return Task.FromResult("{}"); }, null, token));
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, cancellation.Token);
        Assert.Equal(GenerationState.Cancelled, service.State);
        Assert.Same(initial, service.Current);
        Assert.Equal(1, corrections); Assert.Equal(0, captions);
    }

    [Fact]
    public void Correction_response_cannot_modify_events_duration_roster_metadata_or_contain_hidden_extra_fields()
    {
        var original = AuthoredMatchPlan.Document(Rejected());
        var json = JsonSerializer.Serialize(new AuthoredPathCorrection(original.Players), CorrectionJsonContext.Default.AuthoredPathCorrection);
        var corrected = AuthoredPathRefinement.Apply(original, json);
        Assert.Same(original.Events, corrected.Events);
        Assert.Same(original.PhaseKinds, corrected.PhaseKinds);
        Assert.Equal(original.Duration, corrected.Duration);
        Assert.Equal(original.InitialOwnerId, corrected.InitialOwnerId);
        Assert.Throws<ScenarioValidationException>(() => AuthoredPathRefinement.Apply(original, json.Insert(1, "\"events\":[],")));
    }

    [Fact]
    public async Task Event_identity_snapshot_detects_any_correction_adapter_mutation_before_caption_generation()
    {
        var captions = 0;
        var error = await Assert.ThrowsAsync<ScenarioValidationException>(() => AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(Rejected()),
            (document, _, _) =>
            {
                document.Events[0] = document.Events[0] with { Time = document.Events[0].Time + .1 };
                return Task.FromResult(Correction());
            },
            (_, _) => { captions++; return Task.FromResult(Commentary()); }, null, CancellationToken.None));
        Assert.Contains("immutable", error.Message);
        Assert.Equal(0, captions);
    }

    [Theory]
    [InlineData("{\"players\":null}")]
    [InlineData("{\"players\":[null]}")]
    public async Task Missing_repair_players_are_visible_validation_failures_not_unhandled_exceptions(string correction)
    {
        var initial = AuthoredMatchRecording.Create();
        var source = new PipelineSource(token => AuthoredGenerationPipeline.RunAsync(
            _ => Task.FromResult(Rejected()), (_, _, _) => Task.FromResult(correction),
            (_, _) => throw new InvalidOperationException("Malformed repair must not reach commentary."), null, token));
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Same(initial, service.Current);
    }
    private sealed class PipelineSource(Func<CancellationToken, Task<string>> run) : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) => run(cancellationToken);
    }
    private sealed class Messages : IProgress<string>
    {
        public List<string> Items { get; } = [];
        public void Report(string value) => Items.Add(value);
    }
}
