using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;
using Controls=PitchLens.Core.Tests.Components.ViewSettings;

namespace PitchLens.Core.Tests
{
#pragma warning disable BL0006 // Dispatch the real Razor event handlers in this headless interaction regression.
    public sealed class ViewSettingsTests
    {
        [Fact]
        public async Task Actual_Razor_task_focus_and_tailor_handlers_condition_source_without_double_spend()
        {
            var source=new PersonalizedInsightTests.Source();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,replayClock:new Clock());
            native.Command("seek",86);
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("explain-button"));
            Assert.Equal(1,source.Calls);var first=native.Explainer.Current!;
            Assert.False(native.Sample(true)!.Frame.Playing);Assert.Contains("openPopup:moment-dialog",module.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("insight-task","1"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("insight-focus","club:manchester-city"));
            Assert.Equal(1,source.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("tailor-explain-button"));
            Assert.Equal(2,source.Calls);Assert.NotEqual(first.Evidence.Hash,native.Explainer.Current!.Evidence.Hash);
            Assert.Equal(InsightTask.ExplainBuildUp,native.Explainer.Current.Evidence.Preferences.Task);
            Assert.Equal("manchester-city",native.Explainer.Current.Evidence.Preferences.SubjectId);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("tailor-explain-button"));
            Assert.Equal(2,source.Calls);Assert.True(native.Explainer.Current.Cached);
            Assert.Equal(86,native.Sample(true)!.Frame.Snapshot.Time);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Actual_Razor_tour_and_ledger_content_is_honest_about_recorded_reads(bool fresh)
        {
            var source=new PersonalizedInsightTests.Source();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,
                mode:fresh?SessionMode.Live:SessionMode.Test,replayClock:new Clock());
            if(fresh){native.Command("seek",86);await native.ExplainCurrentMomentAsync();}
            else{native.StartGuidedDemo();native.SetGuidedDemoStep(1);}
            using var services=Provider(native,new Capture());
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("How this was checked",html);
            Assert.Contains("not prove every interpretation true",html);
            if(fresh){Assert.Contains("observed_match",html);Assert.Contains("observed_event",html);Assert.Equal(2,native.Explainer.Current!.Ledger!.Length);}
            else{Assert.Contains("Individual read metadata was not recorded",html);Assert.Contains("Guided demo · No AI calls",html);Assert.Equal(0,source.Calls);}
            if(Environment.GetEnvironmentVariable("PITCHLENS_P0_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,fresh?"controlled-fresh-ledger.html":"recorded-tour.html"),html);
                if(fresh&&Environment.GetEnvironmentVariable("PITCHLENS_P0_REAL_WITNESS") is {Length:>0} real&&
                    Environment.GetEnvironmentVariable("PITCHLENS_P0_ACCEPTED_MATCH") is {Length:>0} input)
                {
                    var bytes=await File.ReadAllBytesAsync(input);
                    Assert.Equal("0332e31623b43f17a6b48c16555076eb0a5c2a52895fc924b6d17aa1ccffc1bf",
                        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
                    var match=AuthoredMatchPlan.Parse(System.Text.Encoding.UTF8.GetString(bytes),false);
                    foreach(var task in new[]{"ExplainBuildUp","PlayerContribution"})
                    {
                        var path=Path.Combine(real,task);
                        var facts=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"moment-facts.json")),MomentJsonContext.Default.MomentFacts)!;
                        var evidence=MomentEvidence.Capture(match,facts.Revision,facts.Cutoff,facts.FocusId,preferences:facts.Context!.Preferences);
                        var answer=evidence.Validate(await File.ReadAllTextAsync(Path.Combine(path,"validated.json")));
                        var ledger=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"ledger.json")),MomentJsonContext.Default.MomentReadAuditArray)!;
                        var counts=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"moment-counts.json")),MomentWitnessJsonContext.Default.MomentRequestCounts)!;
                        Assert.Equal(1,counts.SdkSendInvocations);Assert.Equal(1,counts.Responses);Assert.InRange(counts.ToolCalls,ledger.Length,6);
                        Assert.InRange(ledger.Length,2,6);
                        var actual=new ExplainedMoment(evidence,answer,counts.ToolCalls,Ledger:ledger);
                        var rendered=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Components.MomentStudio>(
                            ParameterView.FromDictionary(new Dictionary<string,object?>{{"Result",actual}})));
                        var actualHtml=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString));
                        Assert.Contains(evidence.Hash,actualHtml);Assert.Contains("observed_match",actualHtml);
                        Assert.Contains(facts.Context.SubjectName!,actualHtml);Assert.Contains(answer.Fan[0].Text,actualHtml);
                        await File.WriteAllTextAsync(Path.Combine(folder,$"actual-backend-{task}.html"),actualHtml);
                    }
                }
            }
        }
        [Fact]
        public async Task Actual_Razor_guided_choice_steps_back_and_exit_never_call_SDK_or_start_Live()
        {
            var source=new MatchLensTests.Forbidden();SessionMode? saved=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                readyDemo:false,mode:SessionMode.Unchosen,saveMode:m=>saved=m,replayClock:new Clock(),filmClock:new Clock());
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("first-guided-demo"));
            Assert.Equal(SessionMode.Test,saved);Assert.Equal(0,native.GuidedDemoStep);
            Assert.Equal(76,native.Sample(true)!.Frame.Snapshot.Time);Assert.Equal(0,source.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-next"));
            Assert.Equal(1,native.GuidedDemoStep);Assert.Equal(86,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.Contains("openPopup:moment-dialog",module.Calls);Assert.False(native.Explainer.Current!.Fresh);
            Assert.Equal(InsightPreferences.Default,native.Explainer.Current.Evidence.Preferences);
            Assert.Null(native.Explainer.Current.Ledger);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("moment-tour-next"));
            Assert.Equal(2,native.GuidedDemoStep);Assert.Contains("setEvidence",module.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-next"));
            Assert.True(native.FilmActive);Assert.Equal(58,native.FilmFrame!.Duration);Assert.False(native.Director.Current!.Fresh);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-back"));
            Assert.False(native.FilmActive);Assert.Equal(2,native.GuidedDemoStep);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-exit"));
            Assert.Null(native.GuidedDemoStep);Assert.False(native.Sample(true)!.Frame.Playing);Assert.Equal(0,source.Calls);
            Assert.Equal(SessionMode.Test,native.Mode);
            if(Environment.GetEnvironmentVariable("PITCHLENS_P0_WITNESS") is {Length:>0} folder)
            {Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"tour-handler-path.json"),
                System.Text.Json.JsonSerializer.Serialize(new{mode=native.Mode,sdkCalls=source.Calls,moduleCalls=module.Calls,renderer.PendingPopups,ExitPaused=!native.Sample(true)!.Frame.Playing}));}
        }
        [Theory]
        [InlineData(SessionMode.Test)]
        [InlineData(SessionMode.Live)]
        public async Task Replay_settings_camera_entry_dispatches_actual_Razor_clicks_without_resuming_the_paused_match(SessionMode mode)
        {
            var source=new MatchLensTests.Forbidden();string? stored=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                mode:mode,replayClock:new Clock(),saveViewSettings:value=>stored=PitchViewSettings.Serialize(value));
            native.Command("seek",100);native.Command("highlights",0);native.Command("rate",3);native.Command("play");
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            var selected=native.Sample(true)!.Frame.SelectedId;
            var phases=new List<object>();
            void Record(string phase)
            {
                var f=native.Sample(true)!.Frame;
                phases.Add(new{phase,time=f.Snapshot.Time,f.Playing,f.Rate,f.SelectedId,settings=native.ViewSettings,sdkCalls=source.Calls});
                Assert.Equal(100,f.Snapshot.Time);Assert.Equal(3,f.Rate);Assert.Equal(selected,f.SelectedId);Assert.Equal(0,source.Calls);
            }
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("more-button"));
            Assert.True(native.Sample(true)!.Frame.Playing);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("settings-button"));
            Assert.Contains("openPopup:settings-dialog",module.Calls);Assert.False(native.Sample(true)!.Frame.Playing);
            Record("Replay settings opened");
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("settings-camera-button"));
            Assert.Contains("view-dialog",renderer.PendingPopups);
            Assert.Equal("openPopup:view-dialog",module.Calls.Last(c=>c.StartsWith("openPopup:")));
            Assert.False(native.Sample(true)!.Frame.Playing);Record("Settings camera entry opened shared popup");
            foreach(var style in Enum.GetValues<PlayerMarkerStyle>())
            {
                await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(style));
                Assert.True(await renderer.Dispatcher.InvokeAsync(()=>renderer.HasButton("view-wide-camera")));
                Assert.True(await renderer.Dispatcher.InvokeAsync(()=>renderer.HasButton("view-detail-camera")));
                Record($"Camera framing available for {style}");
                await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-detail-camera"));
                Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);Assert.True(native.ViewSettings.SmartDetail);
                Record($"Detail explicitly selected Smart zoom from {style}");
            }
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-detail-camera"));
            Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);Assert.True(native.ViewSettings.SmartDetail);
            Assert.False(native.Sample(true)!.Frame.Playing);Record("Detail explicitly selects Smart zoom");
            Assert.Equal(native.ViewSettings,PitchViewSettings.Load(stored,out _));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-wide-camera"));
            Assert.False(native.ViewSettings.SmartDetail);Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);
            Record("Wide restores Smart zoom wide camera");
            native.Command("play");
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("more-button"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-button"));
            Assert.True(native.Sample(true)!.Frame.Playing);Record("Direct More camera entry does not pause");
            if(Environment.GetEnvironmentVariable("PITCHLENS_CAMERA_PATH_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder,$"camera-path-{mode}.json"),
                    System.Text.Json.JsonSerializer.Serialize(new{evidence="Actual Razor render-tree click dispatch; no native/browser UI",phases,pendingPopups=renderer.PendingPopups,calls=module.Calls}));
            }
        }
        [Theory]
        [InlineData(SessionMode.Test,false)]
        [InlineData(SessionMode.Test,true)]
        [InlineData(SessionMode.Live,false)]
        [InlineData(SessionMode.Live,true)]
        public async Task Actual_Razor_six_styles_size_badges_and_camera_apply_without_pause_seek_rate_or_SDK(SessionMode mode,bool playing)
        {
            var source=new MatchLensTests.Forbidden();var clock=new Clock();string? stored=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,mode:mode,
                replayClock:clock,saveViewSettings:value=>stored=PitchViewSettings.Serialize(value));
            native.Command("seek",100);native.Command("highlights",0);native.Command("rate",3);
            if(playing)native.Command("play");
            var capture=new Capture();using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var module=new Module();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.AttachViewModuleForTest(module));
            var original=native.Sample(true)!.Frame;
            var hash=NarrationPrompt.Hash(native.Match);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.OpenViewForTest());
            Assert.Contains("openPopup:view-dialog",module.Calls);
            foreach(var style in Enum.GetValues<PlayerMarkerStyle>())
            {
                await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(style));
                await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshViewForTest());
                Assert.Equal(style,native.ViewSettings.Style);
                Assert.NotNull(module.LastSettings);
                Assert.Equal(native.ViewSettings,PitchViewSettings.Load(module.LastSettings,out _));
                var frame=native.Sample(true)!.Frame;
                Assert.Equal(original.Snapshot.Time,frame.Snapshot.Time);Assert.Equal(playing,frame.Playing);
                Assert.Equal(original.SelectedId,frame.SelectedId);Assert.Equal(3,frame.Rate);
                Assert.Equal(hash,NarrationPrompt.Hash(native.Match));
                var styleHtml=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
                Assert.DoesNotContain("class=\"segments camera-controls\" hidden",styleHtml);
                Assert.Contains("Wide or Detail selects the Smart zoom style",styleHtml);
            }
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetSizeForTest("1.25"));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetBadgesForTest(false));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetDetailForTest(true));
            Assert.Equal(1.25,native.ViewSettings.MarkerScale);Assert.False(native.ViewSettings.ShowNumbers);Assert.True(native.ViewSettings.SmartDetail);
            var interop=module.SettingsWrites;
            for(var i=0;i<4;i++)await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshViewForTest());
            Assert.Equal(interop,module.SettingsWrites); // Repeated paused/native samples do not resend visual settings.
            Assert.Equal(0,source.Calls);Assert.NotNull(stored);
            var restored=PitchViewSettings.Load(stored,out var notice);Assert.Null(notice);
            using var relaunched=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                mode:mode,viewSettings:restored);
            Assert.Equal(native.ViewSettings,relaunched.ViewSettings);
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            foreach(var label in new[]{"Camera & player appearance","Camera framing","Wide · Smart zoom","Detail · Smart zoom","Numbers","Photo tokens","Cut-out players","Portrait tiles","Focus first","Smart zoom","Player marker size","Shirt-number badges"})
                Assert.Contains(label,html);
            Assert.Contains("<dialog id=\"view-dialog\"",html);
            Assert.DoesNotContain("id=\"view-button\" aria-haspopup",html);
            Assert.Contains("id=\"settings-camera-button\"",html);
            Assert.True(html.IndexOf("class=\"camera-entry\"",StringComparison.Ordinal)<html.IndexOf("id=\"scenario-provenance\"",StringComparison.Ordinal));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ResetForTest());
            Assert.Equal(PitchViewSettings.Default,native.ViewSettings);
            Assert.Equal(PitchViewSettings.Default,PitchViewSettings.Load(stored,out _));
            Assert.Equal(playing,native.Sample(true)!.Frame.Playing);Assert.Equal(100,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.Equal(0,source.Calls);
            if(Environment.GetEnvironmentVariable("PITCHLENS_VIEW_WITNESS") is {Length:>0} folder)
            {Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,$"view-{mode}-{playing}.html"),html);}
        }
        [Theory]
        [InlineData("{\"version\":1}")]
        [InlineData("{\"version\":2,\"style\":1,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":99,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":1,\"markerScale\":99,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":1,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false,\"url\":\"https://invalid\"}")]
        [InlineData("not-json")]
        public void Invalid_persisted_values_have_visible_default_notice_and_never_bind_an_unvalidated_style(string json)
        {
            var result=PitchViewSettings.Load(json,out var notice);
            Assert.Equal(PitchViewSettings.Default,result);Assert.NotNull(notice);Assert.Contains("invalid",notice);
            Assert.Equal(PitchViewSettings.Default,PitchViewSettings.Load("",out var fresh));Assert.Null(fresh);
            Assert.Throws<ArgumentException>(()=>(result with{MarkerScale=double.NaN}).Validate());
        }
        [Fact]
        public async Task Invalid_input_and_failed_preference_write_show_errors_without_stopping_football_or_mutating_settings()
        {
            var source=new MatchLensTests.Forbidden();var initial=PitchViewSettings.Load("broken",out var notice);
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                viewSettings:initial,viewSettingsNotice:notice,saveViewSettings:_=>throw new IOException("Controlled preference write failure."));
            native.Command("seek",100);native.Command("play");
            var capture=new Capture();using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Saved view settings are invalid",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(PlayerMarkerStyle.PhotoTokens));
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Controlled preference write failure",html);
            Assert.Equal(initial,native.ViewSettings);Assert.True(native.Sample(true)!.Frame.Playing);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetSizeForTest("invalid"));
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);Assert.Contains("Invalid marker size",html);
            Assert.Equal(initial,native.ViewSettings);Assert.Equal(0,source.Calls);
        }
        [Theory]
        [InlineData("arsenal","manchester-city",22)]
        [InlineData("liverpool","chelsea",21)]
        [InlineData("manchester-united","tottenham-hotspur",22)]
        public void Web_portraits_are_catalog_verified_presentation_only_and_missing_photo_has_no_binding(string home,string away,int available)
        {
            var match=AuthoredMatchPlan.Parse(FixtureTests.Json(FixtureCatalog.Create(home,away)));
            using var json=System.Text.Json.JsonDocument.Parse(WebContracts.Scenario(match,3));
            var players=json.RootElement.GetProperty("players").EnumerateArray().ToArray();
            Assert.Equal(available,players.Count(p=>p.GetProperty("portrait").ValueKind==System.Text.Json.JsonValueKind.Object));
            if(away=="chelsea")Assert.Equal(System.Text.Json.JsonValueKind.Null,players.Single(p=>p.GetProperty("id").GetString()=="chelsea-martinez").GetProperty("portrait").ValueKind);
            foreach(var p in players.Where(p=>p.GetProperty("portrait").ValueKind==System.Text.Json.JsonValueKind.Object))
            {
                var portrait=p.GetProperty("portrait");var id=p.GetProperty("id").GetString();
                Assert.Equal(id,portrait.GetProperty("playerId").GetString());
                Assert.Equal($"images/players/{id}.png",portrait.GetProperty("imagePath").GetString());
            }
        }
        private static ServiceProvider Provider(MatchService native,Capture capture,IJSRuntime? runtime=null)=>new ServiceCollection().AddLogging()
            .AddSingleton(native).AddSingleton<IJSRuntime>(runtime??new NoJs()).AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
        private sealed class Clock:IMonotonicClock{public double Seconds=>0;}
        private sealed class NoJs:IJSRuntime
        {public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>throw new InvalidOperationException("No UI.");public ValueTask<T> InvokeAsync<T>(string id,CancellationToken t,object?[]? args)=>InvokeAsync<T>(id,args);}
        private sealed class Module:IJSObjectReference
        {
            public List<string> Calls=[];public int SettingsWrites;public string? LastSettings;
            public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)
            {
                Calls.Add(id=="openPopup"?"openPopup:"+args![0]:id);
                if(id=="setViewSettings"){SettingsWrites++;LastSettings=(string)args![0]!;}
                return ValueTask.FromResult(default(T)!);
            }
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class Capture(bool skipStartup=false):IComponentActivator
        {
            public Page? Page;public Controls? Controls;
            public IComponent CreateInstance(Type type){var c=(IComponent)Activator.CreateInstance(type)!;if(c is Page p){Page=p;if(skipStartup)p.SkipStartupForCameraPathTest();}if(c is Controls v)Controls=v;return c;}
        }
        private sealed class ModuleRuntime(Module module):IJSRuntime
        {
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>ValueTask.FromResult((T)(object)module);
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class CameraEventRenderer(IServiceProvider services,Capture capture):Renderer(services,services.GetRequiredService<ILoggerFactory>())
        {
            public override Dispatcher Dispatcher{get;}=Dispatcher.CreateDefault();
            public List<string> PendingPopups=[];
            private int root;
            public Task StartAsync(){root=AssignRootComponentId(InstantiateComponent(typeof(Page)));return RenderRootComponentAsync(root);}
            protected override void HandleException(Exception error)=>throw new InvalidOperationException("Razor interaction failed.",error);
            protected override Task UpdateDisplayAsync(in RenderBatch batch)
            {if(capture.Page?.PendingPopupForCameraTest() is { } id)PendingPopups.Add(id);return Task.CompletedTask;}
            private ulong FindButton(int component,string id,string eventName="onclick")
            {
                var frames=GetCurrentRenderTreeFrames(component);
                for(var i=0;i<frames.Count;i++)
                {
                    var frame=frames.Array[i];
                    if(frame.FrameType==RenderTreeFrameType.Component)
                    {var child=FindButton(frame.ComponentId,id,eventName);if(child!=0)return child;}
                    if(frame.FrameType!=RenderTreeFrameType.Element)continue;
                    var matches=false;ulong handler=0;
                    for(var j=i+1;j<frames.Count&&frames.Array[j].FrameType==RenderTreeFrameType.Attribute;j++)
                    {
                        var a=frames.Array[j];if(a.AttributeName=="id"&&Equals(a.AttributeValue,id))matches=true;
                        if(a.AttributeName==eventName)handler=a.AttributeEventHandlerId;
                    }
                    if(matches)return handler;
                }
                return 0;
            }
            public bool HasButton(string id)=>FindButton(root,id)!=0;
            public Task ClickAsync(string id)
            {var handler=FindButton(root,id);Assert.NotEqual(0UL,handler);return DispatchEventAsync(handler,null,new MouseEventArgs(),true);}
            public Task ChangeAsync(string id,object value)
            {var handler=FindButton(root,id,"onchange");Assert.NotEqual(0UL,handler);return DispatchEventAsync(handler,null,new ChangeEventArgs{Value=value},true);}
        }
    }
#pragma warning restore BL0006
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public void AttachViewModuleForTest(IJSObjectReference value){module=value;rendererReady=true;loadedRevision=frame!.Revision;startupRequested=true;}
        public async Task OpenViewForTest(){Open("view-dialog");await OnAfterRenderAsync(false);}
        public Task RefreshViewForTest()=>Refresh(true);
        public void SkipStartupForCameraPathTest()=>startupRequested=true;
        public string? PendingPopupForCameraTest()=>pendingPopup?.Id;
    }
    public partial class ViewSettings
    {
        public Task SelectStyleForTest(PlayerMarkerStyle style)=>Apply(Value with{Style=style});
        public Task SetSizeForTest(string value)=>SizeChanged(new(){Value=value});
        public Task SetBadgesForTest(bool value)=>NumbersChanged(new(){Value=value});
        public Task SetDetailForTest(bool value)=>Apply(Value with{SmartDetail=value});
        public Task ResetForTest()=>Apply(PitchViewSettings.Default);
    }
}
