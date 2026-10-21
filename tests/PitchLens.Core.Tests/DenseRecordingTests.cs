using System.Security.Cryptography;
using System.Text;
using PitchLens.Core;
using PitchLens.Generation;
using Xunit.Abstractions;

namespace PitchLens.Core.Tests;

public sealed class DenseRecordingTests(ITestOutputHelper output)
{
    [Fact]
    public void Exact_captured_Sol_package_passes_all_gates_with_geometry_backed_closer_to_goal()
    {
        var raw=DenseNarrationRecording.Json();
        Assert.Equal(DenseNarrationRecording.ResponseHash,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
        var original=AuthoredMatchRecording.Create();var e=original.Events.Single(e=>e.Id=="e25");
        Assert.Equal(EventKind.Carry,e.Kind);
        var right=original.AttacksRight(original.Player(e.ActorId).TeamId);
        var start=original.Ball.At(e.StartTime!.Value);var end=original.Ball.At(e.Time);
        var goal=new Position(right?105:0,34);
        output.WriteLine($"e25 start={e.StartTime} end={e.Time} attacksRight={right} ball={start}->{end} goalDistance={start.DistanceTo(goal)}->{end.DistanceTo(goal)}");
        Assert.True(end.DistanceTo(goal)<start.DistanceTo(goal)-.1);
        var match=DenseNarrationRecording.Create();
        Assert.Equal(NarrationRecording.FactsHash,match.Narrative!.FactsHash);
        Assert.Equal(2,match.Commentary!.Contract);
        Assert.Equal("Saka carries closer to goal.",match.Commentary.Cues.Single(c=>c.Id=="cue-e25").Text);
        var stats=CaptionSchedule.Measure(match.Commentary.Cues.Select(c=>new CaptionSlot(c.EventId,c.Start,c.End)),300);
        Assert.Equal(87,stats.Count);Assert.Equal(289,stats.Seconds);Assert.Equal(4,stats.MaximumInteriorGap);
        Assert.Equal(new[]{18,17,18,17,17},Enumerable.Range(0,5).Select(i=>match.Commentary.Cues.Count(c=>c.Start>=i*60&&c.Start<(i+1)*60)));
        Assert.All(match.Commentary.Cues,c=>{Assert.InRange(c.Text.Split(' ').Length,4,6);Assert.InRange(c.Text.Length,24,42);});
        Assert.All(original.Events.Where(e=>e.Kind==EventKind.Goal),g=>Assert.Contains(match.Commentary.Cues,c=>c.EventId==g.Id));
        Assert.Equal(original.Events,match.Events);Assert.Equal(original.Ball.Frames,match.Ball.Frames);
        foreach(var player in original.Players)Assert.Equal(player.Path.Frames,match.Player(player.Id).Path.Frames);
    }
}
