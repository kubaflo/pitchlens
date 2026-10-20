using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using PitchLens.Core;
using Page = PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests
{
    public sealed class DirectorRenderingTests
    {
        [Fact]
        public async Task Actual_Razor_film_evidence_unlocks_only_at_hold_and_leaves_at_exact_canonical_event_without_AI()
        {
            var source = new DirectorTests.Source();
            using var native = new MatchService(new DirectorTests.NoScenario(), new DirectorTests.NoMoment(), (_, _) => Task.CompletedTask, source);
            native.Command("seek", 300);
            var activation = new Capture();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(activation).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.DirectorPreviewForTest());
            native.FilmCommand("toggle"); native.FilmCommand("seek", 8);
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.RefreshDirectorForTest());
            Assert.DoesNotContain("Inspect this scene", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Throws<InvalidOperationException>(() => native.CurrentFilmEvidence());
            native.FilmCommand("seek", 9);
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.RefreshDirectorForTest());
            Assert.Contains("Inspect this scene", await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Equal("e24", native.CurrentFilmEvidence().EventId);
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.InspectFilmForTest());
            Assert.False(native.FilmActive);
            Assert.Equal(78, native.Sample(true)!.Frame.Snapshot.Time);
            Assert.False(native.Sample(true)!.Frame.Playing);
            Assert.Equal(0, source.Calls);
        }

        [Fact]
        public async Task Actual_periodic_tick_renders_film_end_once_even_if_playing_became_false_before_tick()
        {
            var clock = new FakeClock();
            using var native = new MatchService(new DirectorTests.NoScenario(), new DirectorTests.NoMoment(), (_, _) => Task.CompletedTask,
                new DirectorTests.Source(), clock);
            native.Command("seek", 300);
            var activation = new Capture();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(activation).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.DirectorPreviewForTest());
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.StartDirectorTickForTest());
            clock.Seconds = 58;
            Assert.Equal(58, native.FilmFrame!.FilmTime);
            Assert.False(native.FilmPlaying);
            await Task.Delay(400);
            var ended = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Play film", ended);
            Assert.DoesNotContain("Pause film", ended);
            Assert.Contains("Film complete.", ended);
            Assert.Contains("value=\"58\"", ended);
            var sequence = await renderer.Dispatcher.InvokeAsync(() => activation.Page!.DirectorSequenceForTest);
            await Task.Delay(600);
            Assert.Equal(sequence, await renderer.Dispatcher.InvokeAsync(() => activation.Page!.DirectorSequenceForTest));
        }
        private sealed class FakeClock : IMonotonicClock { public double Seconds { get; set; } }
        [Fact]
        public async Task Explicit_recorded_preview_is_honestly_labelled_and_never_calls_editorial_source()
        {
            var source = new DirectorTests.Source();
            using var native = new MatchService(new DirectorTests.NoScenario(), new DirectorTests.NoMoment(), (_, _) => Task.CompletedTask, source);
            native.Command("seek", 300);
            var activation = new Capture();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(activation).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(() => activation.Page!.DirectorPreviewForTest());
            Assert.True(native.FilmActive); Assert.False(native.Director.Current!.Fresh);
            Assert.Equal(0, source.Calls);
            var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("RECORDED SDK / NOT FRESH", html);
            Assert.Contains("No AI call", html);
            Assert.Contains("Return to paused match", html);
        }
        [Fact]
        public async Task Actual_Razor_director_action_starts_once_shows_paused_activity_and_enters_explicit_film()
        {
            var source = new DirectorTests.Source();
            using var native = new MatchService(new DirectorTests.NoScenario(), new DirectorTests.NoMoment(), (_, _) => Task.CompletedTask, source);
            native.Command("seek", 300);
            var activation = new Capture();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(activation).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            Task? click = null;
            await renderer.Dispatcher.InvokeAsync(() => { click = activation.Page!.DirectorClickForTest(); });
            Assert.Equal(1, source.Calls);
            Assert.True(native.Director.Busy);
            Assert.False(native.Sample(true)!.Frame.Playing);
            var waiting = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-busy=\"true\"", waiting);
            Assert.Contains("Sol is choosing observed clips", waiting);
            Task? again = null;
            await renderer.Dispatcher.InvokeAsync(() => { again = activation.Page!.DirectorClickForTest(); });
            Assert.Equal(1, source.Calls);
            source.Release.SetResult(); await click!; await again!;
            Assert.True(native.FilmActive);
            var film = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Return to paused match", film);
            Assert.Contains("SYNTHETIC IN-APP FILM", film);
            Assert.Contains("player lenses require leaving film", film);
            native.FilmCommand("exit");
            Assert.Equal(300, native.Sample(true)!.Frame.Snapshot.Time);
        }
        private sealed class Capture : IComponentActivator
        {
            public Page? Page;
            public IComponent CreateInstance(Type type)
            { var value = (IComponent)Activator.CreateInstance(type)!; if (value is Page p) Page = p; return value; }
        }
        private sealed class NoJs : IJSRuntime
        {
            public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => throw new InvalidOperationException("No UI launch.");
            public ValueTask<T> InvokeAsync<T>(string id, CancellationToken token, object?[]? args) => InvokeAsync<T>(id,args);
        }
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public Task DirectorClickForTest() => OpenDirector();
        public Task DirectorPreviewForTest() => PreviewDirector();
        public Task InspectFilmForTest() => InspectFilmScene();
        public Task RefreshDirectorForTest() => Refresh(true);
        public void StartDirectorTickForTest() => ticker = Tick();
        public long DirectorSequenceForTest => frame!.Sequence;
    }
}
