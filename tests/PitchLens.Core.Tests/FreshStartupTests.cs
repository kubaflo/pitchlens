using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using System.Text.Json;
using Page = PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests
{
    public sealed class FreshStartupTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Actual_afterrender_transition_initializes_pitch_once_only_after_new_canvas_exists(bool chooseDemo)
        {
            var source=new Delayed();using var native=Native(source);
            var capture=new Capture();var js=new ControlledJs();
            using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(js)
                .AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            Task? startup=null;
            await renderer.Dispatcher.InvokeAsync(()=>{startup=capture.Page!.FirstPaintForFreshTest();});
            Assert.Equal(0,js.Imports);
            if(chooseDemo){native.Cancel();source.Reply.SetCanceled();}
            else source.Reply.SetResult(VarietyTests.OneSided());
            await startup!;
            if(chooseDemo) native.PlayRecordedDemo();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshFreshTest());
            var ready=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("id=\"pitch\"",ready);js.CanvasExists=true;
            Task? firstRender=null;Task? racingRender=null;
            await renderer.Dispatcher.InvokeAsync(()=>{firstRender=capture.Page!.FirstPaintForFreshTest();});
            await renderer.Dispatcher.InvokeAsync(()=>{racingRender=capture.Page!.FirstPaintForFreshTest();});
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshFreshTest());
            Assert.Equal(1,js.Imports);Assert.Equal(0,js.Module.Initializations);
            js.Ready.SetResult(js.Module);
            await firstRender!;await racingRender!;
            Assert.Equal(1,js.Module.Initializations);
            Assert.Single(js.Module.Revisions);
            Assert.Equal(chooseDemo?1:2,js.Module.Revisions[0]);
            Assert.Equal(22,js.Module.PlayerCounts[0]);
            native.Activate();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.FirstPaintForFreshTest());
            await Task.Delay(350);
            Assert.Equal(1,js.Imports);Assert.Equal(1,js.Module.Initializations);Assert.Single(js.Module.Revisions);
            Assert.Equal(1,source.Calls);
        }
        [Fact]
        public async Task Actual_after_first_paint_intent_starts_once_hides_recorded_match_and_newmatch_requests_directly()
        {
            var source = new Delayed();
            using var native = Native(source);
            var capture = new Capture();
            using var services = new ServiceCollection().AddLogging().AddSingleton(native)
                .AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Page>());
            Assert.Equal(0,source.Calls);
            var before = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Live startup creates a new",before);
            Assert.DoesNotContain("id=\"pitch\"",before);
            Assert.DoesNotContain("Recorded SDK example",before);
            Assert.False(native.HasMatch);
            Task? first = null;
            await renderer.Dispatcher.InvokeAsync(() => { first = capture.Page!.FirstPaintForFreshTest(); });
            Assert.Equal(1,source.Calls); Assert.True(native.GenerationActivity.Snapshot.Active);
            var waiting = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-busy=\"true\"",waiting);
            Assert.DoesNotContain("id=\"pitch\"",waiting);
            await renderer.Dispatcher.InvokeAsync(() => capture.Page!.FirstPaintForFreshTest());
            _ = native.StartSessionAsync(); native.Activate(); native.Suspend(); native.Activate();
            Assert.Equal(1,source.Calls);
            source.Reply.SetResult(VarietyTests.OneSided());
            await first!;
            Assert.True(native.HasMatch); Assert.True(native.Fresh);
            Assert.Equal(1,source.Calls);
            await renderer.Dispatcher.InvokeAsync(() => capture.Page!.RefreshFreshTest());
            var delivered=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("id=\"pitch\"",delivered);
            Assert.Contains("Replay from start",delivered);
            native.Command("restart"); native.Command("play"); native.Command("pause"); native.Command("seek",20);
            Assert.Equal(1,source.Calls);
            source.Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? next=null;
            await renderer.Dispatcher.InvokeAsync(() => { next=capture.Page!.NewMatchForFreshTest(); });
            Assert.Equal(2,source.Calls);
            await native.GenerateAsync(); Assert.Equal(2,source.Calls);
            source.Reply.SetException(new CopilotGenerationException("Controlled second failure"));
            var previous=native.Match; await next!;
            Assert.Same(previous,native.Match); Assert.True(native.HasMatch); Assert.True(native.Fresh);
            await native.StartSessionAsync(); Assert.Equal(2,source.Calls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task First_failure_or_cancel_never_selects_demo_or_retries_but_explicit_demo_is_no_paid(bool cancel)
        {
            var source=new Delayed();using var native=Native(source);
            var pending=native.StartSessionAsync();
            if(cancel){native.Cancel();source.Reply.SetCanceled();}
            else source.Reply.SetException(new CopilotGenerationException("Authentication unavailable"));
            await pending;
            Assert.False(native.HasMatch);Assert.False(native.Fresh);
            Assert.Contains("No active match",native.Status);
            await native.StartSessionAsync();Assert.Equal(1,source.Calls);
            native.PlayRecordedDemo();
            Assert.True(native.HasMatch);Assert.False(native.Fresh);
            Assert.Contains("Explicit recorded",native.Status);Assert.Equal(1,source.Calls);
            await native.StartSessionAsync();Assert.Equal(1,source.Calls);
        }

        [Fact]
        public async Task Unload_cancels_first_request_and_late_candidate_does_not_deliver()
        {
            var source=new Delayed();var native=Native(source);
            var request=native.StartSessionAsync();native.Dispose();
            source.Reply.SetResult(VarietyTests.OneSided());await request;
            Assert.False(native.HasMatch);Assert.False(native.Fresh);
            Assert.Equal(1,source.Calls);
        }
        private static MatchService Native(Delayed source)=>new(source,new DirectorTests.NoMoment(),(_,_)=>Task.CompletedTask,readyDemo:false);
        private sealed class Delayed:IScenarioSource
        {
            public int Calls;
            public TaskCompletionSource<string> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken token){Calls++;p?.Report("Sol authoring a different match");return Reply.Task;}
        }
        private sealed class Capture:IComponentActivator
        {
            public Page? Page;
            public IComponent CreateInstance(Type type){var item=(IComponent)Activator.CreateInstance(type)!;if(item is Page p)Page=p;return item;}
        }
        private sealed class NoJs:IJSRuntime
        {
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>throw new InvalidOperationException("No native/browser launch.");
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class ControlledJs:IJSRuntime
        {
            public int Imports;public bool CanvasExists;
            public TaskCompletionSource<IJSObjectReference> Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ControlledModule Module;
            public ControlledJs()=>Module=new(this);
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)
            {
                Assert.Equal("import",id);Assert.True(CanvasExists);Imports++;
                return new(Import<T>());
            }
            private async Task<T> Import<T>()=>(T)(object)await Ready.Task;
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class ControlledModule(ControlledJs owner):IJSObjectReference
        {
            public int Initializations;public List<long> Revisions=[];public List<int> PlayerCounts=[];
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)
            {
                if(id=="initialize"){Assert.True(owner.CanvasExists);Initializations++;}
                if(id=="loadScenario")
                {
                    Assert.Equal(1,Initializations);
                    using var json=JsonDocument.Parse((string)args![0]!);
                    Revisions.Add(json.RootElement.GetProperty("revision").GetInt64());
                    PlayerCounts.Add(json.RootElement.GetProperty("players").GetArrayLength());
                }
                return ValueTask.FromResult(default(T)!);
            }
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
            public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        }
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public Task FirstPaintForFreshTest()=>OnAfterRenderAsync(true);
        public Task RefreshFreshTest()=>Refresh(true);
        public Task NewMatchForFreshTest()=>Generate();
    }
}
