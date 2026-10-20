using System.Text.Json;
using Microsoft.Extensions.AI;
using GitHub.Copilot;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class RequestProgressTests
{
    [Fact]
    public void Observable_sdk_turn_event_only_signals_activity_never_reasoning_text_or_early_success()
    {
        var update = CopilotMomentSource.ProgressForEvent(new AssistantTurnStartEvent { Data = new() { TurnId = "test", Model = CopilotScenarioSource.Model } }, 3);
        Assert.NotNull(update);
        Assert.Equal(RequestStage.Thinking, update.Stage);
        Assert.Equal(3, update.ToolCalls);
        Assert.Null(CopilotMomentSource.ProgressForEvent(new AssistantReasoningDeltaEvent { Data = new() { ReasoningId = "test", DeltaContent = "not UI content" } }, 3));
        Assert.Null(CopilotMomentSource.ProgressForEvent(new SessionIdleEvent { Data = new() }, 3));
    }
    [Fact]
    public void Monotonic_activity_is_immediate_freezes_at_end_and_ignores_old_or_late_callbacks()
    {
        var clock = new Clock();
        var activity = new RequestActivity(clock);
        var changes = 0; activity.Changed += () => changes++;
        var old = activity.Begin("Connecting");
        Assert.True(activity.Snapshot.Active); Assert.Equal(1, changes);
        clock.Seconds = 4.25;
        Assert.Equal(4.25, activity.Snapshot.ElapsedSeconds);
        activity.Report(old, new(RequestStage.Evidence, "Inspecting observed evidence", 2));
        activity.Report(old, new(RequestStage.Thinking, "Sol working"));
        Assert.Equal(2, activity.Snapshot.ToolCalls);
        activity.Finish(old, RequestStage.Complete, "Validated");
        clock.Seconds = 100;
        Assert.Equal(4.25, activity.Snapshot.ElapsedSeconds);
        activity.Report(old, new(RequestStage.Thinking, "Late update"));
        Assert.Equal("Validated", activity.Snapshot.Message);
        var current = activity.Begin("Another request");
        activity.Report(old, new(RequestStage.Failed, "Wrong scene"));
        activity.Finish(old, RequestStage.Complete, "Wrong request");
        Assert.True(activity.Snapshot.Active);
        Assert.Equal("Another request", activity.Snapshot.Message);
        activity.Finish(current, RequestStage.Cancelled, "Cancelled");
        Assert.False(activity.Snapshot.Active);
        activity.Reset();
        activity.Report(current, new(RequestStage.Thinking, "Old request"));
        Assert.Equal("", activity.Snapshot.Message);
        Assert.Equal(0, activity.Snapshot.ElapsedSeconds);
    }

    [Fact]
    public async Task Real_tool_handler_emits_only_actual_safe_evidence_activity()
    {
        var evidence = MomentRecording.Load(AuthoredMatchRecording.Create(), 1, 86).Evidence;
        using var stop = new CancellationTokenSource();
        var events = new List<RequestProgress>();
        var tools = new MomentTools(evidence, stop, new InlineProgress<RequestProgress>(events.Add));
        Assert.Empty(events);
        await tools.Index.InvokeAsync(new());
        await tools.Event.InvokeAsync(new() { ["eventId"] = "e26" });
        Assert.Equal(new[] { 1, 2 }, events.Select(e => e.ToolCalls));
        Assert.All(events, e => Assert.Equal(RequestStage.Evidence, e.Stage));
        Assert.Contains("e26", events[1].Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.Event.InvokeAsync(new() { ["eventId"] = "future" }).AsTask());
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task Paused_native_bridge_notifies_busy_immediately_and_keeps_stages_local_until_validated_cleanup()
    {
        var source = new DelayedMoment();
        using var native = new MatchService(new NeverScenario(), source, (_, _) => Task.CompletedTask);
        native.Command("seek", 86); native.CaptureMoment();
        var notifications = 0; native.Changed += () => notifications++;
        var stages = new List<RequestStage>();
        native.Explainer.Activity.Changed += () => stages.Add(native.Explainer.Activity.Snapshot.Stage);
        var pending = native.ExplainAsync();
        Assert.True(native.Explainer.Busy);
        Assert.True(native.Explainer.Activity.Snapshot.Active);
        Assert.Equal(1, notifications);
        Assert.False(native.Sample(true)!.Frame.Playing);
        source.Progress!.Report(new(RequestStage.Thinking, "Sol working"));
        source.Progress.Report(new(RequestStage.Evidence, "Inspecting captured event", 1));
        Assert.Equal(1, notifications);
        Assert.Null(native.Explainer.Current);
        source.Response.SetResult(Answer(source.Evidence!));
        await source.CleanupStarted.Task;
        Assert.True(native.Explainer.Busy);
        Assert.Equal(RequestStage.Cleanup, native.Explainer.Activity.Snapshot.Stage);
        Assert.Null(native.Explainer.Current);
        source.Cleanup.SetResult();
        await pending;
        Assert.NotNull(native.Explainer.Current);
        Assert.False(native.Explainer.Activity.Snapshot.Active);
        Assert.Equal(RequestStage.Complete, native.Explainer.Activity.Snapshot.Stage);
        Assert.Contains(RequestStage.Validating, stages);
        Assert.Equal(2, notifications);
        source.Progress.Report(new(RequestStage.Thinking, "Late stale update"));
        Assert.Equal(RequestStage.Complete, native.Explainer.Activity.Snapshot.Stage);
        Assert.Equal(86, native.Sample(true)!.Frame.Snapshot.Time);
    }

    [Fact]
    public async Task Cancel_failure_and_scene_replacement_stop_activity_without_replacing_earlier_answer()
    {
        var recording = MomentRecording.Load(AuthoredMatchRecording.Create(), 1, 86);
        var source = new DelayedMoment();
        var service = new MomentExplanationService(source); service.ChangeScene(1); service.UseRecorded(recording);
        var observedStages = new List<RequestStage>();
        service.Activity.Changed += () => observedStages.Add(service.Activity.Snapshot.Stage);
        var pending = service.GenerateAsync(recording.Evidence, CancellationToken.None);
        service.Cancel();
        Assert.Contains(RequestStage.Cancelling, observedStages);
        await source.CleanupStarted.Task;
        Assert.Equal(RequestStage.Cleanup, service.Activity.Snapshot.Stage);
        source.Cleanup.SetResult(); await pending;
        Assert.False(service.Activity.Snapshot.Active); Assert.Equal(RequestStage.Cancelled, service.Activity.Snapshot.Stage);
        Assert.Same(recording, service.Current);
        source = new DelayedMoment();
        service = new(source); service.ChangeScene(1); service.UseRecorded(recording);
        pending = service.GenerateAsync(recording.Evidence, CancellationToken.None);
        source.Response.SetException(new IOException("Controlled transport failure"));
        await source.CleanupStarted.Task; source.Cleanup.SetResult(); await pending;
        Assert.Equal(RequestStage.Failed, service.Activity.Snapshot.Stage); Assert.Same(recording, service.Current);
        source = new DelayedMoment(); service = new(source); service.ChangeScene(1);
        pending = service.GenerateAsync(recording.Evidence, CancellationToken.None);
        service.ChangeScene(2);
        source.Progress!.Report(new(RequestStage.Thinking, "Old scene"));
        await source.CleanupStarted.Task; source.Cleanup.SetResult(); await pending;
        Assert.False(service.Activity.Snapshot.Active); Assert.Null(service.Current);
        Assert.DoesNotContain("Old scene", service.Activity.Snapshot.Message);
    }

    [Fact]
    public async Task Native_generation_and_non_ai_connection_check_publish_request_scoped_activity_while_paused()
    {
        var scenario = new DelayedScenario();
        var connection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<string>? connectionProgress = null;
        using var native = new MatchService(scenario, new DelayedMoment(), (progress, _) => { connectionProgress = progress; return connection.Task; });
        var check = native.CheckConnectionAsync();
        Assert.True(native.ConnectionActivity.Snapshot.Active);
        connectionProgress!.Report("Checking Copilot authentication…");
        Assert.Equal(RequestStage.Authenticating, native.ConnectionActivity.Snapshot.Stage);
        connection.SetResult(); await check;
        Assert.False(native.ConnectionActivity.Snapshot.Active);
        var create = native.GenerateAsync();
        Assert.True(native.GenerationActivity.Snapshot.Active);
        Assert.False(native.Sample(true)!.Frame.Playing);
        foreach (var message in new[] { "Sol is authoring paths…", "AI refining rejected player paths…", "Sol is composing grounded commentator subtitles…", "Cleaning up the SDK session…", "Validating all 22 paths…" })
            scenario.Progress!.Report(message);
        Assert.Equal(RequestStage.Validating, native.GenerationActivity.Snapshot.Stage);
        native.Cancel(); scenario.Response.SetCanceled(); await create;
        Assert.False(native.GenerationActivity.Snapshot.Active);
        Assert.Equal(RequestStage.Cancelled, native.GenerationActivity.Snapshot.Stage);
        scenario.Progress!.Report("Late progress");
        Assert.Equal(RequestStage.Cancelled, native.GenerationActivity.Snapshot.Stage);
    }

    private static string Answer(MomentEvidence e) => JsonSerializer.Serialize(new MomentAnswer(e.Hash,
        [new("The recorded move links the passing sequence.", [e.FocusId])],
        [new("The observed order suggests an attacking connection.", [e.FocusId])]), MomentJsonContext.Default.MomentAnswer);
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
    private sealed class NeverScenario : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) => throw new InvalidOperationException("No model requests in this test.");
    }
    private sealed class DelayedScenario : IScenarioSource
    {
        public IProgress<string>? Progress { get; private set; }
        public TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) { Progress = progress; return Response.Task; }
    }
    private sealed class DelayedMoment : IMomentSource
    {
        public MomentEvidence? Evidence { get; private set; }
        public IProgress<RequestProgress>? Progress { get; private set; }
        public TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CleanupStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> ExplainAsync(MomentEvidence e, CancellationToken token) => ExplainAsync(e, token, null);
        public async Task<string> ExplainAsync(MomentEvidence e, CancellationToken token, IProgress<RequestProgress>? progress)
        {
            Evidence = e; Progress = progress;
            progress?.Report(new(RequestStage.Connecting, "Connecting"));
            try { return await Response.Task.WaitAsync(token); }
            finally { progress?.Report(new(RequestStage.Cleanup, "Cleaning owned runtime")); CleanupStarted.SetResult(); await Cleanup.Task; }
        }
    }
}
