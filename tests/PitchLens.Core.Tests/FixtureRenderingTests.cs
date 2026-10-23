using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;

namespace PitchLens.Core.Tests;

public sealed class FixtureRenderingTests
{
    [Fact]
    public async Task Captured_fixture_is_visible_during_first_creation_before_any_replay_or_score()
    {
        var source=new WaitingSource();
        using var native=new MatchService(source,new DirectorTests.NoMoment(),(_,_)=>Task.CompletedTask,readyDemo:false,variety:new MatchVariety());
        using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
        await native.StartSessionAsync();native.SetFixtureChoice(FixtureChoice.Random);var request=native.SubmitFixtureAsync();
        var fixture=source.Request!.Fixture!;
        var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Contains(fixture.Home.Name,html);Assert.Contains(fixture.Away.Name,html);
        Assert.DoesNotContain("id=\"pitch\"",html);Assert.DoesNotContain("id=\"match-score\"",html);
        native.Cancel();source.End.SetCanceled();await request;
    }
    [Fact]
    public async Task Actual_match_Razor_uses_one_fixture_score_with_selected_club_names_crests_and_roster()
    {
        var source=new VarietyTests.Source("unused");
        using var native=new MatchService(source,new DirectorTests.NoMoment(),(_,_)=>Task.CompletedTask,readyDemo:false,variety:new MatchVariety());
        await native.GenerateAsync();
        Assert.True(native.HasMatch);Assert.True(native.Fresh);
        var fixture=source.Request!.Fixture!;
        using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
        var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
        Assert.Contains(fixture.Home.Name,html);Assert.Contains(fixture.Away.Name,html);
        Assert.Contains($"src=\"{fixture.Home.Crest}\"",html);Assert.Contains($"src=\"{fixture.Away.Crest}\"",html);
        Assert.Contains($">{fixture.Home.Name}</strong>",html);Assert.Contains($">{fixture.Away.Name}</strong>",html);
        Assert.DoesNotContain("pitch-scorebug",html);Assert.DoesNotContain("score-strip",html);
        foreach(var player in fixture.Players)Assert.Contains(player.Name,html);
    }
    private sealed class NoJs:IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id,object?[]?args)=>throw new InvalidOperationException("No app launch.");
        public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]?args)=>InvokeAsync<T>(id,args);
    }
    private sealed class WaitingSource:IScenarioSource
    {
        public VarietyRequest? Request;public TaskCompletionSource<string> End=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> GenerateAsync(IProgress<string>? progress,CancellationToken token)=>throw new InvalidOperationException("Fixture context required.");
        public Task<string> GenerateAsync(VarietyRequest request,IProgress<string>? progress,CancellationToken token){Request=request;return End.Task;}
    }
}
