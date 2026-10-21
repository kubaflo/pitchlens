using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Tools;

// Offline causal probe only. This helper is never linked into the product.
public static class MotionWitnessInput
{
    public static AuthoredMatchDocument Perturb(AuthoredMatchDocument original,string playerId,double time,double dy=3,double dx=0)
    {
        var player=original.Players.Single(p=>p.Id==playerId);
        if(player.Frames.Any(f=>f[0]==time||f[0]==time-1))
            throw new ArgumentException("Choose an interior probe time without existing knots.");
        var path=new Trajectory(player.Frames.Select(f=>new Keyframe(f[0],new(f[1],f[2]))));
        var a=path.At(time-1);var b=path.At(time);
        var frames=player.Frames.Concat(new[]{new[]{time-1,a.X,a.Y},new[]{time,b.X+dx,b.Y+dy}}).OrderBy(f=>f[0]).ToArray();
        return original with{Players=original.Players.Select(p=>p.Id==playerId?p with{Frames=frames}:p).ToArray()};
    }
}
