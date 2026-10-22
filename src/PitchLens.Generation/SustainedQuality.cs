using PitchLens.Core;

namespace PitchLens.Generation;

public static class SustainedQuality
{
    public static AuthoredQualityIssue MeasureActivity(Match match,double start)
    {
        if(!double.IsFinite(start)||start<0||start>280||start%10!=0)throw new ArgumentOutOfRangeException(nameof(start));
        var distances=match.Players.Where(p=>!StoryQuality.IsKeeper(p)).Select(p=>
            new AuthoredPlayerDistance(p.Id,p.TeamId,p.Path.Measure(start+20).Distance-p.Path.Measure(start).Distance)).ToArray();
        var moving=distances.Where(p=>p.Metres>=8).ToArray();
        return new("visible-movement",start,start+20,distances.Select(p=>p.PlayerId).ToArray(),
            [new("all-outfield",moving.Length,6,"qualifying outfield players","minimum"),
             ..match.Teams.Select(t=>new AuthoredQualityMeasure(t.Id,moving.Count(p=>p.TeamId==t.Id),2,"qualifying outfield players","minimum"))],
            distances,8);
    }
    public static void Validate(Match match,List<AuthoredQualityIssue>? pathIssues=null)
    {
        static void Require(bool valid, string message) { if (!valid) throw new ScenarioValidationException(message); }
        void PathCheck(bool valid,string message,AuthoredQualityIssue issue)
        {if(valid)return;if(pathIssues is null)throw new ScenarioValidationException(message);pathIssues.Add(issue);}
        Require(match.Duration == 300, "Expected a genuine 300-second match.");
        foreach (var player in match.Players)
        {
            PathCheck(player.Path.Measure(300).MaxSpeed<=8+1e-8,"Player exceeds 8 m/s.",
                new("player-speed",0,300,[player.Id],[new(player.Id,player.Path.Measure(300).MaxSpeed,8,"m/s (+1e-8 numerical tolerance)","maximum")]));
            if (StoryQuality.IsKeeper(player))
                foreach(var f in player.Path.Frames)
                    PathCheck((match.AttacksRight(player.TeamId)?f.Position.X<=18:f.Position.X>=87)&&f.Position.Y is >=18 and <=50,
                        "Goalkeeper at wrong end/outside own goal area.",new("keeper-area",f.Time,f.Time,[player.Id],
                            [new(player.Id,f.Position.X,match.AttacksRight(player.TeamId)?18:87,"x metres",match.AttacksRight(player.TeamId)?"maximum":"minimum"),
                             new(player.Id,f.Position.Y,18,"y metres","minimum"),new(player.Id,f.Position.Y,50,"y metres","maximum")]));
        }
        for (double t = 0; t <= 300; t += 5)
        {
            foreach (var team in match.Teams)
            {
                var points = match.Players.Where(p => p.TeamId == team.Id && !StoryQuality.IsKeeper(p)).Select(p => p.Path.At(t)).ToArray();
                var width=points.Max(p=>p.Y)-points.Min(p=>p.Y);var span=points.SelectMany(a=>points.Select(a.DistanceTo)).Max();
                PathCheck(width>=14&&span>=25,$"Team shape collapses at {t}s.",
                    new("sustained-team-span",t,t,match.Players.Where(p=>p.TeamId==team.Id&&!StoryQuality.IsKeeper(p)).Select(p=>p.Id).ToArray(),
                        [new(team.Id,width,14,"width metres","minimum"),new(team.Id,span,25,"maximum pair distance metres","minimum")]));
            }
        }
        for (double start = 0; start <= 280; start += 10)
        {
            var activity=MeasureActivity(match,start);
            PathCheck(activity.Measures.All(m=>m.Actual>=m.Limit),$"Insufficient visible movement during {start}-{start+20}s.",activity);
            Require(match.Events.Count(e => e.Time > start && e.Time <= start + 20 && e.Kind is EventKind.Pass or EventKind.Carry or EventKind.Shot) >= 2,
                $"No meaningful event distribution during {start}-{start + 20}s.");
        }
        Require(match.Events.Where(e => e.Kind is EventKind.Pass or EventKind.Carry or EventKind.Shot).Last().Time >= 295,
            "Match has an idle padded tail.");
        Require(match.Events.Count(e => e.Kind == EventKind.Pass && e.Completed == false) >= 4, "Match needs multiple genuine turnovers.");
        Require(match.Events.Any(e => e.Kind == EventKind.Goal), "This prototype requires at least one actual goal total.");
        foreach (var team in match.Teams)
        {
            for (double start = 0; start < 300; start += 60)
                Require(match.Events.Any(e => e.Kind == EventKind.Pass && e.Completed == true &&
                    match.Player(e.ActorId).TeamId == team.Id && e.Time > start && e.Time <= start + 60),
                    "Both clubs must participate in each minute.");
        }
        foreach (var e in match.Events)
        {
            if (e.Kind == EventKind.Carry)
                PathCheck(match.Player(e.ActorId).Path.Measure(e.Time).Distance -
                    match.Player(e.ActorId).Path.Measure(e.StartTime!.Value).Distance >= 3, "Carry must visibly move the ball.",
                    new("carry-distance",e.StartTime!.Value,e.Time,[e.ActorId],
                        [new(e.Id,match.Player(e.ActorId).Path.Measure(e.Time).Distance-match.Player(e.ActorId).Path.Measure(e.StartTime.Value).Distance,3,"carry metres","minimum")]));
            if (e.Kind is EventKind.Pass or EventKind.Shot)
            {
                var speed = match.Ball.At(e.StartTime!.Value).DistanceTo(match.Ball.At(e.Time)) / (e.Time - e.StartTime.Value);
                PathCheck(speed<=40,"Ball flight exceeds 40 m/s.",new("ball-flight-speed",e.StartTime!.Value,e.Time,
                    new[]{e.ActorId,e.ReceiverId}.OfType<string>().ToArray(),[new(e.Id,speed,40,"ball m/s","maximum")]));
                if (e.Kind == EventKind.Pass)
                    Require(match.Player(e.ReceiverId!).Path.At(e.Time).DistanceTo(match.Ball.At(e.Time)) < .001,
                        "Pass does not reach its receiver.");
            }
        }
    }
}
