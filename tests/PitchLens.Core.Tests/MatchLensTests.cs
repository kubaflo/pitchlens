using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Queries=PitchLens.Core.MatchLenses;

namespace PitchLens.Core.Tests;

public sealed class MatchLensTests
{
    [Fact]
    public void Actual_recording_matches_independent_raw_path_and_pass_oracle_for_Saka26_to86seconds()
    {
        var match=DenseNarrationRecording.Create();var window=new ObservedWindow(1,"arsenal/manchester-city",26,86);
        var movement=Queries.Movement(match,window,"arsenal-saka");
        Assert.Equal(60,movement.Cells.Sum(c=>c.Seconds),10);
        Assert.Equal(71.2053357766537,movement.DistanceMetres!.Value,9);
        Assert.Equal(21.111111111111114,movement.Cells[3].Seconds,9);
        Assert.Equal(26.96031746031746,movement.Cells[4].Seconds,9);
        Assert.Equal(11.928571428571427,movement.Cells[5].Seconds,9);
        Assert.All(movement.Cells.Where(c=>c.Index is not(3 or 4 or 5)),c=>Assert.Equal(0,c.Seconds,10));
        var connections=Queries.Connections(match,window,"arsenal");
        Assert.Equal(8,connections.CompletedPasses);Assert.Equal(8,connections.Edges.Length);
        Assert.Equal(new[]{"e9","e18","e19","e21","e22","e23","e24","e26"}.Order(StringComparer.Ordinal),
            connections.Edges.SelectMany(e=>e.EventIds).Order(StringComparer.Ordinal));
        Assert.All(connections.Edges,e=>Assert.Equal(1,e.Count));
        var saka=connections.Nodes.Single(n=>n.PlayerId=="arsenal-saka");
        Assert.Equal(77.70370370370371,saka.X,9);Assert.Equal(12.5,saka.Y,9);Assert.Equal(3,saka.Samples);
    }
    internal static Match Known()
    {
        static Trajectory PlayerPath(double x)=>new([new(0,new(x,17)),new(100,new(x+10,17))]);
        var players=new[]{new Player("h1","home",1,"Home One",PlayerPath(10)),new Player("h2","home",2,"Home Two",PlayerPath(30)),
            new Player("a1","away",3,"Away One",PlayerPath(50)),new Player("a2","away",4,"Away Two",PlayerPath(70))};
        var events=new[]{
            new MatchEvent("p1",10,0,EventKind.Pass,"h1","h2",true,5),
            new MatchEvent("p2",20,0,EventKind.Pass,"h1","h2",true,15),
            new MatchEvent("intercept",30,0,EventKind.Pass,"h2","a1",false,25),
            new MatchEvent("reverse",40,0,EventKind.Pass,"h2","h1",true,35),
            new MatchEvent("away-pass",50,0,EventKind.Pass,"a1","a2",true,45)};
        var ball=new Trajectory(new[]{(0d,0d,0d),(5,10,10),(10,20,10),(15,30,10),(20,50,10),(25,60,10),
            (30,80,10),(35,80,30),(40,90,30),(45,80,40),(50,50,40),(100,50,40)}
            .Select(p=>new Keyframe(p.Item1,new(p.Item2,p.Item3))));
        return new(100,[new("home","Home"),new("away","Away")],players,events,ball,[new(0,"h1")]);
    }
    [Fact]
    public void Directed_counts_historical_release_receipt_averages_and_distances_are_exact_and_asof()
    {
        var match=Known();var window=new ObservedWindow(7,"home/away",10,40);
        var view=Queries.Connections(match,window,"home");
        Assert.Equal(3,view.CompletedPasses);Assert.Equal(1,view.InterceptedAttempts);Assert.Equal(2,view.Edges.Length);
        var pair=view.Edges.Single(e=>e.From=="h1"&&e.To=="h2");
        Assert.Equal(2,pair.Count);Assert.Equal(new[]{"p1","p2"},pair.EventIds);Assert.Equal(new[]{10d,20d},pair.PassMetres);
        var reverse=view.Edges.Single(e=>e.From=="h2");
        Assert.Equal(new[]{"reverse"},reverse.EventIds);Assert.Equal(new[]{10d},reverse.PassMetres);
        var h1=view.Nodes.Single(n=>n.PlayerId=="h1");var h2=view.Nodes.Single(n=>n.PlayerId=="h2");
        Assert.Equal(130d/3,h1.X,10);Assert.Equal(50d/3,h1.Y,10);Assert.Equal(3,h1.Samples);
        Assert.Equal(50,h2.X);Assert.Equal(50d/3,h2.Y,10);Assert.Equal(3,h2.Samples);
        Assert.NotEqual(match.Player("h1").Path.At(40).X,h1.X);
        var early=Queries.Connections(match,ObservedWindow.Capture(match,7,19,ObservedRange.MatchSoFar),"home");
        Assert.Equal(new[]{"p1"},Assert.Single(early.Edges).EventIds);
        var away=Queries.Connections(match,ObservedWindow.Capture(match,7,50,ObservedRange.MatchSoFar),"away");
        Assert.Equal("away-pass",Assert.Single(Assert.Single(away.Edges).EventIds));
        Assert.Empty(Queries.Connections(match,new(7,"home/away",0,0),"home").Nodes);
        Assert.Empty(Queries.Connections(match,new(7,"home/away",40.1,49),"home").Edges);
        Assert.Equal(1,Queries.Connections(match,new(7,"home/away",19,20),"home").CompletedPasses); // release before window
        Assert.Throws<ArgumentException>(()=>Queries.Connections(match,window with{Fixture="home/other"},"home"));
        Assert.Throws<ArgumentException>(()=>Queries.Connections(match,window,"unknown"));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ObservedWindow.Capture(match,1,double.NaN,ObservedRange.MatchSoFar));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ObservedWindow.Capture(match,1,101,ObservedRange.MatchSoFar));
        Assert.Throws<ArgumentException>(()=>Queries.Movement(match,window,"wrong-player"));
    }
    [Fact]
    public void Occupancy_integrates_seconds_not_knots_including_partial_segments_dwell_and_closed_right_edge()
    {
        var path=new Trajectory([new(0,new(0,8)),new(7,new(105,8)),new(10,new(105,8))]);
        var whole=Queries.Occupancy(path,0,10);
        Assert.Equal(10,whole.Sum(c=>c.Seconds),10);
        Assert.Equal(new[]{1d,1,1,1,1,1,4},whole.Take(7).Select(c=>Math.Round(c.Seconds,10)));
        Assert.All(whole.Skip(7),c=>Assert.Equal(0,c.Seconds));
        var part=Queries.Occupancy(path,1.5,5.25);
        Assert.Equal(new[]{0d,.5,1,1,1,.25,0},part.Take(7).Select(c=>Math.Round(c.Seconds,10)));
        Assert.Equal(3.75,part.Sum(c=>c.Seconds),10);
        var boundary=new Trajectory([new(0,new(15,17)),new(10,new(15,17))]);
        Assert.Equal(10,Queries.Occupancy(boundary,0,10).Single(c=>c.Seconds>0).Seconds);
        Assert.Equal(8,Queries.Occupancy(boundary,0,10).Single(c=>c.Seconds>0).Index);
        Assert.All(Queries.Occupancy(path,7,7),c=>Assert.Equal(0,c.Seconds));
        Assert.Throws<ArgumentOutOfRangeException>(()=>Queries.Occupancy(path,double.NaN,5));
        Assert.Throws<ArgumentOutOfRangeException>(()=>Queries.Occupancy(path,6,5));
        Assert.Throws<ArgumentOutOfRangeException>(()=>Queries.Occupancy(path,0,11));
        Assert.Throws<ArgumentException>(()=>Queries.Occupancy(new([new(0,new(double.NaN,0)),new(10,new(1,1))]),0,10));
    }
    [Fact]
    public void Corner_crossing_and_arbitrary_knot_subdivision_preserve_each_cell_and_no_frame_frequency_enters_metric()
    {
        static Position At(double t)=>new(105*t/28,68*t/28);
        var sparse=new Trajectory([new(0,At(0)),new(28,At(28))]);
        var dense=new Trajectory(new[]{0d,.1,1.3,4,7,7.2,14,19.9,21,27.99,28}.Select(t=>new Keyframe(t,At(t))));
        var expected=Queries.Occupancy(sparse,2.25,26.75);
        var actual=Queries.Occupancy(dense,2.25,26.75);
        Assert.Equal(24.5,actual.Sum(c=>c.Seconds),10);
        foreach(var cell in expected)Assert.Equal(cell.Seconds,actual[cell.Index].Seconds,9);
        foreach(var frequency in new[]{4,30,60,144})
        {
            for(var i=0;i<frequency;i++)Queries.Occupancy(dense,0,28*i/frequency);
            var sampled=Queries.Occupancy(dense,2.25,26.75);
            foreach(var cell in expected)Assert.Equal(cell.Seconds,sampled[cell.Index].Seconds,9);
        }
    }
    [Theory]
    [InlineData("arsenal","manchester-city")]
    [InlineData("liverpool","chelsea")]
    [InlineData("manchester-united","tottenham-hotspur")]
    public void Every_fixture_window_and_focus_stays_observed_with_both_clubs_goals(string home,string away)
    {
        var match=home=="arsenal"?DenseNarrationRecording.Create():
            AuthoredMatchPlan.Parse(FixtureTests.Json(FixtureCatalog.Create(home,away)),expectedFixture:FixtureCatalog.Create(home,away));
        foreach(var cutoff in new[]{0d,59,86,176,275,300,85.9,0})
        {
            var window=ObservedWindow.Capture(match,4,cutoff,ObservedRange.Last60Seconds);
            Assert.Equal(Math.Max(0,cutoff-60),window.Start);
            var connections=Queries.Connections(match,window,home);
            Assert.All(connections.Edges.SelectMany(e=>e.EventIds),id=>Assert.InRange(match.Events.Single(e=>e.Id==id).Time,window.Start,cutoff));
            var movement=Queries.Movement(match,window,match.Players[^1].Id);
            Assert.Equal(window.Duration,movement.Cells.Sum(c=>c.Seconds),8);
            var distance=match.Players[^1].Path.Measure(cutoff).Distance-match.Players[^1].Path.Measure(window.Start).Distance;
            Assert.Equal(cutoff==0?null:distance,movement.DistanceMetres);
            foreach(var focus in new[]{new StoryFocus(),new(StoryFocusKind.Club,home),new(StoryFocusKind.Player,match.Players[^1].Id)})
            {
                var story=Queries.Story(match,cutoff,focus);
                Assert.All(story,c=>Assert.True(c.Chapter.RevealTime<=cutoff));
                Assert.All(NarrativeReplay.At(match,cutoff).Where(c=>c.Kind=="goal"),g=>Assert.Contains(story,c=>c.Chapter.Id==g.Id));
                foreach(var c in story.Where(c=>c.Chapter.Kind!="goal"&&focus.Kind!=StoryFocusKind.All))
                    Assert.Contains(c.Chapter.EventIds,id=>{
                        var e=match.Events.Single(e=>e.Id==id);
                        return new[]{e.ActorId,e.ReceiverId}.OfType<string>().Any(p=>focus.Kind==StoryFocusKind.Player?p==focus.Id:match.Player(p).TeamId==focus.Id);
                    });
            }
        }
        Assert.False(Queries.FocusAvailable(match,new(StoryFocusKind.Player,"not-in-match")));
        Assert.Throws<ArgumentException>(()=>Queries.Story(match,86,new(StoryFocusKind.Club,"missing")));
    }
    [Fact]
    public void Connection_replay_resolves_canonical_pass_without_AI_and_rejects_stale_revision_and_future_window()
    {
        var forbidden=new Forbidden();using var service=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,mode:SessionMode.Test);
        service.Command("seek",100);var frame=service.Sample(true)!.Frame;
        var view=Queries.Connections(frame.Match,ObservedWindow.Capture(frame.Match,frame.Revision,100,ObservedRange.MatchSoFar),frame.Match.Teams[0].Id);
        var edge=view.Edges[0];var evidence=service.ConnectionEvidence(view,edge.Id);
        Assert.Equal(edge.EventIds[^1],evidence.FocusId);
        service.InspectEvidence(evidence,evidence.FocusId);
        Assert.Equal(evidence.Resolve(evidence.FocusId).Time,service.Sample(true)!.Frame.Snapshot.Time);
        Assert.False(service.Sample(true)!.Frame.Playing);Assert.Equal(0,forbidden.Calls);
        Assert.Throws<InvalidOperationException>(()=>service.ConnectionEvidence(view with{Window=view.Window with{Revision=99}},edge.Id));
        service.Command("seek",0);Assert.Throws<InvalidOperationException>(()=>service.ConnectionEvidence(view,edge.Id));
    }
    [Fact]
    public void Personal_story_exact_recorded_selection_retains_opposition_goals_and_mixed_club_turnovers()
    {
        var match=DenseNarrationRecording.Create();
        Assert.Equal(10,Queries.Story(match,300,new()).Count);
        Assert.Equal(new[]{"chapter-across-to-saka","chapter-doku-interception","chapter-width-reconnected",
            "chapter-gyokeres-goal","chapter-ait-nouri-turnover","chapter-haaland-equalizes"},
            Queries.Story(match,300,new(StoryFocusKind.Player,"arsenal-saka")).Select(c=>c.Chapter.Id));
        Assert.Equal(new[]{"chapter-doku-interception","chapter-gyokeres-goal","chapter-city-restart-route",
            "chapter-ait-nouri-turnover","chapter-haaland-equalizes","chapter-timber-intercepts","chapter-anderson-intercepts"},
            Queries.Story(match,300,new(StoryFocusKind.Club,"manchester-city")).Select(c=>c.Chapter.Id));
        var goalkeeper=Queries.Story(match,300,new(StoryFocusKind.Player,"arsenal-raya"));
        Assert.Equal(new[]{"chapter-gyokeres-goal","chapter-haaland-equalizes"},goalkeeper.Select(c=>c.Chapter.Id));
        Assert.All(goalkeeper,c=>Assert.True(c.GoalContext));
        Assert.Empty(Queries.Story(match,85.9,new(StoryFocusKind.Player,"arsenal-raya")));
        Assert.Single(Queries.Story(match,86,new(StoryFocusKind.Player,"arsenal-raya")));
        Assert.Empty(Queries.Story(match,0,new(StoryFocusKind.Club,"manchester-city")));
        Assert.Equal(87,match.Commentary!.Cues.Count);
        Assert.Equal(1,Replay.At(match,300).Score["arsenal"]);Assert.Equal(1,Replay.At(match,300).Score["manchester-city"]);
    }
    internal sealed class Forbidden:IScenarioSource,IMomentSource,IDirectorSource
    {
        public int Calls;
        public Task<string> GenerateAsync(IProgress<string>? p,CancellationToken t){Calls++;throw new InvalidOperationException("No SDK");}
        public Task<string> ExplainAsync(MomentEvidence e,CancellationToken t){Calls++;throw new InvalidOperationException("No SDK");}
        public Task<string> CreateAsync(DirectorEvidence e,IProgress<string>? p,CancellationToken t){Calls++;throw new InvalidOperationException("No SDK");}
    }
}
