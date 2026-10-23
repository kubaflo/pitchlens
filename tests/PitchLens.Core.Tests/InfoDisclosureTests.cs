using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using PitchLens.Core.Tests.Components;

namespace PitchLens.Core.Tests;

public sealed class InfoDisclosureTests
{
    [Fact]
    public async Task Measure_actual_production_screens_without_SDK_calls()
    {
        var forbidden = new MatchLensTests.Forbidden();
        using var native = new MatchService(forbidden, forbidden, (_, _) => throw new InvalidOperationException("No auth"), forbidden,
            mode: SessionMode.Live, readyDemo: false);
        await native.ChooseModeAsync(SessionMode.Live);
        using var services = new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var screens = new Dictionary<string, string>();
        async Task<string> Render<T>(Dictionary<string, object?>? parameters = null) where T : IComponent =>
            await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(
                ParameterView.FromDictionary(parameters ?? []))).ToHtmlString());
        screens["startup-manual"] = await Render<MatchView>();
        native.SetFixtureChoice(FixtureChoice.Random);
        screens["startup-random"] = await Render<MatchView>();
        screens["camera"] = await Render<Components.ViewSettings>(new() { ["SelectedPlayer"] = AuthoredMatchRecording.Create().Players[0] });
        screens["reading"] = await Render<ReadingPreferences>();
        await native.ChooseModeAsync(SessionMode.Test);
        screens["replay"] = await Render<MatchView>();
        var source = new ExplanationTeamSource(new ExplanationTeamTests.Roles());
        var evidence = ExplanationTeamTests.Evidence();
        var response = await source.ExplainAsync(evidence, CancellationToken.None);
        var report = source.Collaboration!;
        var pending = report with { Approved = false, Checks = [], Stages = [report.Stages[0] with { Status = "Working", Responses = 0, OutputHash = null }] };
        screens["team-pending"] = await Render<AgentCollaboration>(new() { ["Report"] = pending });
        var result = new ExplainedMoment(evidence, evidence.Validate(response), report.Stages.Sum(s => s.ToolCalls), Collaboration: report);
        screens["team-approved-answer"] = await Render<MomentStudio>(new() { ["Result"] = result });
        var counts = screens.ToDictionary(s => s.Key, s => Regex.Matches(VisibleText(s.Value,
            s.Key == "replay" ? "settings-dialog" : s.Key == "camera" ? "view-dialog" : s.Key == "reading" ? "reading-dialog" : null), @"[\p{L}\p{N}]+").Count);
        // Measured with this renderer/visibility policy on production baseline 551088c.
        var before = new Dictionary<string, int> { ["startup-manual"] = 49, ["startup-random"] = 55, ["camera"] = 110,
            ["reading"] = 59, ["replay"] = 312, ["team-pending"] = 128, ["team-approved-answer"] = 246 };
        foreach (var screen in counts)
            Assert.True(screen.Value <= before[screen.Key] * .75, $"{screen.Key}: {screen.Value} words, baseline {before[screen.Key]}; require at least 25% less.");
        foreach (var (name, markup) in screens)
        {
            var visible = VisibleText(markup, name == "replay" ? "settings-dialog" : name == "camera" ? "view-dialog" : name == "reading" ? "reading-dialog" : null);
            foreach (System.Text.RegularExpressions.Match content in Regex.Matches(markup, @"<span id=""info-[^""]+"" class=""info-content""[^>]* hidden>([\s\S]*?)</span>"))
                Assert.DoesNotContain(WebUtility.HtmlDecode(Regex.Replace(content.Groups[1].Value, "<[^>]+>", "")).Trim(), visible);
            foreach (System.Text.RegularExpressions.Match button in Regex.Matches(markup, @"<button type=""button"" class=""info-button""[^>]+>"))
            {
                Assert.Contains("aria-expanded=\"false\"", button.Value);
                Assert.Contains("aria-label=\"About ", button.Value);
                Assert.DoesNotContain("aria-haspopup", button.Value);
                var id = Regex.Match(button.Value, @"aria-controls=""([^""]+)""").Groups[1].Value;
                Assert.Contains($"id=\"{id}\" class=\"info-content\" popover=\"manual\" role=\"note\"", markup);
                Assert.Contains("class=\"info-close\" data-info-close", markup);
            }
        }
        Assert.Contains("Synthetic match", VisibleText(screens["startup-manual"]));
        Assert.Contains("Uses Copilot usage", VisibleText(screens["startup-random"]));
        Assert.Contains("Up to 3 responses", VisibleText(screens["startup-manual"]));
        Assert.Contains(result.Answer.Fan[0].Text, VisibleText(screens["team-approved-answer"]));
        Assert.Contains("Fresh Sol", VisibleText(screens["team-approved-answer"]));
        Assert.Contains("Review accepted", VisibleText(screens["team-approved-answer"]));
        Assert.Contains("Watch this evidence", VisibleText(screens["team-approved-answer"]));
        Assert.Contains("Not started", VisibleText(screens["team-pending"]));
        Assert.DoesNotContain("Review accepted", VisibleText(screens["team-pending"]));
        Assert.Contains("Not fresh", VisibleText(screens["replay"], "settings-dialog"));
        if (Environment.GetEnvironmentVariable("PITCHLENS_INFO_MEASURE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var screen in screens) await File.WriteAllTextAsync(Path.Combine(folder, screen.Key + ".html"), screen.Value);
            await File.WriteAllTextAsync(Path.Combine(folder, "counts.json"), System.Text.Json.JsonSerializer.Serialize(counts));
            foreach (var screen in screens)
                foreach (var percent in new[] { 100, 150, 200 })
                    foreach (var expanded in new[] { false, true })
                    {
                        var markup = screen.Value;
                        var readingCss = new ReadingSettings(Percent: percent).Css;
                        markup = Regex.Replace(markup, @"<style id=""reading-style"">[\s\S]*?</style>", $"<style id=\"reading-style\">{readingCss}</style>");
                        var dialog = screen.Key == "replay" ? "settings-dialog" : screen.Key == "camera" ? "view-dialog" : screen.Key == "reading" ? "reading-dialog" : null;
                        if (dialog is not null) markup = Regex.Replace(markup, $@"<dialog id=""{dialog}""", $"<dialog open id=\"{dialog}\"");
                        var document = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                            "<title>PitchLens production disclosure preview</title><link rel=\"stylesheet\" href=\"app.css\"><style>" + readingCss +
                            "</style></head><body data-info-preview=\"" + (expanded ? "open" : "closed") + "\"><p class=\"caption\">Production Razor / controlled team fixture. " + percent + "% / popup " + (expanded ? "open" : "closed") +
                            ". Popup handlers exercised by offscreen harness; native bridge disconnected. Not native or pixel certification.</p>" + markup + "</body></html>";
                        await File.WriteAllTextAsync(Path.Combine(folder, $"{screen.Key}-{percent}-{(expanded ? "open" : "closed")}.html"), document);
                    }
        }
        Assert.Equal(0, forbidden.Calls);
        Assert.All(counts.Values, count => Assert.True(count > 0));
    }

    [Fact]
    public async Task Errors_and_active_request_remain_visible_outside_help()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent =>
            await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        var fixture = await Render<FixturePicker>(new() { ["Error"] = "Choose two different clubs." });
        Assert.Contains("Choose two different clubs.", VisibleText(fixture));
        Assert.Contains("role=\"alert\"", fixture);
        var camera = await Render<Components.ViewSettings>(new() { ["SelectedPlayer"] = AuthoredMatchRecording.Create().Players[0], ["Notice"] = "Save failed." });
        Assert.Contains("Save failed.", VisibleText(camera, "view-dialog"));
        var reading = await Render<ReadingPreferences>(new() { ["Notice"] = "Invalid saved reading size." });
        Assert.Contains("Invalid saved reading size.", VisibleText(reading, "reading-dialog"));
        var activity = new RequestActivity();
        var id = activity.Begin("Waiting for evidence");
        var request = await Render<RequestActivityView>(new() { ["Activity"] = activity, ["Title"] = "Explain this moment", ["Budget"] = "270s whole run" });
        var visible = VisibleText(request);
        Assert.Contains("Waiting for evidence", visible); Assert.Contains("Cancel", visible); Assert.Contains("0:00", visible);
        Assert.DoesNotContain("270s", visible);
        activity.Finish(id, RequestStage.Failed, "Evidence rejected. Previous answer kept.");
        request = await Render<RequestActivityView>(new() { ["Activity"] = activity, ["Title"] = "Explain this moment", ["Budget"] = "270s whole run" });
        Assert.Contains("Evidence rejected. Previous answer kept.", VisibleText(request));
        Assert.Contains("role=\"alert\"", request);
    }

    [Fact]
    public void Visibility_counter_ignores_closed_guidance_but_not_open_help_errors_or_selected_values()
    {
        const string html = "<style>not text</style><p>Result</p><details><summary>Details</summary>Hidden help</details>" +
            "<span hidden>Hidden explanation</span><span aria-hidden=\"true\">i</span><p role=\"alert\">Error</p>" +
            "<select value=\"b\"><option value=\"a\">Not selected</option><option value=\"b\">Chosen</option></select>" +
            "<details open><summary>Open</summary>Visible help</details><dialog id=\"test\"><p>Dialog</p></dialog>";
        Assert.Equal("Result Details Error Chosen Open Visible help", VisibleText(html));
        Assert.Equal("Dialog", VisibleText(html, "test"));
    }

    // Count rendered text, not HTML bytes: closed dialogs/details/help, SVG titles,
    // unselected options and accessibility-only/icon text are not default-visible prose.
    internal static string VisibleText(string html, string? dialog = null)
    {
        var text = new StringBuilder();
        var stack = new Stack<(string Tag, bool Visible, bool Active, bool Closed, string? Selection)>();
        stack.Push(("", true, dialog is null, false, null));
        foreach (System.Text.RegularExpressions.Match token in Regex.Matches(html, @"<!--[\s\S]*?-->|<[^>]+>|[^<]+"))
        {
            var value = token.Value;
            var parent = stack.Peek();
            if (!value.StartsWith('<'))
            {
                if (parent.Visible && parent.Active && !parent.Closed) text.Append(' ').Append(WebUtility.HtmlDecode(value));
                continue;
            }
            if (value.StartsWith("<!--")) continue;
            var tag = Regex.Match(value, @"^</?([\w-]+)").Groups[1].Value.ToLowerInvariant();
            if (value.StartsWith("</"))
            {
                if (stack.Count > 1 && stack.Peek().Tag == tag) stack.Pop();
                continue;
            }
            string? Attribute(string name)
            {
                var match = Regex.Match(value, $@"\s{Regex.Escape(name)}(?:=""([^""]*)""|'([^']*)'|=([^\s>]+))?(?=[\s/>])");
                return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value :
                    match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value) : null;
            }
            var active = parent.Active || Attribute("id") == dialog;
            var visible = parent.Visible && (!parent.Closed || tag == "summary") &&
                Attribute("hidden") is null && Attribute("aria-hidden") != "true" &&
                !(Attribute("class") ?? "").Split(' ').Contains("visually-hidden") &&
                tag is not ("style" or "script" or "svg" or "title") &&
                (tag != "dialog" || Attribute("id") == dialog || Attribute("open") is not null) &&
                (tag != "option" || Attribute("selected") is not null || Attribute("value") == parent.Selection);
            if (tag is not ("input" or "img" or "br" or "hr" or "meta" or "link" or "source") && !value.EndsWith("/>"))
                stack.Push((tag, visible, active, tag == "details" && Attribute("open") is null, tag == "select" ? Attribute("value") : parent.Selection));
        }
        return Regex.Replace(text.ToString(), @"\s+", " ").Trim();
    }

    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => throw new InvalidOperationException("No browser");
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object?[]? args) => InvokeAsync<T>(id, args);
    }
}
