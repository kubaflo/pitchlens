using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class FixtureTests
{
    // Offline control only: remap a known trajectory fixture to exercise generic validators; never production generation.
    internal static AuthoredMatchDocument Control(MatchFixture fixture)
    {
        var old=AuthoredMatchPlan.Document(VarietyTests.OneSided());
        var ids=RegulationRoster.Players.Zip(fixture.Players).ToDictionary(p=>p.First.Id,p=>p.Second.Id);
        var doc=old with { Fixture=fixture,Narration=null,Commentary=null,InitialOwnerId=ids[old.InitialOwnerId],
            Players=old.Players.Select(p=>p with { Id=ids[p.Id] }).ToArray(),
            Events=old.Events.Select(e=>e with { ActorId=ids[e.ActorId],ReceiverId=e.ReceiverId is null?null:ids[e.ReceiverId] }).ToArray() };
        var match=AuthoredMatchPlan.Build(doc,false,fixture);
        return doc with { Narration=NarrationFixtures.Package(match) };
    }
    internal static string Json(MatchFixture fixture)=>JsonSerializer.Serialize(Control(fixture),AuthoredJsonContext.Default.AuthoredMatchDocument);
    [Theory]
    [InlineData("liverpool","chelsea")]
    [InlineData("manchester-united","tottenham-hotspur")]
    public void Verified_nonhistorical_fixture_controls_pass_full_quality_and_all_asof_consumers(string home,string away)
    {
        var fixture=FixtureCatalog.Create(home,away);
        var doc=Control(fixture);var match=AuthoredMatchPlan.Parse(Json(fixture),expectedFixture:fixture);
        Assert.Equal(new[]{home,away},match.Teams.Select(t=>t.Id));
        Assert.Equal(fixture.Players.Select(p=>p.Name),match.Players.Select(p=>p.Name));
        AuthoredPathQuality.Validate(match);
        Assert.Equal(1,Replay.At(match,300).Score[home]);Assert.Equal(0,Replay.At(match,300).Score[away]);
        Assert.Equal(0,Replay.At(match,85.9).Score[home]);Assert.Equal(1,Replay.At(match,86).Score[home]);
        Assert.All(match.Players.Where(p=>match.IsKeeper(p.Id)),p=>Assert.Equal("goalkeeper",p.Role));
        Assert.Single(GoalHighlights.For(match));
        Assert.DoesNotContain(NarrativeReplay.At(match,85),c=>c.Kind=="goal");
        Assert.Equal("Takes the lead",NarrativeReplay.Consequence(match,match.Narrative!.Chapters.Single(c=>c.Kind=="goal")));
        var moment=MomentEvidence.Capture(match,2,86,match.Events.First(e=>e.Kind==EventKind.Goal).Id);
        Assert.Equal(home,moment.Facts.Clubs![0].Id);
        Assert.False(MomentRecording.Available(match,1));Assert.False(DirectorRecording.Available(match,1,300));
        var director=DirectorEvidence.Capture(match,2,300);Assert.Equal(away,director.Facts.Clubs![1].Id);
        var cuts=new[]{86d,125,199,285}.Select((time,i)=>
        {
            var focus=match.Events.Last(e=>e.Time<=time&&e.Kind!=EventKind.Kickoff);
            return new FilmScene("scene-"+i,focus.Time-10,focus.Time,1,3,i==0?"finish":"tactical",focus.Id,[focus.Id],"The recorded sequence changes the observed route.");
        }).ToArray();
        var plan=director.Validate(DirectorTests.Json(new(director.Hash,2,300,cuts)));
        Assert.Equal(52,FilmTimeline.Duration(plan));
        Assert.Equal(1,Replay.At(match,FilmTimeline.At(plan,10).SourceTime).Score[home]);
        var answer=new MomentAnswer(moment.Hash,[new("The recorded finish changes the score.",[moment.FocusId])],
            [new("The observed sequence suggests a central attacking connection.",[moment.FocusId])]);
        Assert.Equal(moment.Hash,moment.Validate(JsonSerializer.Serialize(answer,MomentJsonContext.Default.MomentAnswer)).SnapshotHash);
        Assert.NotEqual(NarrationRecording.FactsHash,NarrationPrompt.Hash(match));
        Assert.True(MatchVariety.NearDuplicate(MatchVariety.Signature(AuthoredMatchPlan.Parse(VarietyTests.OneSided())),MatchVariety.Signature(match)),
            "Changing club/player identities alone cannot make copied gameplay new.");
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(doc,false,FixtureCatalog.Create(away,home)));
        var bad=doc with { Fixture=fixture with { Players=fixture.Players.Select((p,i)=>i==0?p with { Name="Invented" }:p).ToArray() } };
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(bad,false));
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(doc with { Fixture=fixture with { Home=null! } },false));
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(doc with { Fixture=fixture with { Players=null! } },false));
        Assert.Throws<ScenarioValidationException>(()=>AuthoredMatchPlan.Build(doc with { Players=doc.Players.Select((p,i)=>i==0?p with { Id="arsenal-raya" }:p).ToArray() },false));
        Assert.Contains(home,AuthoredMatchPrompt.For(fixture));Assert.DoesNotContain("Arsenal-ManchesterCity",AuthoredMatchPrompt.For(fixture));
    }
    [Fact]
    public void Rotation_changes_unordered_pair_in_memory_and_after_history_reload()
    {
        var history=new MatchVariety();var first=history.Next();
        Assert.DoesNotContain(first.Fixture!.Home.Id,new[]{"arsenal","manchester-city"});
        Assert.DoesNotContain(first.Fixture.Away.Id,new[]{"arsenal","manchester-city"});
        var second=history.Next();Assert.NotEqual(first.Fixture.PairKey,second.Fixture!.PairKey);
        var match=AuthoredMatchPlan.Parse(Json(first.Fixture));
        history.Accept(match);
        var restored=new MatchVariety(history.Recent);
        Assert.NotEqual(first.Fixture.PairKey,restored.Next().Fixture!.PairKey);
        string? persisted=null;
        var pending=new MatchVariety(previousPair:first.Fixture.PairKey,saveSelection:pair=>persisted=pair);
        var attempt=pending.Next();
        Assert.Equal(attempt.Fixture!.PairKey,persisted);
        var relaunch=new MatchVariety(previousPair:persisted);
        Assert.NotEqual(persisted,relaunch.Next().Fixture!.PairKey);
        Assert.Equal(6,FixtureCatalog.Clubs.Count);
    }
    [Fact]
    public async Task Captured_fixture_survives_full_pipeline_and_rejects_switched_metadata_before_commit()
    {
        var fixture=FixtureCatalog.Create("liverpool","chelsea");
        var doc=Control(fixture) with { Narration=null };
        var raw=JsonSerializer.Serialize(doc with { Fixture=null },AuthoredJsonContext.Default.AuthoredMatchDocument);
        var calls=0;
        var output=await AuthoredGenerationPipeline.RunAsync(_=>Task.FromResult(raw),
            (_,_,_)=>throw new InvalidOperationException("No correction expected."),
            (match,_)=>{calls++;return Task.FromResult(NarrationFixtures.Json(match,JsonSerializer.Serialize(
                new CommentaryDocument(CommentaryPrompt.Hash(match),Control(fixture).Narration!.Cues),CommentaryJsonContext.Default.CommentaryDocument)));},
            null,CancellationToken.None,fixture:fixture);
        var delivered=AuthoredMatchPlan.Parse(output,expectedFixture:fixture);
        Assert.Equal(fixture.Players.Select(p=>p.Id),delivered.Players.Select(p=>p.Id));Assert.Equal(1,calls);
        var switched=doc with { Fixture=FixtureCatalog.Create("chelsea","liverpool") };
        await Assert.ThrowsAsync<ScenarioValidationException>(()=>AuthoredGenerationPipeline.RunAsync(
            _=>Task.FromResult(JsonSerializer.Serialize(switched,AuthoredJsonContext.Default.AuthoredMatchDocument)),
            (_,_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),null,CancellationToken.None,fixture:fixture));
    }
    [Fact]
    public async Task Single_shape_correction_preserves_new_fixture_and_all_captured_events()
    {
        var fixture=FixtureCatalog.Create("manchester-united","tottenham-hotspur");
        var ids=RegulationRoster.Players.Zip(fixture.Players).ToDictionary(p=>p.First.Id,p=>p.Second.Id);
        var original=AuthoredMatchPlan.Document(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","rejected-depth-paths.json")));
        var mapped=original with { Fixture=null,InitialOwnerId=ids[original.InitialOwnerId],
            Players=original.Players.Select(p=>p with { Id=ids[p.Id] }).ToArray(),
            Events=original.Events.Select(e=>e with { ActorId=ids[e.ActorId],ReceiverId=e.ReceiverId is null?null:ids[e.ReceiverId] }).ToArray() };
        var correction=JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ai-depth-correction.json")),
            CorrectionJsonContext.Default.AuthoredPathCorrection)!;
        var expected=AuthoredMatchPlan.Build(mapped with { Fixture=fixture,Players=correction.Players.Select(p=>p with { Id=ids[p.Id] }).ToArray() },false);
        var caption=JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","corrected-commentary.json")),
            CommentaryJsonContext.Default.CommentaryDocument)!;
        var safeCues=caption.Cues.Select(c=>c with { PlayerIds=c.PlayerIds.Select(id=>ids[id]).ToArray(),
            Text=expected.Events.Single(e=>e.Id==c.EventId).Kind==EventKind.Goal?"The recorded shot ends in the goal.":"The recorded ball changes route." }).ToArray();
        var timing=JsonSerializer.Serialize(new CommentaryDocument(CommentaryPrompt.Hash(expected),safeCues),CommentaryJsonContext.Default.CommentaryDocument);
        var corrections=0;
        var output=await AuthoredGenerationPipeline.RunAsync(_=>Task.FromResult(JsonSerializer.Serialize(mapped,AuthoredJsonContext.Default.AuthoredMatchDocument)),
            (document,_,_)=>{corrections++;Assert.Equal(fixture.Id,document.Fixture!.Id);Assert.Equal(mapped.Events,document.Events);
                return Task.FromResult(JsonSerializer.Serialize(new AuthoredPathCorrection(correction.Players.Select(p=>p with { Id=ids[p.Id] }).ToArray()),CorrectionJsonContext.Default.AuthoredPathCorrection));},
            (match,_)=>Task.FromResult(NarrationFixtures.Json(match,timing)),null,CancellationToken.None,fixture:fixture);
        var delivered=AuthoredMatchPlan.Parse(output,expectedFixture:fixture);
        Assert.Equal(1,corrections);Assert.Equal(fixture.Players.Select(p=>p.Id),delivered.Players.Select(p=>p.Id));
        Assert.Equal(mapped.Events.Select(e=>(e.Id,e.Time,e.ActorId)),delivered.Events.Select(e=>(e.Id,e.Time,e.ActorId)));
    }
}
