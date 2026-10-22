using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record AuthoredShapeIssue(string TeamId, double Time, double Width, double Depth);
public sealed record AuthoredMotionIssue(string PlayerId,double Time,string Kind,double Actual,double Maximum,Keyframe[] Frames);
public sealed record AuthoredQualityMeasure(string SubjectId,double Actual,double Limit,string Unit,string Bound);
public sealed record AuthoredPlayerDistance(string PlayerId,string TeamId,double Metres);
public sealed record AuthoredQualityIssue(string Kind,double Start,double End,string[] PlayerIds,AuthoredQualityMeasure[] Measures,
    AuthoredPlayerDistance[]? PlayerDistances=null,double? QualifyingDistanceMetres=null);
public sealed class AuthoredPathValidationException : ScenarioValidationException
{
    public IReadOnlyList<AuthoredShapeIssue> Issues { get; }
    public IReadOnlyList<AuthoredMotionIssue> MotionIssues { get; }
    public IReadOnlyList<AuthoredQualityIssue> QualityIssues { get; }
    public AuthoredPathValidationException(IReadOnlyList<AuthoredShapeIssue> issues)
        : base(FormattableString.Invariant($"AI path shape failed for {issues[0].TeamId} at {issues[0].Time:0.#}s: width {issues[0].Width:0.###}m (minimum 30), depth {issues[0].Depth:0.###}m (minimum 22)."))
    { Issues=issues;MotionIssues=[];QualityIssues=[]; }
    public AuthoredPathValidationException(IReadOnlyList<AuthoredMotionIssue> issues)
        : base(FormattableString.Invariant($"AI player motion failed for {issues[0].PlayerId} at {issues[0].Time:0.###}s: {issues[0].Kind} {issues[0].Actual:0.###} (maximum {issues[0].Maximum:0.###})."))
    { Issues=[];MotionIssues=issues;QualityIssues=[]; }
    public AuthoredPathValidationException(IReadOnlyList<AuthoredShapeIssue> shapes,IReadOnlyList<AuthoredMotionIssue> motion,
        IReadOnlyList<AuthoredQualityIssue> quality)
        :base(motion.Count>0?new AuthoredPathValidationException(motion).Message:quality.Count>0?
            FormattableString.Invariant($"AI path quality failed: {quality[0].Kind} during {quality[0].Start:0.###}-{quality[0].End:0.###}s. ")+
                string.Join("; ",quality[0].Measures.Select(m=>FormattableString.Invariant($"{m.SubjectId}: {m.Actual:0.###} {m.Unit}, {m.Bound} {m.Limit:0.###}"))):
            new AuthoredPathValidationException(shapes).Message)
    {Issues=shapes;MotionIssues=motion;QualityIssues=quality;}
}

public static class AuthoredPathQuality
{
    private static void Require(bool valid, string message) { if (!valid) throw new ScenarioValidationException(message); }
    public static void Validate(Match match,IEnumerable<AuthoredQualityIssue>? earlierIssues=null)
    {
        var qualityIssues=earlierIssues?.ToList()??[];
        void Check(bool valid,string kind,double start,double end,IEnumerable<Player> subjects,params AuthoredQualityMeasure[] measures)
        {if(!valid)qualityIssues.Add(new(kind,start,end,subjects.Select(p=>p.Id).ToArray(),measures));}
        Require(match.Players.Count == 22 && match.Teams.All(t => match.Players.Count(p => p.TeamId == t.Id) == 11), "Expected 11 players per team.");
        foreach (var player in match.Players)
        {
            var frames = player.Path.Frames;
            Require(frames.Zip(frames.Skip(1), (a, b) => b.Time - a.Time).All(gap => gap <= 12), "Authored knots must cover active detail, max12s gap.");
        }
        var motionIssues=new List<AuthoredMotionIssue>();
        foreach(var player in match.Players)
        {
            var frames=player.Path.Frames;
            for(var i=1;i<frames.Count;i++)
            {
                var a=frames[i-1];var b=frames[i];
                var speed=a.Position.DistanceTo(b.Position)/(b.Time-a.Time);
                Require(double.IsFinite(speed),$"Nonfinite player speed for {player.Id}; invalid numerical input is not repair eligibility.");
                if(speed>8+1e-8)motionIssues.Add(new(player.Id,b.Time,"speed m/s",speed,8,[a,b]));
            }
            for (var i = 2; i < frames.Count; i++)
            {
                var a = frames[i - 2]; var b = frames[i - 1]; var c = frames[i];
                var v1 = new Position((b.Position.X - a.Position.X) / (b.Time - a.Time), (b.Position.Y - a.Position.Y) / (b.Time - a.Time));
                var v2 = new Position((c.Position.X - b.Position.X) / (c.Time - b.Time), (c.Position.Y - b.Position.Y) / (c.Time - b.Time));
                var acceleration=v1.DistanceTo(v2)/((c.Time-a.Time)/2);
                Require(double.IsFinite(acceleration),$"Nonfinite velocity change for {player.Id}; invalid numerical input is not repair eligibility.");
                if(acceleration>2.5)motionIssues.Add(new(player.Id,b.Time,"velocity change m/s²",acceleration,2.5,[a,b,c]));
            }
        }
        SustainedQuality.Validate(match,qualityIssues);
        Require(match.Events.Count(e => e.Kind == EventKind.Carry) >= 8, "11v11 action needs varied controlled carries, not only passing.");
        foreach (var team in match.Teams)
        {
            var outfield = match.Players.Where(p => p.TeamId == team.Id && !match.IsKeeper(p.Id)).ToArray();
            double Line(string role) => outfield.Where(p => p.Role == role).Average(p => p.Path.At(0).X);
            var direction=match.AttacksRight(team.Id)?1:-1;
            Check(direction*(Line("midfielder")-Line("defender"))>0&&direction*(Line("forward")-Line("midfielder"))>0,
                "initial-role-order",0,0,outfield,new(team.Id,direction*(Line("midfielder")-Line("defender")),0,"midfield minus defence metres","strict minimum"),
                new(team.Id,direction*(Line("forward")-Line("midfielder")),0,"attack minus midfield metres","strict minimum"));
            var turning=outfield.Count(p=>StoryQuality.DirectionChanges(p.Path)>=2);
            Check(turning>=6,"individual-direction-changes",0,300,outfield,new AuthoredQualityMeasure(team.Id,turning,6,"players with >=2 purposeful turns","minimum"));
            int BurstPlayers(double speed) => outfield.Count(p => p.Path.Frames.Zip(p.Path.Frames.Skip(1),
                (a, b) => (Duration: b.Time - a.Time, Speed: a.Position.DistanceTo(b.Position) / (b.Time - a.Time)))
                .Any(segment => segment.Duration >= 2 && segment.Speed >= speed));
            var runners=BurstPlayers(3);var sprinters=BurstPlayers(4);
            Check(runners>=4&&sprinters>=2,"running-bursts",0,300,outfield,
                new(team.Id,runners,4,"players with >=3m/s for >=2s","minimum"),new(team.Id,sprinters,2,"players with >=4m/s for >=2s","minimum"));
            Require(match.Events.Count(e => e.Kind == EventKind.Carry && match.Player(e.ActorId).TeamId == team.Id) >= 3,
                "Both clubs need controlled carries.");
            Require(match.Events.Where(e => match.Player(e.ActorId).TeamId == team.Id && !match.IsKeeper(e.ActorId))
                .Select(e => e.ActorId).Distinct().Count() >= 7, "New outfield players must participate, not decorate the old6v6 match.");
        }
        int synchronized = 0;
        var shapeIssues = new List<AuthoredShapeIssue>();
        for (double t = 0; t <= 300; t++)
        {
            var points = match.Players.Select(p => p.Path.At(t)).ToArray();
            var collisions = points.SelectMany((p, i) => points.Skip(i + 1).Select(p.DistanceTo)).Count(d => d < .4);
            Check(collisions<=1,"close-overlap",t,t,match.Players,new AuthoredQualityMeasure("all",collisions,1,"pairs closer than 0.4m","maximum"));
            var cluster=points.Max(p=>points.Count(q=>p.DistanceTo(q)<5));
            Check(cluster<=5,"player-pile-up",t,t,match.Players,new AuthoredQualityMeasure("all",cluster,5,"players within 5m including self","maximum"));
            foreach (var team in match.Teams)
            {
                var outfield = match.Players.Where(p => p.TeamId == team.Id && !match.IsKeeper(p.Id)).ToArray();
                var shape = outfield.Select(p => p.Path.At(t)).ToArray();
                var width = shape.Max(p => p.Y) - shape.Min(p => p.Y);
                var depth = shape.Max(p => p.X) - shape.Min(p => p.X);
                if (width < 30 || depth < 22) shapeIssues.Add(new(team.Id, t, width, depth));
                if (t > 0 && t % 5 == 0)
                {
                    var vectors = outfield.Select(p => new Position(p.Path.At(t).X - p.Path.At(t - 5).X, p.Path.At(t).Y - p.Path.At(t - 5).Y)).ToArray();
                    if (vectors.Any(v => v.DistanceTo(default) > 2 && vectors.Count(w => v.DistanceTo(w) < .3) >= 8)) synchronized++;
                }
            }
        }
        Check(synchronized<8,"synchronized-translation",0,300,match.Players,
            new AuthoredQualityMeasure("all",synchronized,8,"team/sample synchronized translations","strict maximum"));
        if(shapeIssues.Count>0||motionIssues.Count>0||qualityIssues.Count>0)
            throw new AuthoredPathValidationException(shapeIssues.AsReadOnly(),motionIssues.AsReadOnly(),qualityIssues.AsReadOnly());
    }
}
