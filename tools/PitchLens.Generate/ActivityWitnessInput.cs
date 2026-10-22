using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Tools;

// Controlled offline input only; never linked into the product or claimed as a failed Live response.
public static class ActivityWitnessInput
{
    public static AuthoredMatchDocument FreezeAwayWindow(AuthoredMatchDocument original,double start=200,double end=220)
    {
        if(start!=200||end!=220)throw new ArgumentException("This bounded probe targets exactly 200–220s.");
        var fixture=original.Fixture??FixtureCatalog.Historical;
        var away=fixture.Players.Where(p=>p.TeamId==fixture.Away.Id&&p.Role!="goalkeeper").Select(p=>p.Id).ToHashSet();
        return original with{Players=original.Players.Select(p=>{
            if(!away.Contains(p.Id))return p;
            var path=new Trajectory(p.Frames.Select(f=>new Keyframe(f[0],new(f[1],f[2]))));
            var stopped=path.At(start);
            var times=p.Frames.Select(f=>f[0]).Concat([start,end]).Distinct().Order();
            var frames=times.Select(t=>{var point=t>=start&&t<=end?stopped:path.At(t);return new[]{t,point.X,point.Y};}).ToArray();
            if(frames.Length>81)throw new ArgumentException("Probe would exceed the existing knot count.");
            return p with{Frames=frames};
        }).ToArray()};
    }
}
