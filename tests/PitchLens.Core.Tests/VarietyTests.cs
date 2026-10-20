using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;
using PitchLens.App;

namespace PitchLens.Core.Tests;

public sealed class VarietyTests
{
    internal static AuthoredMatchDocument OneSidedDocument()
    {
        var original = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        var goal = original.Events.Last(e => e.Kind == "goal");
        var restart = original.Events.First(e => e.Kind == "kickoff" && e.Time > goal.Time);
        return original with
        {
            Events = original.Events.Where(e => e.Id != goal.Id && e.Id != restart.Id).Select(e =>
                e.Kind == "shot" && e.ActorId == goal.ActorId && e.Time == goal.Time
                    ? e with { Kind = "pass", ReceiverId = restart.ActorId, Completed = false } : e).ToArray(),
            PhaseKinds = original.PhaseKinds.Select((kind, i) => i == 5 ? "turnover" : kind).ToArray()
        };
    }
    internal static string OneSided()
    {
        var raw = OneSidedDocument();
        var document = raw with { Players = raw.Players.Select(p => p with { Frames = p.Frames.Select(f => new[] { f[0], f[1], 68-f[2] }).ToArray() }).ToArray() };
        var match = AuthoredMatchPlan.Build(document, false);
        var timing = CommentaryPrompt.Attach(AuthoredMatchRecording.CommentaryJson(), AuthoredMatchRecording.Create()).Commentary!.Cues
            .Where(c => document.Events.Any(e => e.Id == c.EventId)).Select(c => c.EventId == "e54" ? c with { Text = "The ball changes hands.", PlayerIds = ["manchester-city-haaland"] } : c).ToArray();
        var narrated = match.WithCommentary(timing, CopilotScenarioSource.Model);
        return JsonSerializer.Serialize(document with { Narration = NarrationFixtures.Package(narrated) }, AuthoredJsonContext.Default.AuthoredMatchDocument);
    }
    [Fact]
    public void One_sided_result_passes_full_unchanged_22player_physics_shape_and_narrative_checks()
    {
        var match = AuthoredMatchPlan.Parse(OneSided());
        Assert.Equal(22, match.Players.Count); Assert.Equal(300, match.Duration);
        Assert.Equal(1, Replay.At(match, 300).Score["arsenal"]);
        Assert.Equal(0, Replay.At(match, 300).Score["manchester-city"]);
        AuthoredPathQuality.Validate(match);
        Assert.Equal(1, match.Narrative!.Chapters.Count(c => c.Kind == "goal"));
        Assert.DoesNotContain(NarrativeReplay.At(match, 85), c => c.Kind == "goal");
        Assert.Equal("Takes the lead", NarrativeReplay.Consequence(match, match.Narrative.Chapters.Single(c => c.Kind == "goal")));
        Assert.Single(GoalHighlights.For(match));
        Assert.True(DirectorEvidence.CanCapture(match, 300));
        var invalid = OneSidedDocument() with { Players = OneSidedDocument().Players.Select((p,i) => i==1 ? p with { Frames = p.Frames.Select(f => new[] { f[0], 52d, 34d }).ToArray() } : p).ToArray() };
        Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(invalid, false));
    }
    [Fact]
    public void Opposite_winner_is_valid_under_the_same_direction_onside_and_goal_evidence_contract()
    {
        var doc=OneSidedDocument();
        var ars=RegulationRoster.Players.Where(p=>p.TeamId=="arsenal").GroupBy(p=>p.Role).ToDictionary(g=>g.Key,g=>g.ToArray());
        var city=RegulationRoster.Players.Where(p=>p.TeamId=="manchester-city").GroupBy(p=>p.Role).ToDictionary(g=>g.Key,g=>g.ToArray());
        var ids=new Dictionary<string,string>();
        foreach(var role in ars.Keys)
            foreach(var pair in ars[role].Zip(city[role])) { ids[pair.First.Id]=pair.Second.Id;ids[pair.Second.Id]=pair.First.Id; }
        var switched=doc with
        {
            InitialOwnerId=ids[doc.InitialOwnerId],
            Players=doc.Players.Select(p=>new AuthoredPlayer(ids[p.Id],p.Frames.Select(f=>new[]{f[0],105-f[1],f[2]}).ToArray())).ToArray(),
            Events=doc.Events.Select(e=>e with { ActorId=ids[e.ActorId],ReceiverId=e.ReceiverId is null?null:ids[e.ReceiverId] }).ToArray()
        };
        var match=AuthoredMatchPlan.Build(switched,false);
        Assert.Equal(0,Replay.At(match,300).Score["arsenal"]);
        Assert.Equal(1,Replay.At(match,300).Score["manchester-city"]);
        AuthoredPathQuality.Validate(match);
        Assert.Single(GoalHighlights.For(match));
    }
    [Fact]
    public void Canonical_signature_ignores_titles_eventids_narration_and_detects_small_timing_route_copies()
    {
        var original = AuthoredMatchRecording.Create();
        var doc = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        var cosmetic = doc with { Title = "The Turning Tide", Events = doc.Events.Select((e,i) => e with { Id = "cosmetic-" + i }).ToArray() };
        var same = AuthoredMatchPlan.Build(cosmetic, false);
        Assert.Equal(MatchVariety.Signature(original).Hash, MatchVariety.Signature(same).Hash);
        var signature = MatchVariety.Signature(original);
        var near = signature with { Hash = "different-exact-hash", PositionCells = signature.PositionCells.Select((c,i) => i%7==0 ? c+1 : c).ToArray() };
        Assert.True(MatchVariety.NearDuplicate(signature, near));
        Assert.True(MatchVariety.NearDuplicate(signature, MatchVariety.Signature(AuthoredMatchPlan.Build(OneSidedDocument(), false))));
        var distinct = AuthoredMatchPlan.Parse(OneSided());
        Assert.False(MatchVariety.NearDuplicate(signature, MatchVariety.Signature(distinct)));
        var history = new MatchVariety();
        Assert.Throws<ScenarioValidationException>(() => history.Accept(same));
        history.Accept(distinct);
        Assert.Throws<ScenarioValidationException>(() => history.Accept(distinct));
        var first = history.Next(); var second = history.Next();
        Assert.NotEqual(first.RequestId, second.RequestId);
        Assert.NotEqual(first.Direction, second.Direction);
        Assert.Contains("Goals:", first.Instructions);
        Assert.Contains("either club may score none", first.Instructions);
    }
    [Fact]
    public async Task Context_reaches_source_and_duplicate_rejection_never_commits_or_retries()
    {
        var source = new Source(OneSided());
        var initial = NarrationRecording.Create();
        var service = new GenerationService(source, initial, variety: new MatchVariety());
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Succeeded, service.State);
        Assert.NotNull(source.Request);
        var valid = service.Current;
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Same(valid, service.Current);
        Assert.Contains("near-identical", service.Status);
        Assert.Equal(2, source.Calls);
    }
    [Fact]
    public async Task Metadata_is_bounded_culture_stable_and_a_write_failure_prevents_atomic_delivery()
    {
        var match=AuthoredMatchPlan.Parse(OneSided());
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var first=MatchVariety.Signature(match);
            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(first.Hash,MatchVariety.Signature(match).Hash);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture=culture; }
        var signature=MatchVariety.Signature(match);
        Assert.Throws<InvalidOperationException>(()=>new MatchVariety(Enumerable.Repeat(signature,9)));
        Assert.Throws<InvalidOperationException>(()=>new MatchVariety([signature with { PositionCells=[] }]));
        var initial=NarrationRecording.Create();
        var service=new GenerationService(new Source(OneSided()),initial,variety:new MatchVariety(save:_=>throw new IOException("Controlled metadata write error")));
        await service.GenerateAsync(null,CancellationToken.None);
        Assert.Equal(GenerationState.Failed,service.State);
        Assert.Same(initial,service.Current);
        Assert.Contains("metadata write",service.Status);
    }
    internal sealed class Source(string json) : IScenarioSource
    {
        public int Calls; public VarietyRequest? Request;
        public Task<string> GenerateAsync(IProgress<string>? p, CancellationToken t) { Calls++; return Task.FromResult(json); }
        public Task<string> GenerateAsync(VarietyRequest request, IProgress<string>? p, CancellationToken t)
        { Request = request; return GenerateAsync(p,t); }
    }
}
