using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using Page = PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests
{
    public sealed class OneClickMomentTests
    {
        [Fact]
        public async Task Actual_Razor_failure_reopen_exposes_explicit_retry_without_automatically_spending_again()
        {
            var source = new ControlledMoment { Outcome = "error" }; source.Release.SetResult();
            using var native = Native(source); native.Command("seek", 27.8);
            var activator = new CaptureActivator();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJavaScript()).AddSingleton<IComponentActivator>(activator).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(() => activator.Page!.ClickExplainForTest());
            Assert.Equal(1, source.Calls);
            var failed = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Controlled transport failure", failed);
            Assert.Contains("Retry current moment", failed);
            Assert.DoesNotContain("Ask Sol", failed, StringComparison.OrdinalIgnoreCase);
            await renderer.Dispatcher.InvokeAsync(() => activator.Page!.ClickExplainForTest());
            await renderer.Dispatcher.InvokeAsync(() => activator.Page!.RefreshForTest());
            Assert.Equal(1, source.Calls);
            source.Outcome = "success";
            await renderer.Dispatcher.InvokeAsync(() => activator.Page!.ClickRetryForTest());
            Assert.Equal(2, source.Calls);
            Assert.Contains("Fresh Sol", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        }
        [Fact]
        public async Task Actual_Razor_click_starts_exactly_one_request_opens_immediate_paused_activity_and_has_no_second_action()
        {
            var source = new ControlledMoment();
            using var native = Native(source);
            native.Command("seek", 27.8);
            var activator = new CaptureActivator();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJavaScript()).AddSingleton<IComponentActivator>(activator).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            var initial = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Uses Copilot usage", initial);
            Assert.DoesNotContain("Ask Sol", initial, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("moment-event", initial);
            Assert.DoesNotContain("Choose an observed moment and ask", initial);
            Assert.DoesNotContain("Recorded goal analysis", initial);
            Task? click = null;
            await renderer.Dispatcher.InvokeAsync(() => { click = activator.Page!.ClickExplainForTest(); });
            Assert.Equal(1, source.Calls);
            Assert.True(native.Explainer.Busy);
            Assert.True(activator.Page!.MomentPopupRequestedForTest);
            Assert.False(native.Sample(true)!.Frame.Playing);
            Assert.Equal(27.8, source.Evidence!.Cutoff);
            var captured = native.ExplanationSnapshot;
            Task? reopen = null;
            await renderer.Dispatcher.InvokeAsync(() => { reopen = activator.Page.ClickExplainForTest(); });
            Assert.Same(captured, native.ExplanationSnapshot);
            Assert.Equal(1, source.Calls);
            var waiting = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-busy=\"true\"", waiting);
            Assert.Contains("Controlled Sol thinking", waiting);
            Assert.Contains("Captured", waiting);
            source.Release.SetResult();
            await click!; await reopen!;
            var completed = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Fresh Sol", completed);
            Assert.Contains("Evidence, on the pitch.", completed);
            Assert.Equal(captured!.Hash, native.Explainer.Current!.Evidence.Hash);
            await renderer.Dispatcher.InvokeAsync(() => activator.Page.ClickExplainForTest());
            await renderer.Dispatcher.InvokeAsync(() => activator.Page.RefreshForTest());
            Assert.Equal(1, source.Calls);
            Assert.True(native.Explainer.Current.Cached);
            Assert.Contains("Cached Sol / Exact snapshot", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            await renderer.Dispatcher.InvokeAsync(() => activator.Studio!.SwitchAudienceForTest(true));
            await renderer.Dispatcher.InvokeAsync(() => activator.Studio!.SwitchAudienceForTest(false));
            Assert.Equal(1, source.Calls);
        }

        [Fact]
        public async Task Different_snapshot_requests_once_and_recorded_answer_is_never_an_automatic_fresh_substitute()
        {
            var source = new ControlledMoment(); source.Release.SetResult();
            using var native = Native(source);
            native.Command("seek", 86); native.LoadRecordedMoment();
            Assert.False(native.Explainer.Current!.Fresh);
            await native.ExplainCurrentMomentAsync();
            Assert.Equal(1, source.Calls); Assert.True(native.Explainer.Current!.Fresh); Assert.False(native.Explainer.Current.Cached);
            await native.ExplainCurrentMomentAsync();
            Assert.Equal(1, source.Calls); Assert.True(native.Explainer.Current.Cached);
            native.Command("seek", 86.1);
            await native.ExplainCurrentMomentAsync();
            Assert.Equal(2, source.Calls); Assert.Equal(86.1, native.Explainer.Current.Evidence.Cutoff);
        }

        [Theory]
        [InlineData("error")]
        [InlineData("cancel")]
        [InlineData("timeout")]
        public async Task Failed_same_snapshot_reopen_does_not_retry_until_explicit_retry_and_keeps_earlier_answer(string outcome)
        {
            var source = new ControlledMoment(); source.Release.SetResult();
            using var native = Native(source);
            native.Command("seek", 27.8);
            await native.ExplainCurrentMomentAsync();
            var earlier = native.Explainer.Current;
            native.Command("seek", 28);
            source.Outcome = outcome;
            if (outcome == "cancel")
            {
                source.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = native.ExplainCurrentMomentAsync();
                native.Explainer.Cancel(); await pending;
                source.Release.SetResult();
            }
            else await native.ExplainCurrentMomentAsync();
            Assert.Same(earlier, native.Explainer.Current);
            Assert.False(native.Explainer.Activity.Snapshot.Active);
            Assert.Equal(2, source.Calls);
            await native.ExplainCurrentMomentAsync();
            Assert.Equal(2, source.Calls);
            source.Outcome = "success";
            await native.ExplainCurrentMomentAsync(retry: true);
            Assert.Equal(3, source.Calls);
            Assert.Equal(28, native.Explainer.Current!.Evidence.Cutoff);
        }

        [Fact]
        public async Task Blank_creation_and_connection_guards_are_visible_and_issue_no_model_request()
        {
            var source = new ControlledMoment();
            var scenario = new ControlledScenario();
            var check = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var native = new MatchService(scenario, source, (_, _) => check.Task);
            Assert.False(native.CanExplainCurrent);
            await native.ExplainCurrentMomentAsync();
            Assert.Contains("No observed", native.MomentIntentNotice);
            Assert.Equal(0, source.Calls);
            native.Command("seek", 27.8);
            var connection = native.CheckConnectionAsync();
            Assert.False(native.CanExplainCurrent);
            await native.ExplainCurrentMomentAsync();
            Assert.Contains("connection check", native.MomentIntentNotice);
            check.SetResult(); await connection;
            var creating = native.GenerateAsync();
            Assert.False(native.CanExplainCurrent);
            await native.ExplainCurrentMomentAsync();
            Assert.Contains("Match creation", native.MomentIntentNotice);
            native.Cancel(); scenario.Release.SetCanceled(); await creating;
            Assert.Equal(0, source.Calls);
        }

        [Fact]
        public async Task Rapid_parallel_intents_have_one_admission_and_unload_or_scene_change_cannot_publish_late_result()
        {
            var source = new ControlledMoment();
            var native = Native(source); native.Command("seek", 27.8);
            var admissions = Enumerable.Range(0, 12).Select(_ => Task.Factory.StartNew(() => native.ExplainCurrentMomentAsync())).ToArray();
            var requests = await Task.WhenAll(admissions);
            Assert.Equal(1, source.Calls);
            native.Dispose();
            await Task.WhenAll(requests);
            Assert.Null(native.Explainer.Current);
            Assert.False(native.Explainer.Busy);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => native.ExplainCurrentMomentAsync());
            source = new ControlledMoment();
            using var next = Native(source); next.Command("seek", 27.8);
            var pending = next.ExplainCurrentMomentAsync();
            next.Explainer.ChangeScene(2);
            await pending;
            Assert.Null(next.Explainer.Current);
            Assert.False(next.Explainer.Activity.Snapshot.Active);
        }

        private static MatchService Native(IMomentSource source) => new(new ControlledScenario(), source, (_, _) => Task.CompletedTask);
        private sealed class ControlledScenario : IScenarioSource
        {
            public TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) => Release.Task;
        }
        private sealed class ControlledMoment : IMomentSource
        {
            public int Calls { get; private set; }
            public MomentEvidence? Evidence { get; private set; }
            public string Outcome { get; set; } = "success";
            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Release { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) => ExplainAsync(evidence, token, null);
            public async Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token, IProgress<RequestProgress>? progress)
            {
                Calls++; Evidence = evidence;
                progress?.Report(new(RequestStage.Thinking, "Controlled Sol thinking"));
                Entered.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(token);
                    if (Outcome == "error") throw new IOException("Controlled transport failure");
                    if (Outcome == "timeout") throw new OperationCanceledException("Controlled timeout");
                    return JsonSerializer.Serialize(new MomentAnswer(evidence.Hash,
                        [new("The observed move links the recorded actions.", [evidence.FocusId])],
                        [new("The recorded sequence suggests another passing route.", [evidence.FocusId])]), MomentJsonContext.Default.MomentAnswer);
                }
                finally { progress?.Report(new(RequestStage.Cleanup, "Controlled owned cleanup")); }
            }
        }
        private sealed class CaptureActivator : IComponentActivator
        {
            public Page? Page { get; private set; }
            public Components.MomentStudio? Studio { get; private set; }
            public IComponent CreateInstance(Type type)
            {
                var component = (IComponent)(Activator.CreateInstance(type) ?? throw new InvalidOperationException("Component constructor missing."));
                if (component is Page page) Page = page;
                if (component is Components.MomentStudio studio) Studio = studio;
                return component;
            }
        }
        private sealed class NoJavaScript : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("No browser or native UI in this test.");
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        }
    }
}

namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public Task ClickExplainForTest() => OpenMoment();
        public Task ClickRetryForTest() => RetryMoment();
        public bool MomentPopupRequestedForTest => pendingPopup?.Id == "moment-dialog";
        public Task RefreshForTest() => Refresh(true);
    }
    public partial class MomentStudio
    {
        public void SwitchAudienceForTest(bool value) { analyst = value; StateHasChanged(); }
    }
}
