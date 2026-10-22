using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    public sealed class ViewSettingsTests
    {
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
            foreach(var label in new[]{"Numbers","Photo tokens","Cut-out players","Portrait tiles","Focus first","Smart zoom","Player marker size","Shirt-number badges"})
                Assert.Contains(label,html);
            Assert.Contains("<dialog id=\"view-dialog\"",html);
            Assert.DoesNotContain("id=\"view-button\" aria-haspopup",html);
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
        private static ServiceProvider Provider(MatchService native,Capture capture)=>new ServiceCollection().AddLogging()
            .AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
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
        private sealed class Capture:IComponentActivator
        {
            public Page? Page;public Controls? Controls;
            public IComponent CreateInstance(Type type){var c=(IComponent)Activator.CreateInstance(type)!;if(c is Page p)Page=p;if(c is Controls v)Controls=v;return c;}
        }
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public void AttachViewModuleForTest(IJSObjectReference value){module=value;rendererReady=true;loadedRevision=frame!.Revision;startupRequested=true;}
        public async Task OpenViewForTest(){Open("view-dialog");await OnAfterRenderAsync(false);}
        public Task RefreshViewForTest()=>Refresh(true);
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
