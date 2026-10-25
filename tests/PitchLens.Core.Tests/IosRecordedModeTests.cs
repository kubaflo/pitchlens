using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using Page = PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests;

public sealed class IosRecordedModeTests
{
    [Theory]
    [InlineData(SessionMode.Live)]
    [InlineData(SessionMode.Unchosen)]
    [InlineData(SessionMode.Test)]
    public async Task Mobile_capability_blocks_every_fresh_entrypoint_without_calling_sources_or_changing_fixture(SessionMode saved)
    {
        var source = new Forbidden();
        using var session = Create(source, saved);
        await session.StartSessionAsync();
        Assert.Equal(SessionMode.Test, session.Mode);
        Assert.False(session.HasMatch);
        var recording = session.Match;
        session.SetFixtureChoice(new FixtureChoice { HomeId = "liverpool", AwayId = "chelsea" });
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(session.SubmitFixtureAsync);
        Assert.Equal(MatchService.MobileBackendRequirement, refused.Message);
        refused = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ChooseModeAsync(SessionMode.Live));
        Assert.Equal(MatchService.MobileBackendRequirement, refused.Message);
        await session.GenerateAsync(FixtureCatalog.Historical);
        await session.ExplainCurrentMomentAsync();
        await session.DirectCurrentAsync();
        await session.CheckConnectionAsync();
        refused = await Assert.ThrowsAsync<InvalidOperationException>(session.ExplainAsync);
        Assert.Equal(MatchService.MobileBackendRequirement, refused.Message);
        Assert.Equal(MatchService.MobileBackendRequirement, session.Status);
        Assert.Equal(MatchService.MobileBackendRequirement, session.MomentIntentNotice);
        Assert.Equal(MatchService.MobileBackendRequirement, session.DirectorNotice);
        Assert.Equal(MatchService.MobileBackendRequirement, session.ConnectionStatus);
        Assert.False(session.HasMatch);
        Assert.Null(session.RequestedFixture);
        Assert.Same(recording, session.Match);
        Assert.Equal(GenerationState.Ready, session.RequestState);
        Assert.False(session.GenerationActivity.Snapshot.Active);
        Assert.False(session.Explainer.Busy);
        Assert.False(session.Director.Busy);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Mobile_recorded_tour_retains_historical_identity_and_supports_analysis_evidence_and_film()
    {
        var source = new Forbidden();
        using var session = Create(source, SessionMode.Live);
        await session.StartSessionAsync();
        session.SetFixtureChoice(new FixtureChoice { HomeId = "liverpool", AwayId = "chelsea" });
        session.StartGuidedDemo();
        Assert.Equal(new[] { "arsenal", "manchester-city" }, session.Match.Teams.Select(t => t.Id));
        Assert.False(session.Fresh);
        Assert.Equal(ScenarioOrigin.RecordedSdkExample, session.Origin);
        Assert.Equal(76, session.Sample(true)!.Frame.Snapshot.Time);
        session.SetGuidedDemoStep(1);
        Assert.False(session.Explainer.Current!.Fresh);
        Assert.Null(session.Explainer.Current.Collaboration);
        Assert.NotEmpty(session.Explainer.Current.Answer.Fan);
        Assert.NotEmpty(session.Explainer.Current.Answer.Analyst);
        session.SetGuidedDemoStep(2);
        var captured = session.ExplanationSnapshot!;
        session.InspectEvidence(captured, captured.FocusId);
        Assert.Equal(captured.Resolve(captured.FocusId).Time, session.Sample(true)!.Frame.Snapshot.Time);
        session.SetGuidedDemoStep(3);
        Assert.True(session.FilmActive);
        Assert.False(session.Director.Current!.Fresh);
        session.FilmCommand("toggle");
        session.FilmCommand("seek", 10);
        Assert.Equal(10, session.FilmFrame!.FilmTime, 1);
        session.ExitGuidedDemo();
        Assert.False(session.FilmActive);
        session.Command("select", id: session.Match.Players.Last().Id);
        session.Command("mode", 1);
        session.Command("seek", 100);
        Assert.Equal(session.Match.Players.Last().Id, session.Sample(true)!.Frame.SelectedId);
        session.Command("restart");
        Assert.Equal(0, session.Sample(true)!.Frame.Snapshot.Time);
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task Actual_mobile_Razor_has_deliberate_recorded_paths_and_no_desktop_login_or_fixture_relabelling()
    {
        var source = new Forbidden();
        using var session = Create(source, SessionMode.Live);
        using var services = new ServiceCollection().AddLogging().AddSingleton(session)
            .AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("Your pocket Matchday", html);
        Assert.Contains(MatchService.MobileBackendRequirement, html);
        Assert.Contains("id=\"startup-test-demo\"", html);
        Assert.Contains("id=\"first-guided-demo\"", html);
        Assert.DoesNotContain("id=\"fixture-home\"", html);
        Assert.DoesNotContain("id=\"fixture-away\"", html);
        Assert.DoesNotContain("id=\"pitch\"", html);
        await renderer.Dispatcher.InvokeAsync(() => session.ChooseModeAsync(SessionMode.Test));
        html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("id=\"pitch\"", html);
        Assert.Contains("class=\"pitch-surface\"", html);
        Assert.Contains("Synthetic &#xB7; Recorded / Offline", html);
        Assert.Contains("id=\"recorded-director-button\"", html);
        Assert.Contains("id=\"ios-generation-boundary\"", html);
        Assert.DoesNotContain("id=\"cli\"", html);
        Assert.DoesNotContain("Use Live mode", html);
        Assert.DoesNotContain("id=\"director-button\"", html);
        Assert.Contains("Arsenal", html);
        Assert.Contains("Manchester City", html);
        Assert.Equal(0, source.Calls);
    }

    private static MatchService Create(Forbidden source, SessionMode mode) =>
        new(source, source, (_, _) => throw new InvalidOperationException("No connection permitted"), source,
            readyDemo: false, mode: mode, liveGenerationAvailable: false);

    private sealed class Forbidden : IScenarioSource, IMomentSource, IDirectorSource
    {
        public int Calls;
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token)
        { Calls++; throw new InvalidOperationException("Unexpected fresh generation"); }
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token)
        { Calls++; throw new InvalidOperationException("Unexpected fresh explanation"); }
        public Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token)
        { Calls++; throw new InvalidOperationException("Unexpected fresh film"); }
    }

    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static Razor render must not launch a browser.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) =>
            InvokeAsync<T>(identifier, args);
    }
}
