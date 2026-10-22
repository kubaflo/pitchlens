using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;
using Portrait=PitchLens.Core.Tests.Components.PlayerPortrait;

namespace PitchLens.Core.Tests
{
    public sealed class PlayerPortraitTests
    {
        [Fact]
        public void Exact65_original_PNGs_match_reviewed66_identities_dimensions_hashes_and_explicit403_fallback()
        {
            var folder=Path.Combine(AppContext.BaseDirectory,"PortraitAssets");
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"portraits.json")));
            using var reviewed=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"identities.json")));
            var entries=manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            var players=FixtureCatalog.Clubs.SelectMany(team=>FixtureCatalog.Create(team.Id,
                FixtureCatalog.Clubs.First(other=>other.Id!=team.Id).Id).Players.Where(p=>p.TeamId==team.Id)).ToArray();
            Assert.Equal(66,players.Length);Assert.Equal(players.Select(p=>p.Id).Order(),entries.Select(e=>e.GetProperty("playerId").GetString()).Order());
            Assert.Equal(66,PlayerPortraitCatalog.Entries.Count);
            var rules=reviewed.RootElement.EnumerateArray().ToDictionary(e=>e[0].GetString()!,e=>e);
            var present=entries.Where(e=>e.GetProperty("imagePath").ValueKind==JsonValueKind.String).ToArray();
            Assert.Equal(65,present.Length);Assert.Equal(65,Directory.GetFiles(folder,"*.png").Length);
            Assert.Equal(6_708_397,present.Sum(e=>e.GetProperty("bytes").GetInt64()));
            var hashes=new HashSet<string>();
            foreach(var entry in entries)
            {
                var id=entry.GetProperty("playerId").GetString()!;
                var player=players.Single(p=>p.Id==id);
                Assert.Equal(player.Name,entry.GetProperty("name").GetString());
                Assert.Equal(player.TeamId,entry.GetProperty("teamId").GetString());
                var official=entry.GetProperty("officialId").GetString();
                Assert.Equal(rules[id][1].GetString(),official);
                Assert.Equal(rules[id][2].GetString(),entry.GetProperty("officialName").GetString());
                Assert.Equal(official,entry.GetProperty("identityEvidence").GetProperty("officialPlayerId").GetString());
                Assert.Equal(player.Number,entry.GetProperty("identityEvidence").GetProperty("shirtNumber").GetInt32());
                Assert.Contains("/en/players/"+official+"/",entry.GetProperty("profileUrl").GetString());
                Assert.Equal($"https://resources.premierleague.com/premierleague25/photos/players/110x140/{official}.png",entry.GetProperty("imageUrl").GetString());
                if(entry.GetProperty("imagePath").ValueKind==JsonValueKind.Null)
                {
                    Assert.Equal("chelsea-martinez",id);
                    Assert.Contains("403",entry.GetProperty("unavailableReason").GetString());
                    continue;
                }
                Assert.Equal($"images/players/{id}.png",entry.GetProperty("imagePath").GetString());
                var bytes=File.ReadAllBytes(Path.Combine(folder,id+".png"));
                Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},bytes.Take(8));
                Assert.Equal(entry.GetProperty("width").GetInt32(),(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)));
                Assert.Equal(entry.GetProperty("height").GetInt32(),(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4)));
                Assert.Equal(entry.GetProperty("bytes").GetInt32(),bytes.Length);
                var sha=Convert.ToHexStringLower(SHA256.HashData(bytes));
                Assert.Equal(entry.GetProperty("sha256").GetString(),sha);
                Assert.NotEqual(manifest.RootElement.GetProperty("placeholderSha256").GetString(),sha);
                Assert.True(hashes.Add(sha),"Distinct official identities must not share a placeholder face.");
                Assert.True(entry.GetProperty("visiblePixels").GetInt32()>1000);
                Assert.True(entry.GetProperty("uniqueColors").GetInt32()>100);
            }
            Assert.DoesNotContain(PlayerPortraitCatalog.Entries,e=>e.OfficialId=="427637"); // Official sample is Brenden Aaronson, not our roster.
        }
        [Theory]
        [InlineData("arsenal","manchester-city",false)]
        [InlineData("liverpool","chelsea",true)]
        [InlineData("manchester-united","tottenham-hotspur",true)]
        public async Task Actual_Test_and_generated_fixture_Razor_use_exact_shared_portraits_without_SDK_or_hash_changes(string home,string away,bool generated)
        {
            var forbidden=new MatchLensTests.Forbidden();
            var source=new FixtureSource(FixtureTests.Json(FixtureCatalog.Create(home,away)));
            using var native=new MatchService(generated?source:forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,mode:SessionMode.Live);
            if(generated)await native.GenerateAsync();
            else await native.ChooseModeAsync(SessionMode.Test,false);
            Assert.True(native.HasMatch);
            Assert.Equal(generated,native.Fresh);
            Assert.Equal(home,native.Match.Teams[0].Id);Assert.Equal(away,native.Match.Teams[1].Id);
            var match=native.Match;var facts=NarrationPrompt.Hash(match);
            var gameplay=MatchVariety.Signature(match).Hash;
            using var services=new ServiceCollection().AddLogging().AddSingleton(native).AddSingleton<IJSRuntime>(new NoJs()).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("class=\"player-portrait\"",html);
            foreach(var player in match.Players)
            {
                var portrait=PlayerPortraitCatalog.Find(player);
                Assert.NotNull(portrait);
                if(portrait.ImagePath is { } image)Assert.Contains($"src=\"{image}\"",html);
                else Assert.Contains($"Photo unavailable for {player.Name}",html);
                Assert.Contains($"data-portrait-player=\"{player.Id}\"",html);
            }
            var selected=match.Player(native.Sample(true)!.Frame.SelectedId);
            Assert.Contains(PlayerPortraitCatalog.Find(selected)!.ImagePath!,html);
            Assert.Contains("loading=\"lazy\"",html);Assert.Contains("width=\"110\" height=\"140\"",html);
            Assert.DoesNotContain("src=\"https://resources.premierleague.com",html);
            Assert.Equal(facts,NarrationPrompt.Hash(match));Assert.Equal(gameplay,MatchVariety.Signature(match).Hash);
            Assert.Equal(generated?1:0,source.Calls);Assert.Equal(0,forbidden.Calls);
        }
        [Fact]
        public async Task Unknown_mislabelled_missing_and_failed_images_show_shirt_fallback_not_another_person()
        {
            var original=DenseNarrationRecording.Create().Players.First();
            foreach(var person in new[]{
                original with{Id="unmapped-player"},original with{Name="Another person"},original with{TeamId="another-club"},
                FixtureCatalog.Create("chelsea","liverpool").Players.Where(p=>p.Id=="chelsea-martinez")
                    .Select(p=>new Player(p.Id,p.TeamId,p.Number,p.Name,original.Path)).Single()})
            {
                using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
                await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
                var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Portrait>(ParameterView.FromDictionary(
                    new Dictionary<string,object?>{{nameof(Portrait.Player),person}})));
                var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
                Assert.DoesNotContain("<img",html);Assert.Contains("Photo unavailable for "+person.Name,html);
                Assert.Contains("#"+person.Number,html);
            }
            var capture=new Capture();
            using var provider=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var rendered=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
            var image=await rendered.Dispatcher.InvokeAsync(()=>rendered.RenderComponentAsync<Portrait>(ParameterView.FromDictionary(
                new Dictionary<string,object?>{{nameof(Portrait.Player),original}})));
            Assert.Contains("<img",await rendered.Dispatcher.InvokeAsync(image.ToHtmlString));
            await rendered.Dispatcher.InvokeAsync(()=>capture.Component!.FailImageForTest());
            var fallback=await rendered.Dispatcher.InvokeAsync(image.ToHtmlString);
            Assert.DoesNotContain("<img",fallback);Assert.Contains("portrait-fallback",fallback);
            Assert.Contains(original.Name,System.Net.WebUtility.HtmlDecode(fallback));
        }
        private sealed class FixtureSource(string json):IScenarioSource
        {public int Calls;public Task<string> GenerateAsync(IProgress<string>? progress,CancellationToken token){Calls++;return Task.FromResult(json);}}
        private sealed class NoJs:IJSRuntime
        {public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>throw new InvalidOperationException("No native UI.");public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);}
        private sealed class Capture:IComponentActivator
        {public Portrait? Component;public IComponent CreateInstance(Type t){var c=(IComponent)Activator.CreateInstance(t)!;if(c is Portrait p)Component=p;return c;}}
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class PlayerPortrait
    {public void FailImageForTest(){ImageFailed(EventArgs.Empty);StateHasChanged();}}
}
