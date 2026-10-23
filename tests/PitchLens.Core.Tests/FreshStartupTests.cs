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
            await startup!;
            Assert.Equal(0,source.Calls);
            if(!chooseDemo){var request=native.SubmitFixtureAsync();source.Reply.SetResult(VarietyTests.OneSided());await request;}
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
            Assert.Equal(chooseDemo?0:1,source.Calls);
        }
        [Fact]
        public async Task Actual_after_first_paint_waits_for_Create_hides_recorded_match_and_Newmatch_reopens_picker()
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
            Assert.Contains("Choose your teams",before);
            Assert.DoesNotContain("Choose your clubs before creating",InfoDisclosureTests.VisibleText(before));
            Assert.DoesNotContain("id=\"pitch\"",before);
            Assert.DoesNotContain("Recorded SDK example",before);
            Assert.False(native.HasMatch);
            await renderer.Dispatcher.InvokeAsync(() => capture.Page!.FirstPaintForFreshTest());
            Assert.Equal(0,source.Calls);Assert.False(native.GenerationActivity.Snapshot.Active);
            Task? first=null;
            await renderer.Dispatcher.InvokeAsync(()=>{first=capture.Page!.CreateFixtureForFreshTest();});
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
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.NewMatchForFreshTest());
            Assert.Equal(1,source.Calls);Assert.True(native.FixturePickerOpen);
            await renderer.Dispatcher.InvokeAsync(() => { next=capture.Page!.CreateFixtureForFreshTest(); });
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
            await native.StartSessionAsync();var pending=native.SubmitFixtureAsync();
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

        [Theory]
        [InlineData(false,false)]
        [InlineData(true,false)]
        [InlineData(false,true)]
        [InlineData(true,true)]
        public async Task Failed_no_match_to_explicit_recorded_demo_recontexts_terminal_activity_and_retains_request_facts(bool useTest,bool cancel)
        {
            var source=new Delayed();using var native=Native(source);
            var capture=new Capture();
            using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs())
                .AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            await native.StartSessionAsync();var pending=native.SubmitFixtureAsync();
            Assert.Throws<InvalidOperationException>(native.PlayRecordedDemo);
            Assert.True(native.GenerationActivity.Snapshot.Active);Assert.False(native.HasMatch);
            if(cancel){native.Cancel();source.Reply.SetCanceled();}
            else source.Reply.SetException(new CopilotGenerationException("Controlled turnover phase lacks interception."));
            await pending;
            var outcome=cancel?GenerationState.Cancelled:GenerationState.Failed;
            Assert.Equal(outcome,native.RequestState);Assert.False(native.HasMatch);
            Assert.Contains("No active match",native.Status);
            Assert.Contains("No active match",native.GenerationActivity.Snapshot.Message);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshFreshTest());
            Assert.Contains("No active match",await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            await renderer.Dispatcher.InvokeAsync(async()=>{
                if(useTest)await native.ChooseModeAsync(SessionMode.Test,false);
                else native.PlayRecordedDemo();
                await capture.Page!.RefreshFreshTest();
            });
            Assert.True(native.HasMatch);Assert.False(native.Fresh);Assert.Equal(outcome,native.RequestState);
            Assert.Equal(ScenarioOrigin.RecordedSdkExample,native.Origin);
            Assert.True(native.RecordedDemoAfterGenerationIssue);
            Assert.NotNull(native.PriorGenerationIssue);
            Assert.Equal(outcome,native.PriorGenerationIssue.State);
            Assert.Contains(cancel?"Generation cancelled.":"Controlled turnover phase lacks interception.",native.PriorGenerationIssue.Message);
            Assert.DoesNotContain("No active match",native.PriorGenerationIssue.Message);
            Assert.DoesNotContain("Previous match kept",native.PriorGenerationIssue.Message);
            Assert.False(native.GenerationActivity.Snapshot.Active);
            Assert.Equal(RequestStage.Complete,native.GenerationActivity.Snapshot.Stage);
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("id=\"pitch\"",html);Assert.Contains("Recorded demo active",html);
            Assert.Contains("id=\"earlier-generation-issue\"",html);
            Assert.Contains("Earlier AI creation",html);
            Assert.DoesNotContain("No active match",html);
            Assert.DoesNotContain("choose the recorded demo",html);
            Assert.DoesNotContain("Creation failed. Previous match kept.",html);
            Assert.DoesNotContain("Sol &#xB7; Creating your match</strong>",html);
            Assert.Equal(1,source.Calls);
            native.Command("seek",300);native.Command("restart");await native.StartSessionAsync();
            if(useTest)await native.GenerateAsync();
            Assert.Equal(1,source.Calls);
            var prior=native.PriorGenerationIssue;native.PlayRecordedDemo();
            Assert.Equal(prior,native.PriorGenerationIssue); // Opening the demo again does not reset elapsed request history.
            await native.ChooseModeAsync(SessionMode.Live,false);
            source.Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
            var retry=native.GenerateAsync();
            Assert.False(native.RecordedDemoAfterGenerationIssue);
            Assert.True(native.GenerationActivity.Snapshot.Active);Assert.True(native.HasMatch);
            Assert.Equal(prior,native.PriorGenerationIssue);
            Assert.Throws<InvalidOperationException>(native.PlayRecordedDemo);
            source.Reply.SetException(new CopilotGenerationException("Controlled later failure."));
            await retry;
            Assert.Equal(2,source.Calls);Assert.True(native.HasMatch);
            Assert.Equal(GenerationState.Failed,native.RequestState);
            Assert.Equal(RequestStage.Failed,native.GenerationActivity.Snapshot.Stage);
            Assert.Contains("Controlled later failure.",native.GenerationActivity.Snapshot.Message);
            Assert.DoesNotContain("No active match",native.GenerationActivity.Snapshot.Message);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshFreshTest());
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Controlled later failure.",html);
            Assert.Contains("Creation failed. Previous match kept.",html);
        }

        [Fact]
        public async Task Unload_cancels_first_request_and_late_candidate_does_not_deliver()
        {
            var source=new Delayed();var native=Native(source);
            await native.StartSessionAsync();var request=native.SubmitFixtureAsync();native.Dispose();
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
        public Task CreateFixtureForFreshTest()=>CreateFixture();
    }
}
