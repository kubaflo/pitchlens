using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests
{
    public sealed class ModeTests
    {
        [Fact]
        public async Task Actual_Razor_first_choice_Test_opens_instantly_and_all_paid_sources_remain_unreachable()
        {
            var sdk=new Forbidden();SessionMode? saved=null;
            using var native=new MatchService(sdk,sdk,(_,_)=>throw new InvalidOperationException("No connection calls"),sdk,
                readyDemo:false,mode:SessionMode.Unchosen,saveMode:m=>saved=m);
            var capture=new Capture();
            using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs())
                .AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.FirstPaintForFreshTest());
            Assert.Equal(0,sdk.Calls);Assert.False(native.HasMatch);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ChooseTestForModeTest());
            Assert.Equal(SessionMode.Test,saved);Assert.True(native.HasMatch);Assert.False(native.Fresh);
            Assert.Equal(2,native.Match.Commentary!.Contract);Assert.Equal(87,native.Match.Commentary.Cues.Count);
            await native.GenerateAsync();await native.ExplainCurrentMomentAsync();await native.DirectCurrentAsync();await native.CheckConnectionAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>native.ExplainAsync());
            native.Command("seek",86);await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ClickExplainForTest());
            Assert.False(native.Explainer.Current!.Fresh);
            native.Command("seek",300);native.PreviewRecordedDirector();native.FilmCommand("exit");
            Assert.False(native.Director.Current!.Fresh);Assert.Equal(0,sdk.Calls);
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("TEST",html);Assert.Contains("No AI calls",html);
            Assert.DoesNotContain("Listen",html);Assert.DoesNotContain("Voice",html);Assert.Contains(">CC</button>",html);
        }
        [Fact]
        public async Task Remembered_Test_has_zero_requests_and_settings_switch_preserves_current_game()
        {
            var sdk=new Forbidden();
            using var native=new MatchService(sdk,sdk,(_,_)=>throw new InvalidOperationException(),sdk,readyDemo:false,mode:SessionMode.Test);
            await native.StartSessionAsync();var match=native.Match;native.Command("seek",120);
            await native.ChooseModeAsync(SessionMode.Live,false);Assert.Same(match,native.Match);
            Assert.Equal(120,native.Sample(true)!.Frame.Snapshot.Time);
            await native.ChooseModeAsync(SessionMode.Test,false);await native.StartSessionAsync();
            Assert.Same(match,native.Match);Assert.Equal(0,sdk.Calls);
        }
        [Fact]
        public async Task Explicit_Live_choice_starts_once_then_Test_keeps_fresh_game_without_further_calls()
        {
            var source=new PendingScenario();var sdk=new Forbidden();
            using var native=new MatchService(source,sdk,(_,_)=>Task.CompletedTask,sdk,readyDemo:false,mode:SessionMode.Unchosen);
            await native.StartSessionAsync();Assert.Equal(0,source.Calls);
            var first=native.ChooseModeAsync(SessionMode.Live);var again=native.ChooseModeAsync(SessionMode.Live);
            Assert.Equal(1,source.Calls);source.Reply.SetResult(VarietyTests.OneSided());await first;await again;
            var match=native.Match;Assert.True(native.Fresh);native.Command("seek",150);
            await native.ChooseModeAsync(SessionMode.Test,false);await native.GenerateAsync();
            Assert.Same(match,native.Match);Assert.Equal(150,native.Sample(true)!.Frame.Snapshot.Time);Assert.Equal(1,source.Calls);
        }
        private sealed class Forbidden:IScenarioSource,IMomentSource,IDirectorSource
        {
            public int Calls;
            public Task<string> GenerateAsync(IProgress<string>?p,CancellationToken t){Calls++;throw new InvalidOperationException("Forbidden SDK");}
            public Task<string> ExplainAsync(MomentEvidence e,CancellationToken t){Calls++;throw new InvalidOperationException("Forbidden SDK");}
            public Task<string> CreateAsync(DirectorEvidence e,IProgress<string>?p,CancellationToken t){Calls++;throw new InvalidOperationException("Forbidden SDK");}
        }
        private sealed class PendingScenario:IScenarioSource
        {
            public int Calls;public TaskCompletionSource<string> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<string> GenerateAsync(IProgress<string>?p,CancellationToken t){Calls++;return Reply.Task;}
        }
        private sealed class Capture:IComponentActivator
        {public Page? Page;public IComponent CreateInstance(Type t){var c=(IComponent)Activator.CreateInstance(t)!;if(c is Page p)Page=p;return c;}}
        private sealed class NoJs:IJSRuntime
        {public ValueTask<T> InvokeAsync<T>(string id,object?[]?args)=>throw new InvalidOperationException("No UI");public ValueTask<T> InvokeAsync<T>(string id,CancellationToken t,object?[]?args)=>InvokeAsync<T>(id,args);}
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView {public Task ChooseTestForModeTest()=>StartTest();}
}
