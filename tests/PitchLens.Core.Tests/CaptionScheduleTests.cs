using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class CaptionScheduleTests
{
    [Fact]
    public void Exact_test_recording_opportunities_are87slots289seconds_with_at_most4second_interior_blanks()
    {
        var match=AuthoredMatchRecording.Create();
        var slots=CaptionSchedule.For(match);var stats=CaptionSchedule.Measure(slots,300);
        Assert.Equal(87,stats.Count);Assert.Equal(289,stats.Seconds);
        Assert.Equal(289d/300,stats.Fraction);Assert.Equal(4,stats.MaximumInteriorGap);
        Assert.Equal(3,stats.HeadGap);Assert.Equal(0,stats.TailGap);
        foreach(var slot in slots)
        {
            Assert.Equal(match.Events.Single(e=>e.Id==slot.EventId).Time,slot.Start);
            Assert.InRange(slot.End-slot.Start,2,6);
            Assert.All(match.Events.Where(e=>e.Kind==EventKind.Goal&&e.Time==slot.Start),g=>Assert.Equal(g.Id,slot.EventId));
        }
        Assert.Equal("e28",slots.Single(s=>s.Start==86).EventId);
        Assert.DoesNotContain(slots,s=>s.EventId=="e27");
        Assert.All(Enumerable.Range(0,5),minute=>Assert.True(slots.Count(s=>s.Start>=minute*60&&s.Start<(minute+1)*60)>=2));
        using var schema=JsonDocument.Parse(DenseNarrationPrompt.Schema);
        Assert.Equal(2,schema.RootElement.GetProperty("properties").GetProperty("captionContract").GetProperty("const").GetInt32());
    }
    [Fact]
    public void Dense_version_checks_exact_slots_text_readability_and_future_refs_while_legacy_remains_explicit()
    {
        var match=AuthoredMatchRecording.Create();var package=NarrationFixtures.Package(match);
        var valid=NarrationPrompt.Attach(package,match);
        Assert.Equal(2,valid.Commentary!.Contract);Assert.Equal(87,valid.Commentary.Cues.Count);
        Assert.Equal(1,NarrationRecording.Create().Commentary!.Contract);
        var first=package.Cues[0];
        foreach(var bad in new[]{
            first with{Start=first.Start+.1},first with{End=first.End+.1},
            first with{EventId="unknown"},first with{Text=new string('x',66)},
            first with{Text="Find the winger and pass forward."},
            first with{Text="The ball goes into another part of the pitch again and again."},
            first with{EvidenceIds=[first.EventId,"e28"]},
            first with{Text="An opener follows that pass."}
        })
        {
            var cues=package.Cues.ToArray();cues[0]=bad;
            Assert.Throws<ScenarioValidationException>(()=>NarrationPrompt.Attach(package with{Cues=cues},match));
        }
        Assert.Throws<ScenarioValidationException>(()=>NarrationPrompt.Attach(package with{Cues=package.Cues.Skip(1).ToArray()},match));
        Assert.Throws<ScenarioValidationException>(()=>NarrationPrompt.Attach(package with{CaptionContract=3},match));
    }
    [Fact]
    public void Measurement_rejects_nan_overlap_invalid_tail_and_reports_empty_without_fake_coverage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>CaptionSchedule.Measure([],double.NaN));
        Assert.Throws<ArgumentException>(()=>CaptionSchedule.Measure([new("x",double.NaN,3)],300));
        Assert.Throws<ArgumentException>(()=>CaptionSchedule.Measure([new("a",1,5),new("b",4,8)],300));
        Assert.Throws<ArgumentException>(()=>CaptionSchedule.Measure([new("a",299,301)],300));
        var empty=CaptionSchedule.Measure([],300);
        Assert.Equal(0,empty.Count);Assert.Equal(0,empty.Fraction);Assert.Equal(300,empty.HeadGap);
        Assert.Equal(300,empty.TailGap);
    }
    [Fact]
    public void Opportunity_count_is_event_based_not_fixed80_and_sparse_gaps_are_honest_not_filled()
    {
        var oneSided=AuthoredMatchPlan.Build(VarietyTests.OneSidedDocument(),false);
        var actual=CaptionSchedule.Measure(CaptionSchedule.For(oneSided),300);
        Assert.Equal(86,actual.Count);Assert.Equal(7,actual.MaximumInteriorGap);
        CaptionSchedule.RequireOpportunities(oneSided); // No observed event can fill the genuine gap; UI reports it.
        var old=AuthoredMatchRecording.Create();
        var sparse=new Match(300,old.Teams,old.Players,[],old.Ball,old.Possession);
        Assert.Throws<ArgumentException>(()=>CaptionSchedule.RequireOpportunities(sparse));
        Assert.NotEqual(40,CaptionSchedule.For(old).Count);
        var events=Enumerable.Range(0,45).Select(i=>new MatchEvent("slot-"+i,3+i*6.5,i,EventKind.Carry,
            old.Players[0].Id,StartTime:2+i*6.5));
        var fortyFive=new Match(300,old.Teams,old.Players,events,old.Ball,old.Possession);
        CaptionSchedule.RequireOpportunities(fortyFive);
        Assert.Equal(45,CaptionSchedule.For(fortyFive).Count);
        Assert.Equal(.9,CaptionSchedule.Measure(CaptionSchedule.For(fortyFive),300).Fraction);
        var lateEvents=Enumerable.Range(0,80).Select(i=>new MatchEvent("late-"+i,61+i*3,i,EventKind.Carry,
            old.Players[0].Id,StartTime:60+i*3));
        var late=new Match(300,old.Teams,old.Players,lateEvents,old.Ball,old.Possession);
        Assert.True(CaptionSchedule.Measure(CaptionSchedule.For(late),300).Fraction>.75);
        Assert.Contains("per-minute counts 0/",Assert.Throws<ArgumentException>(()=>CaptionSchedule.RequireOpportunities(late)).Message);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Dense_captions_do_not_own_or_change_playback_rate_pause_seek_or_duration(double rate)
    {
        var match=NarrationPrompt.Attach(NarrationFixtures.Package(AuthoredMatchRecording.Create()),AuthoredMatchRecording.Create());
        var clock=new Clock();using var replay=new ReplayPresentation(match,clock);
        replay.SetHighlights(false);replay.SetRate(rate);replay.Play();
        clock.Seconds=10;
        var frame=replay.Sample(true)!;
        Assert.Equal(rate*10,frame.Snapshot.Time);Assert.Equal(rate,frame.EffectiveRate);
        replay.Pause();clock.Seconds=100;Assert.Equal(rate*10,replay.Position);
        foreach(var time in new[]{86d,0,176,85.9,300,3})
        {
            replay.Seek(time);var cue=CommentaryReplay.At(match.Commentary,time);
            if(cue is not null)Assert.All(cue.EvidenceIds!,id=>Assert.True(match.Events.Single(e=>e.Id==id).Time<=time));
        }
    }
    private sealed class Clock:IMonotonicClock{public double Seconds{get;set;}}
}
