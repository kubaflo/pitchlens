using System.Text.Json;
using Microsoft.Extensions.AI;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class MomentExplanationTests
{
    private static MomentEvidence Capture(long revision = 1)
    {
        var match = AuthoredMatchRecording.Create();
        return MomentEvidence.Capture(match, revision, 86, match.Events.First(e => e.Kind == EventKind.Goal).Id);
    }
    private static string Answer(MomentEvidence evidence) => JsonSerializer.Serialize(new MomentAnswer(evidence.Hash,
        [new("The passing move ends with the scorer finding the net.", [evidence.FocusId])],
        [new("The recorded sequence suggests a useful attacking connection.", [evidence.FocusId])]),
        MomentJsonContext.Default.MomentAnswer);

    [Fact]
    public void Immutable_capture_has_exact_partial_metrics_clipped_paths_and_no_future_events()
    {
        var match = AuthoredMatchRecording.Create();
        var captured = MomentEvidence.Capture(match, 7, 85.5);
        Assert.Equal(7, captured.Revision);
        Assert.All(captured.Facts.Events, e => { Assert.True(e.Time <= 85.5); Assert.All(e.Ball, p => Assert.True(p.Time <= e.Time)); });
        Assert.DoesNotContain(captured.Facts.Events, e => e.Kind == "Goal");
        foreach (var metric in captured.Facts.Players)
        {
            var stats = Replay.At(match, 85.5).Players.Single(p => p.PlayerId == metric.PlayerId);
            Assert.Equal(stats.Passes, metric.Passes);
            Assert.Equal(stats.CompletedPasses, metric.CompletedPasses);
            Assert.Equal(stats.Distance, metric.DistanceMetres, 10);
            Assert.Equal(stats.MaxSpeed, metric.MaxSpeedMetresPerSecond, 10);
            Assert.Equal(85.5, metric.Path[^1].Time);
            Assert.All(metric.Path, p => Assert.True(p.Time <= 85.5));
        }
        var hash = captured.Hash;
        captured.Facts.Events[0].Ball[0] = new(300, 0, 0);
        Assert.Equal(hash, captured.Hash);
        Assert.All(captured.Facts.Events[0].Ball, p => Assert.True(p.Time <= 85.5));
        Assert.Equal(hash, MomentEvidence.Capture(match, 7, 85.5).Hash);
        Assert.NotEqual(hash, MomentEvidence.Capture(match, 8, 85.5).Hash);
        Assert.Throws<InvalidOperationException>(() => MomentEvidence.Capture(match, 1, 85.5,
            match.Events.First(e => e.Kind == EventKind.Goal).Id));
    }

    [Fact]
    public void Validation_resolves_known_ids_and_rejects_unknown_future_hash_markup_numbers_and_duplicate_fields()
    {
        var evidence = Capture();
        var raw = Answer(evidence);
        Assert.Equal(evidence.Hash, evidence.Validate(raw).SnapshotHash);
        Assert.Throws<ScenarioValidationException>(() => evidence.Validate(raw.Replace(evidence.FocusId, "future-goal")));
        Assert.Throws<ScenarioValidationException>(() => evidence.Validate(raw.Replace(evidence.Hash, "wrong")));
        foreach (var text in new[] { "<script>alert()</script>", "He scored twice with 2 shots.", "The pressure guaranteed this." })
        {
            var answer = new MomentAnswer(evidence.Hash, [new(text, [evidence.FocusId])], [new(text, [evidence.FocusId])]);
            Assert.Throws<ScenarioValidationException>(() => evidence.Validate(JsonSerializer.Serialize(answer, MomentJsonContext.Default.MomentAnswer)));
        }
        Assert.Throws<ScenarioValidationException>(() => evidence.Validate(raw.Replace("\"fan\":", "\"unknown\":0,\"fan\":")));
        Assert.Throws<ScenarioValidationException>(() => evidence.Validate(raw.Replace("\"fan\":", "\"snapshotHash\":\"other\",\"fan\":")));
    }

    [Fact]
    public async Task Two_read_only_tools_enforce_known_evidence_strict_arguments_and_six_call_budget()
    {
        var evidence = Capture();
        using var cancellation = new CancellationTokenSource();
        var tools = new MomentTools(evidence, cancellation);
        var config = CopilotMomentSource.CreateConfig(tools);
        Assert.Equal(CopilotScenarioSource.Model, config.Model);
        Assert.Equal(new[] { "observed_match", "observed_event" }, config.AvailableTools);
        Assert.Equal(2, config.Tools!.Count);
        Assert.False(config.EnableConfigDiscovery);
        Assert.Empty(CopilotScenarioSource.CreateSessionConfig().AvailableTools!);
        var index = Assert.IsType<JsonElement>(await tools.Index.InvokeAsync(new()));
        Assert.True(index.GetProperty("events").GetArrayLength() <= 12);
        var result = Assert.IsType<JsonElement>(await tools.Event.InvokeAsync(new() { ["eventId"] = evidence.FocusId }));
        Assert.Equal(evidence.FocusId, result.GetProperty("id").GetString());
        Assert.True(tools.ReadIndex); Assert.True(tools.ReadFocus);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.Event.InvokeAsync(new() { ["eventId"] = "future" }).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.Index.InvokeAsync(new() { ["path"] = "/private" }).AsTask());
        await tools.Index.InvokeAsync(new()); await tools.Index.InvokeAsync(new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.Index.InvokeAsync(new()).AsTask());
        Assert.True(cancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task Both_modes_are_returned_once_and_invalid_followup_retains_exact_last_valid()
    {
        var evidence = Capture();
        var source = new Source((e, _) => Task.FromResult(Answer(e)));
        var service = new MomentExplanationService(source);
        service.ChangeScene(1);
        await service.GenerateAsync(evidence, CancellationToken.None);
        var valid = service.Current;
        Assert.NotNull(valid);
        _ = valid.Answer.Fan; _ = valid.Answer.Analyst; _ = valid.Answer.Fan;
        Assert.Equal(1, source.Requests);
        source.Run = (_, _) => Task.FromResult("{}");
        await service.GenerateAsync(evidence, CancellationToken.None);
        Assert.Same(valid, service.Current);
        Assert.Contains("failed", service.Status);
        Assert.Equal(2, source.Requests);
    }

    [Fact]
    public async Task Real_recording_is_hash_matched_observed_only_and_never_labelled_fresh_or_calls_source()
    {
        var match = AuthoredMatchRecording.Create();
        Assert.True(MomentRecording.Available(match, 1));
        Assert.False(MomentRecording.Available(match, 2));
        Assert.Throws<InvalidOperationException>(() => MomentRecording.Load(match, 1, 85.9));
        var recorded = MomentRecording.Load(match, 1, 86);
        Assert.False(recorded.Fresh);
        Assert.Equal(MomentRecording.Hash, recorded.Evidence.Hash);
        Assert.Equal(2, recorded.Answer.Fan.Length); Assert.Equal(3, recorded.Answer.Analyst.Length);
        var scorer = recorded.Evidence.Facts.Players.Single(p => p.PlayerId == "arsenal-gyokeres");
        Assert.Equal(1, scorer.Goals); Assert.Equal(1, scorer.Shots);
        Assert.Equal(0, scorer.Passes); Assert.Equal(0, scorer.CompletedPasses);
        Assert.Equal(76.22988864371342, scorer.DistanceMetres, 10);
        Assert.Equal(1.25, scorer.MaxSpeedMetresPerSecond, 10);
        var source = new Source((_, _) => throw new InvalidOperationException("Must not request a model."));
        var service = new MomentExplanationService(source);
        service.ChangeScene(1); service.UseRecorded(recorded);
        Assert.Same(recorded, service.Current);
        Assert.Null(service.VisibleAt(1, 85.9));
        Assert.Null(service.VisibleAt(2, 86));
        Assert.Same(recorded, service.VisibleAt(1, 86));
        Assert.Contains("Not a fresh", service.Status); Assert.Equal(0, source.Requests);
        source.Run = (_, _) => throw new CopilotGenerationException("Authentication unavailable.");
        await service.GenerateAsync(Capture(), CancellationToken.None);
        Assert.Same(recorded, service.Current);
        Assert.Contains("Authentication unavailable", service.Status);
        Assert.Equal(1, source.Requests);
        source.Run = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return ""; };
        using var cancel = new CancellationTokenSource();
        var pending = service.GenerateAsync(Capture(), cancel.Token);
        cancel.Cancel(); await pending;
        Assert.Same(recorded, service.Current);
    }

    [Fact]
    public async Task Cancellation_timeout_and_stale_scene_never_publish_or_retry()
    {
        var evidence = Capture();
        var source = new Source(async (e, token) => { await Task.Delay(Timeout.Infinite, token); return Answer(e); });
        var service = new MomentExplanationService(source, TimeSpan.FromMilliseconds(20));
        service.ChangeScene(1);
        await service.GenerateAsync(evidence, CancellationToken.None);
        Assert.Null(service.Current); Assert.Contains("timed out", service.Status);
        Assert.False(service.Activity.Snapshot.Active);
        Assert.Equal(RequestStage.Failed, service.Activity.Snapshot.Stage);
        using var cancel = new CancellationTokenSource();
        var pending = service.GenerateAsync(evidence, cancel.Token);
        cancel.Cancel(); await pending;
        Assert.Null(service.Current); Assert.Contains("cancelled", service.Status);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Run = (_, _) => completion.Task;
        pending = service.GenerateAsync(evidence, CancellationToken.None);
        service.ChangeScene(2);
        completion.SetResult(Answer(evidence)); await pending;
        Assert.Null(service.Current);
        Assert.Contains("New match", service.Status);
        Assert.False(service.Busy); Assert.Equal(3, source.Requests);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(evidence, CancellationToken.None));
    }

    private sealed class Source(Func<MomentEvidence, CancellationToken, Task<string>> run) : IMomentSource
    {
        public int Requests { get; private set; }
        public Func<MomentEvidence, CancellationToken, Task<string>> Run { get; set; } = run;
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) { Requests++; return Run(evidence, token); }
    }
}
