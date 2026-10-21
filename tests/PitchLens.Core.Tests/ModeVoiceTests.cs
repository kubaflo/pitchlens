using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests
{
    public sealed class ModeVoiceTests
    {
        [Fact]
        public async Task Actual_Razor_first_paint_requires_choice_and_Test_is_an_instant_no_SDK_boundary()
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
            var chooser=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Test mode",chooser);Assert.Contains("Fresh AI match",chooser);Assert.False(native.HasMatch);Assert.Equal(0,sdk.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ChooseTestForModeTest());
            Assert.Equal(SessionMode.Test,saved);Assert.True(native.HasMatch);Assert.False(native.Fresh);
            Assert.False(native.Voice.Enabled);
            await native.GenerateAsync();await native.ExplainCurrentMomentAsync();await native.DirectCurrentAsync();await native.CheckConnectionAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>native.ExplainAsync());
            Assert.Equal(0,sdk.Calls);
            native.Command("seek",86);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ClickExplainForTest());
            Assert.False(native.Explainer.Current!.Fresh);
            native.Command("seek",300);native.PreviewRecordedDirector();native.FilmCommand("exit");
            Assert.False(native.Director.Current!.Fresh);Assert.Equal(0,sdk.Calls);
            native.Command("restart");native.Activate();await native.StartSessionAsync();
            Assert.Equal(0,sdk.Calls);
            var rendered=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("TEST",rendered);Assert.Contains("No AI calls",rendered);Assert.Contains("Recorded analysis",rendered);
            Assert.Contains("Voice off",rendered);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ToggleVoiceForModeTest());
            Assert.True(native.Voice.Enabled);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ToggleVoiceForModeTest());
            Assert.False(native.Voice.Enabled);Assert.Equal(0,sdk.Calls);
        }
        [Fact]
        public async Task Remembered_Test_launch_never_calls_and_switching_preferences_preserves_game_until_explicit_generation()
        {
            var sdk=new Forbidden();
            using var native=new MatchService(sdk,sdk,(_,_)=>throw new InvalidOperationException(),sdk,readyDemo:false,mode:SessionMode.Test);
            await native.StartSessionAsync();Assert.True(native.HasMatch);Assert.Equal(0,sdk.Calls);
            var match=native.Match;native.Command("seek",120);
            await native.ChooseModeAsync(SessionMode.Live,false);
            Assert.Same(match,native.Match);Assert.Equal(120,native.Sample(true)!.Frame.Snapshot.Time);Assert.Equal(0,sdk.Calls);
            await native.ChooseModeAsync(SessionMode.Test,false);Assert.Same(match,native.Match);Assert.Equal(0,sdk.Calls);
        }
        [Fact]
        public async Task Explicit_Live_choice_starts_once_and_entering_Test_retains_fresh_game_without_more_requests()
        {
            var source=new PendingScenario();var sdk=new Forbidden();SessionMode? stored=null;
            using var native=new MatchService(source,sdk,(_,_)=>Task.CompletedTask,sdk,readyDemo:false,
                mode:SessionMode.Unchosen,saveMode:value=>stored=value);
            await native.StartSessionAsync();Assert.Equal(0,source.Calls);
            var first=native.ChooseModeAsync(SessionMode.Live);
            var duplicate=native.ChooseModeAsync(SessionMode.Live);
            Assert.Equal(1,source.Calls);Assert.True(native.Generating);
            source.Reply.SetResult(VarietyTests.OneSided());await first;await duplicate;
            Assert.True(native.Fresh);var match=native.Match;native.Command("seek",150);
            await native.ChooseModeAsync(SessionMode.Test,false);
            Assert.Same(match,native.Match);Assert.Equal(150,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.True(native.Fresh);Assert.Equal(SessionMode.Test,stored);
            await native.StartSessionAsync();await native.GenerateAsync();Assert.Equal(1,source.Calls);
        }
        [Fact]
        public async Task Voice_dedups_current_cue_serializes_supersession_has_no_backlog_and_ignores_stale_completion()
        {
            var retire=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var engine=new Speech{Retirement=retire.Task};using var voice=new SpokenCommentary(engine);
            Assert.False(voice.Enabled);voice.Read("a","First");Assert.Empty(engine.Text);
            voice.SetEnabled(true);voice.Read("a","First");
            await engine.WaitCalls(1);
            for(var i=0;i<20;i++)voice.Read("a","First");
            Assert.Single(engine.Text);
            voice.Read("b","Second");voice.Read("c","Newest");
            retire.SetResult();
            await engine.WaitCalls(2);
            Assert.Equal(1,engine.MaxConcurrent);
            Assert.Equal("Newest",engine.Text.Last());
            Assert.DoesNotContain("Second",engine.Text);
            voice.Stop();await Task.Delay(20);
            voice.Read("next","Next observed");await engine.WaitCalls(3);
            voice.SetEnabled(false);await Task.Delay(20);Assert.Equal("Voice off",voice.Status);
            Assert.False(voice.Enabled);
        }
        [Fact]
        public async Task Voice_error_disables_only_audio_and_dispose_retires_pending_callbacks()
        {
            using var voice=new SpokenCommentary(new ErrorSpeech());voice.SetEnabled(true);voice.Read("a","text");
            await Task.Delay(30);Assert.False(voice.Enabled);Assert.Contains("Voice unavailable",voice.Status);
            var engine=new Speech();var disposed=new SpokenCommentary(engine);disposed.SetEnabled(true);disposed.Read("a","text");
            await engine.WaitCalls(1);disposed.Dispose();await Task.Delay(20);Assert.Equal("Voice off",disposed.Status);
        }
        [Fact]
        public async Task Stale_engine_failure_after_voice_off_cannot_replace_off_status_or_reenable_audio()
        {
            var engine=new LateSpeech();using var voice=new SpokenCommentary(engine);
            voice.SetEnabled(true);voice.Read("first","Old cue");
            Assert.Equal(1,engine.Calls);
            voice.SetEnabled(false);
            engine.Done.SetException(new InvalidOperationException("Late old engine error"));
            await Task.Delay(30);
            Assert.False(voice.Enabled);Assert.Equal("Voice off",voice.Status);
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
            public Task<string> GenerateAsync(IProgress<string>?progress,CancellationToken token){Calls++;return Reply.Task;}
        }
        private sealed class Clock:IMonotonicClock{public double Seconds{get;set;}}
        private sealed class ErrorSpeech:ILocalSpeech
        {public Task SpeakAsync(string text,CancellationToken token)=>throw new NotSupportedException("No installed English voice");}
        private sealed class LateSpeech:ILocalSpeech
        {
            public int Calls;public TaskCompletionSource Done=new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task SpeakAsync(string text,CancellationToken token){Calls++;return Done.Task;}
        }
        private sealed class Speech:ILocalSpeech
        {
            public List<string> Text=[];private int active;public int MaxConcurrent;
            public Task Retirement=Task.CompletedTask;
            public async Task SpeakAsync(string text,CancellationToken token)
            {active++;MaxConcurrent=Math.Max(MaxConcurrent,active);Text.Add(text);try{await Task.Delay(Timeout.Infinite,token);}finally{await Retirement;active--;}}
            public async Task WaitCalls(int n){for(var i=0;i<100&&Text.Count<n;i++)await Task.Delay(5);Assert.Equal(n,Text.Count);}
        }
        private sealed class Capture:IComponentActivator
        {public Page? Page;public IComponent CreateInstance(Type t){var c=(IComponent)Activator.CreateInstance(t)!;if(c is Page p)Page=p;return c;}}
        private sealed class NoJs:IJSRuntime
        {public ValueTask<T> InvokeAsync<T>(string id,object?[]?args)=>throw new InvalidOperationException("No native UI");public ValueTask<T> InvokeAsync<T>(string id,CancellationToken t,object?[]?args)=>InvokeAsync<T>(id,args);}
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public Task ChooseTestForModeTest()=>StartTest();
        public void ToggleVoiceForModeTest()=>ToggleVoice();
    }
}
