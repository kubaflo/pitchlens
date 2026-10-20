using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PitchLens.Core;
using PitchLens.Generation;
using PitchLens.Core.Tests.Components;

namespace PitchLens.Core.Tests;

public sealed class InsightRenderingTests
{
    [Fact]
    public async Task Narrative_cards_render_only_observed_AI_interpretations_and_reuse_actual_evidence_map()
    {
        var original = AuthoredMatchRecording.Create();
        var match = NarrationPrompt.Attach(NarrationFixtures.Package(original), original);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        async Task<string> Render(double time, ExplanationMode mode) => await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<NarrativeStory>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Match"] = match, ["Revision"] = 1L, ["Cutoff"] = time, ["Score"] = Replay.At(match, time).Score, ["Mode"] = mode }));
            return root.ToHtmlString();
        });
        var before = await Render(85.9, ExplanationMode.Casual);
        Assert.DoesNotContain("Takes the lead", before);
        Assert.DoesNotContain("Level again", before);
        Assert.Contains("Recorded AI story / Not fresh", before);
        Assert.Contains("viewBox=\"-4 -3 113 74\"", before);
        var goal = await Render(86, ExplanationMode.Casual);
        Assert.Contains("Takes the lead", goal);
        Assert.DoesNotContain("Level again", goal);
        Assert.Contains("Replay selected evidence", System.Net.WebUtility.HtmlDecode(goal));
        Assert.Contains("The recorded sequence connects the move.", goal);
        var analyst = await Render(176, ExplanationMode.Analyst);
        Assert.Contains("Level again", analyst);
        Assert.Contains("The observed route suggests", analyst);
        Assert.DoesNotContain("The recorded sequence connects the move.", analyst);
        var backward = await Render(0, ExplanationMode.Casual);
        Assert.DoesNotContain("narrative-card", backward);
        Assert.DoesNotContain("Takes the lead", backward);
    }

    [Fact]
    public async Task Real_studio_renders_accessible_original_svg_histogram_and_expandable_safe_prose_without_requests()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var recorded = MomentRecording.Load(AuthoredMatchRecording.Create(), 1, 86);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MomentStudio>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Result"] = recorded, ["Pending"] = true }));
            return root.ToHtmlString();
        });
        Assert.Contains("viewBox=\"-4 -3 113 74\"", html);
        Assert.Contains("Recorded Sol / Not fresh", html);
        Assert.Contains("Earlier answer", html);
        Assert.Contains("8 measured", html);
        Assert.Contains(17.042944713347826.ToString("F1") + " m", html);
        Assert.Contains("30&#x2B; m, 1 pass attempts", html);
        Assert.Contains("Watch this evidence", html);
        Assert.Contains("Keyboard-accessible sequence selection", html);
        Assert.Contains("Full original AI explanation", html);
        Assert.Contains("Match-to-cutoff player totals", html);
        Assert.Contains(recorded.Answer.Fan[0].Text, System.Net.WebUtility.HtmlDecode(html));
        var hostile = recorded with { Answer = recorded.Answer with { Fan = [new("<script>alert('not executed')</script>", ["e28"])] } };
        var safe = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<MomentStudio>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Result"] = hostile }));
            return root.ToHtmlString();
        });
        Assert.DoesNotContain("<script>", safe);
        Assert.Contains("&lt;script&gt;", safe);
    }

    [Fact]
    public async Task Actual_activity_component_updates_stage_and_elapsed_while_replay_is_paused_and_stops_at_terminal()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var clock = new Clock();
        var activity = new RequestActivity(clock);
        var id = activity.Begin("Connecting to SDK");
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<RequestActivityView>(
            ParameterView.FromDictionary(new Dictionary<string, object?>
            { ["Activity"] = activity, ["Title"] = "Sol analysis", ["Budget"] = "270s response budget · Up to 6 read-only calls" })));
        var initial = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("aria-busy=\"true\"", initial);
        Assert.Contains("Connecting to SDK", initial);
        activity.Report(id, new(RequestStage.Thinking, "Sol is working"));
        clock.Seconds = 2;
        await Task.Delay(1150);
        var thinking = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Sol is working", thinking);
        Assert.Contains("0:02", thinking);
        activity.Report(id, new(RequestStage.Evidence, "Inspecting e26", 2));
        var inspecting = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Inspecting e26", inspecting);
        Assert.Contains("2 observed tool calls", inspecting);
        activity.Finish(id, RequestStage.Failed, "Controlled error. Previous answer kept.");
        var ended = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("aria-busy=\"false\"", ended);
        Assert.DoesNotContain("is-active", ended);
        Assert.Contains("role=\"alert\"", ended);
        clock.Seconds = 80; activity.Report(id, new(RequestStage.Thinking, "Late"));
        Assert.Equal(2, activity.Snapshot.ElapsedSeconds);
        Assert.DoesNotContain("Late", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
    }
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
}
