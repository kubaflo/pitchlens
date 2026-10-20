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
    public sealed class DirectorRenderingTests
    {
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
            Assert.Contains("Player lenses and main replay controls are unavailable", film);
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
    }
}
