using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;
using Controls=PitchLens.Core.Tests.Components.MatchLenses;

namespace PitchLens.Core.Tests
{
    public sealed class LensRenderingTests
    {
        [Theory]
        [InlineData(SessionMode.Test)]
        [InlineData(SessionMode.Live)]
        public async Task Actual_Razor_lens_controls_focus_cell_and_edge_replay_make_zero_SDK_calls(SessionMode mode)
        {
            var forbidden=new MatchLensTests.Forbidden();var clock=new Clock();
            using var native=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,mode:mode,replayClock:clock);
            native.Command("seek",200);native.Command("highlights",0);native.Command("play");
            var capture=new Capture();
            using var services=Services(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            Assert.Null(capture.Page!.LensViewForTest);
            var initial=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Analysis",initial);Assert.DoesNotContain("My match story focus",initial);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ToggleForAnalysisTest());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(PitchLensMode.Connections));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.RangeForLensTest(ObservedRange.MatchSoFar));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ClubForLensTest(native.Match.Teams[1].Id));
            var view=capture.Page.LensViewForTest!;
            Assert.Equal("manchester-city",view.SubjectId);Assert.Equal(0,view.Window.Start);
            Assert.True(native.Sample(true)!.Frame.Playing);
            var edge=view.Edges.First();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.EdgeForLensTest(edge.Id));
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("Historical connections",html);Assert.Contains("average historical",html);
            Assert.Contains("Replay most recent pass evidence",html);
            Assert.Contains(string.Join(", ",edge.EventIds),html);
            var serialized=System.Text.Json.JsonSerializer.Serialize(capture.Page.LensViewForTest,WebJsonContext.Default.PitchLensView);
            Assert.Contains("\"revision\":1",serialized);Assert.Contains("\"cutoff\":200",serialized);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ReplayForLensTest(edge.Id));
            Assert.False(native.Sample(true)!.Frame.Playing);
            Assert.Equal(native.Match.Events.Single(e=>e.Id==edge.EventIds[^1]).Time,native.Sample(true)!.Frame.Snapshot.Time);
            native.Command("seek",200);native.Command("play");
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(PitchLensMode.Movement));
            var player=native.Match.Players[^1];
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.PlayerForLensTest(player.Id));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.CellForLensTest(7));
            Assert.Equal(player.Id,native.Sample(true)!.Frame.SelectedId);Assert.True(native.Sample(true)!.Frame.Playing);
            Assert.Equal(7,capture.Page.LensViewForTest!.SelectedCell);
            Assert.Equal(200,capture.Page.LensViewForTest.Cells.Sum(c=>c.Seconds),8);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.FocusForLensTest("player:"+player.Id));
            html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("My match / "+player.Name,html);
            Assert.Contains("Goal context",html);
            Assert.True(native.Sample(true)!.Frame.Playing);Assert.Equal(0,forbidden.Calls);
            clock.Seconds=1;
            Assert.Equal(203,native.Sample(true)!.Frame.Snapshot.Time); // Focus did not pause or change selected3x.
            native.Command("seek",0);await renderer.Dispatcher.InvokeAsync(()=>capture.Page.RefreshForLensTest());
            Assert.All(capture.Page.LensViewForTest!.Cells,c=>Assert.Equal(0,c.Seconds));
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("No elapsed player-time yet",html);
            Assert.Contains("No observed chapters match",html);
            Assert.Equal(0,forbidden.Calls);
        }
        [Theory]
        [InlineData("liverpool","chelsea")]
        [InlineData("manchester-united","tottenham-hotspur")]
        public async Task Actual_lens_controls_use_each_nonhistorical_fixture_without_name_or_direction_hardcodes(string home,string away)
        {
            var forbidden=new MatchLensTests.Forbidden();
            var source=new LocalFixture(FixtureTests.Json(FixtureCatalog.Create(home,away)));
            using var native=new MatchService(source,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden);
            await native.GenerateAsync();Assert.True(native.Fresh,native.Status);Assert.Equal(1,source.Calls);
            await native.ChooseModeAsync(SessionMode.Test,false);
            native.Command("seek",150);
            var capture=new Capture();using var services=Services(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ToggleForAnalysisTest());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(PitchLensMode.Connections));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ClubForLensTest(away));
            var lens=capture.Page!.LensViewForTest!;
            Assert.Equal(home+"/"+away,lens.Window.Fixture);Assert.Equal(away,lens.SubjectId);
            Assert.All(lens.Nodes,n=>Assert.Equal(away,native.Match.Player(n.PlayerId).TeamId));
            var focus=native.Match.Players.Last(p=>p.TeamId==home);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.FocusForLensTest("player:"+focus.Id));
            Assert.Equal(home,capture.Page.LensViewForTest!.SubjectId);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(PitchLensMode.Movement));
            Assert.Equal(focus.Id,capture.Page.LensViewForTest!.SubjectId);
            Assert.Equal(60,capture.Page.LensViewForTest.Cells.Sum(c=>c.Seconds),8);
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains(focus.Name,html);Assert.Contains("Time-weighted movement",html);
            Assert.DoesNotContain("My match / Arsenal",html);Assert.DoesNotContain("My match / Manchester City",html);
            Assert.Equal(1,source.Calls);Assert.Equal(0,forbidden.Calls); // Only one offline fixture delivery; every lens action is local.
        }
        [Theory]
        [InlineData(PitchLensMode.Movement,false)]
        [InlineData(PitchLensMode.Connections,false)]
        [InlineData(PitchLensMode.Movement,true)]
        [InlineData(PitchLensMode.Connections,true)]
        public async Task Analysis_is_collapsed_with_active_lens_and_open_close_preserves_replay_focus_window_and_selection(PitchLensMode mode,bool otherFixture)
        {
            var forbidden=new MatchLensTests.Forbidden();var clock=new Clock();
            var source=new LocalFixture(FixtureTests.Json(FixtureCatalog.Create("liverpool","chelsea")));
            using var native=new MatchService(otherFixture?source:forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,replayClock:clock);
            if(otherFixture)await native.GenerateAsync();
            await native.ChooseModeAsync(SessionMode.Test,false);
            native.Command("seek",150);native.Command("highlights",0);native.Command("rate",3);native.Command("play");
            var capture=new Capture();using var services=Services(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            // Load an active selection before ever opening the disclosure.
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(mode));
            Assert.False(capture.Controls!.AnalysisOpenForTest);
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-expanded=\"false\"",html);Assert.Contains("aria-controls=\"analysis-panel\"",html);
            Assert.Contains("class=\"analysis-panel\" hidden",html);
            Assert.DoesNotContain("Lens observed window",html);Assert.DoesNotContain("My match story focus",html);
            Assert.DoesNotContain("hottest cell",html);Assert.DoesNotContain("class=\"lens-summary\"",html);
            Assert.Contains("id=\"commentary-caption\"",html);Assert.Contains("id=\"play-pause\"",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.ToggleForAnalysisTest());
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-expanded=\"true\"",html);Assert.Contains("id=\"analysis-panel\"",html);
            Assert.Contains("Lens observed window",html);Assert.Contains("My match story focus",html);
            Assert.Contains("Source details",html);Assert.Contains("<summary>Explore",html);
            var player=native.Match.Players.First(p=>p.TeamId==native.Match.Teams[0].Id);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.FocusForLensTest("player:"+player.Id));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.RangeForLensTest(ObservedRange.MatchSoFar));
            if(mode==PitchLensMode.Movement)await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.CellForLensTest(8));
            else await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.EdgeForLensTest(capture.Page!.LensViewForTest!.Edges[0].Id));
            var focus=capture.Controls.Focus;
            var options=capture.Controls.Options;
            Assert.NotNull(capture.Page);
            var playing=native.Sample(true)!.Frame;
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.ToggleForAnalysisTest());
            Assert.False(capture.Controls.AnalysisOpenForTest);
            Assert.Equal(options,capture.Controls.Options);Assert.Equal(focus,capture.Controls.Focus);
            Assert.Equal(player.Id,native.Sample(true)!.Frame.SelectedId);
            Assert.Equal(playing.Snapshot.Time,native.Sample(true)!.Frame.Snapshot.Time);Assert.True(native.Sample(true)!.Frame.Playing);
            for(var tick=1;tick<=4;tick++)
            {
                clock.Seconds=tick*.25;
                await renderer.Dispatcher.InvokeAsync(()=>capture.Page.RefreshForLensTest());
                Assert.False(capture.Controls.AnalysisOpenForTest);
                Assert.Equal(options,capture.Controls.Options);
            }
            Assert.Equal(153,native.Sample(true)!.Frame.Snapshot.Time);
            native.Command("seek",100);await renderer.Dispatcher.InvokeAsync(()=>capture.Page.RefreshForLensTest());
            Assert.False(capture.Controls.AnalysisOpenForTest);
            Assert.Equal(mode.ToString().ToLowerInvariant(),capture.Page.LensViewForTest!.Mode);
            Assert.Equal(ObservedRange.MatchSoFar,capture.Controls.Options.Range);
            Assert.Equal(player.Id,native.Sample(true)!.Frame.SelectedId);Assert.Equal(focus,capture.Controls.Focus);
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("class=\"analysis-panel\" hidden",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.ToggleForAnalysisTest());
            Assert.True(capture.Controls.AnalysisOpenForTest);
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("My match story focus",html);
            Assert.DoesNotContain("<details class=\"lens-data\" open",html); // Closing outer analysis cannot reopen nested exploration.
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls.ModeForLensTest(PitchLensMode.Off));
            Assert.Null(capture.Page.LensViewForTest);Assert.Equal(focus,capture.Controls.Focus);
            Assert.Equal(3,native.Sample(true)!.Frame.Rate);Assert.True(native.Sample(true)!.Frame.Playing);
            Assert.Equal(otherFixture?1:0,source.Calls);Assert.Equal(0,forbidden.Calls);
        }
        [Fact]
        public async Task Director_hides_and_restores_lenses_and_new_fixture_resets_missing_favorite_with_explicit_notice()
        {
            var forbidden=new MatchLensTests.Forbidden();var source=new VarietyTests.Source("unused");
            using var native=new MatchService(source,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,variety:new MatchVariety());
            native.Command("seek",300);
            var capture=new Capture();using var services=Services(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ToggleForAnalysisTest());
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ModeForLensTest(PitchLensMode.Movement));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.FocusForLensTest("club:arsenal"));
            native.PreviewRecordedDirector();native.WatchDirector();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshForLensTest());
            Assert.Null(capture.Page!.LensViewForTest);
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-expanded=\"false\"",html);
            Assert.Contains("class=\"analysis-panel\" hidden",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ToggleForAnalysisTest());
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("hidden during Director",html);
            Assert.Contains("<fieldset disabled",html);
            native.FilmCommand("exit");await renderer.Dispatcher.InvokeAsync(()=>capture.Page.RefreshForLensTest());
            Assert.NotNull(capture.Page.LensViewForTest);
            await renderer.Dispatcher.InvokeAsync(native.GenerateAsync);
            Assert.True(native.Fresh,native.Status);Assert.Equal(1,source.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page.RefreshForLensTest());
            Assert.Equal(2,capture.Page.LensViewForTest!.Window.Revision);
            Assert.Equal(0,capture.Page.LensViewForTest.Window.Cutoff);
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("aria-expanded=\"false\"",html);
            Assert.Contains("class=\"analysis-panel\" hidden",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ToggleForAnalysisTest());
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("previous favorite is not in this match",html);
            Assert.Contains("All observed stories",html);
            Assert.DoesNotContain("My match / Arsenal",html);
            Assert.Equal(0,forbidden.Calls);
        }
        private static ServiceProvider Services(MatchService native,Capture capture)=>new ServiceCollection().AddLogging()
            .AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
        private sealed class Clock:IMonotonicClock{public double Seconds{get;set;}}
        private sealed class LocalFixture(string json):IScenarioSource
        {
            public int Calls;
            public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken t){Calls++;return Task.FromResult(json);}
        }
        private sealed class NoJs:IJSRuntime
        {
            public ValueTask<T> InvokeAsync<T>(string id,object?[]?args)=>throw new InvalidOperationException("No app or SDK launch.");
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken t,object?[]?args)=>InvokeAsync<T>(id,args);
        }
        private sealed class Capture:IComponentActivator
        {
            public Page? Page;public Controls? Controls;
            public IComponent CreateInstance(Type type)
            {
                var component=(IComponent)Activator.CreateInstance(type)!;
                if(component is Page page)Page=page;if(component is Controls controls)Controls=controls;
                return component;
            }
        }
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public PitchLensView? LensViewForTest=>lensView;
        public Task RefreshForLensTest()=>Refresh(true);
    }
    public partial class MatchLenses
    {
        public void ToggleForAnalysisTest(){ToggleAnalysis();StateHasChanged();}
        public bool AnalysisOpenForTest=>analysisOpen;
        public Task ModeForLensTest(PitchLensMode mode)=>SetMode(mode);
        public Task RangeForLensTest(ObservedRange range)=>WindowChanged(new(){Value=range.ToString()});
        public Task ClubForLensTest(string id)=>ClubChanged(new(){Value=id});
        public Task PlayerForLensTest(string id)=>PlayerChanged(new(){Value=id});
        public Task EdgeForLensTest(string id)=>SelectEdge(id);
        public Task CellForLensTest(int index)=>SelectCell(index);
        public Task FocusForLensTest(string value)=>FocusChanged(new(){Value=value});
        public Task ReplayForLensTest(string id)=>Replay.InvokeAsync((View!,id));
    }
}
