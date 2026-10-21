namespace PitchLens.Core;

public sealed record CaptionSlot(string EventId,double Start,double End);
public sealed record CaptionCoverage(int Count,double Seconds,double Fraction,double MaximumInteriorGap,double HeadGap,double TailGap);

public static class CaptionSchedule
{
    public static IReadOnlyList<CaptionSlot> For(Match match)
    {
        var events=match.Events.GroupBy(e=>e.Time).Select(g=>g.FirstOrDefault(e=>e.Kind==EventKind.Goal)??g.First()).ToArray();
        var result=new List<CaptionSlot>();double previousEnd=0;
        for(var i=0;i<events.Length;i++)
        {
            var e=events[i];
            if(e.Time<previousEnd||events.Any(g=>g.Kind==EventKind.Goal&&g.Time>e.Time&&g.Time<e.Time+2))continue;
            var next=events.Skip(i+1).FirstOrDefault(n=>n.Time>=e.Time+2)?.Time??match.Duration;
            var end=Math.Min(match.Duration,Math.Min(e.Time+6,next));
            if(end-e.Time<2)continue;
            result.Add(new(e.Id,e.Time,end));previousEnd=end;
        }
        return result.AsReadOnly();
    }
    public static CaptionCoverage Measure(IEnumerable<CaptionSlot> slots,double duration)
    {
        if(!double.IsFinite(duration)||duration<=0)throw new ArgumentOutOfRangeException(nameof(duration));
        var items=slots.ToArray();double previous=0,total=0,maxGap=0;
        for(var i=0;i<items.Length;i++)
        {
            var slot=items[i];
            if(!double.IsFinite(slot.Start)||!double.IsFinite(slot.End)||slot.Start<previous||slot.Start<0||
                slot.End>duration||slot.End-slot.Start is <2 or >6)throw new ArgumentException("Invalid caption schedule bounds or overlap.");
            if(i>0)maxGap=Math.Max(maxGap,slot.Start-previous);
            total+=slot.End-slot.Start;previous=slot.End;
        }
        return new(items.Length,total,total/duration,maxGap,items.Length==0?duration:items[0].Start,duration-previous);
    }
    public static void RequireOpportunities(Match match)
    {
        var slots=For(match);var coverage=Measure(slots,match.Duration);
        var minuteCounts=Enumerable.Range(0,(int)Math.Ceiling(match.Duration/60))
            .Select(minute=>slots.Count(s=>s.Start>=minute*60&&s.Start<(minute+1)*60)).ToArray();
        if(coverage.Fraction<.75||minuteCounts.Any(count=>count<2)||
            match.Events.Where(e=>e.Kind==EventKind.Goal).Any(g=>!slots.Any(s=>s.EventId==g.Id)))
            throw new ArgumentException($"Observed caption opportunities are insufficient: {coverage.Count} slots, {coverage.Fraction:P1} coverage, {coverage.MaximumInteriorGap:0.#}s maximum interior gap, per-minute counts {string.Join("/",minuteCounts)}. No filler captions were invented.");
    }
    public static void Validate(Match match,IReadOnlyList<CommentaryCue> cues)
    {
        RequireOpportunities(match);
        var slots=For(match);
        if(cues.Count!=slots.Count)throw new ArgumentException($"Dense captions must fill all {slots.Count} observed slots, not a fixed cue count.");
        for(var i=0;i<slots.Count;i++)
        {
            var cue=cues[i];var slot=slots[i];
            if(cue is null||cue.Text is null||cue.PlayerIds is null)throw new ArgumentException("Dense cue text and participants are required.");
            if(cue.EventId!=slot.EventId||cue.Start!=slot.Start||cue.End!=slot.End)
                throw new ArgumentException($"Dense cue '{cue.Id}' must match observed slot {slot.EventId} at {slot.Start:0.###}–{slot.End:0.###}s exactly.");
            var words=cue.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length;
            if(cue.Text.Length>65||words is <2 or >9)throw new ArgumentException($"Dense cue '{cue.Id}' must be a readable2–9word call, at most65characters.");
            if(cue.PlayerIds.Length>3)throw new ArgumentException("Dense calls must not become roster-name lists.");
            if(new[]{"Push on","Take it","Find ","Bring ","Work ","Drive ","Step ","Advance ","Spread ","Sweep ","Keep ","Send "}
                .Any(prefix=>cue.Text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Dense cue '{cue.Id}' must react to football, not issue coaching commands.");
        }
        if(cues.Count(c=>c.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length is >=3 and <=8)<cues.Count*.75)
            throw new ArgumentException("At least75% of dense calls must be compact3–8word subtitles.");
    }
}
