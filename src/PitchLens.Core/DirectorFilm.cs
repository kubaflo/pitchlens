namespace PitchLens.Core;

public sealed record FilmScene(string Id, double SourceStart, double SourceEnd, double Rate, double HoldSeconds,
    string Camera, string FocusEventId, string[] EvidenceIds, string Text);
public sealed record FilmPlan(string FactsHash, long Revision, double Cutoff, FilmScene[] Scenes);
public sealed record FilmFrame(double FilmTime, double SourceTime, int SceneIndex, bool Holding, double Duration);

public static class FilmTimeline
{
    public static double SceneDuration(FilmScene scene) => (scene.SourceEnd - scene.SourceStart) / scene.Rate + scene.HoldSeconds;
    public static double Duration(FilmPlan plan) => plan.Scenes.Sum(SceneDuration);
    public static FilmFrame At(FilmPlan plan, double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        var duration = Duration(plan);
        var time = Math.Clamp(seconds, 0, duration);
        var offset = time;
        for (var i = 0; i < plan.Scenes.Length; i++)
        {
            var scene = plan.Scenes[i];
            var length = SceneDuration(scene);
            if (offset < length || i == plan.Scenes.Length - 1)
            {
                var footage = (scene.SourceEnd - scene.SourceStart) / scene.Rate;
                return new(time, Math.Min(scene.SourceEnd, scene.SourceStart + Math.Min(offset, footage) * scene.Rate),
                    i, offset >= footage, duration);
            }
            offset -= length;
        }
        throw new InvalidOperationException("Film has no scenes.");
    }
}

public sealed class FilmPlayback
{
    private readonly Playback playback;
    public FilmPlan Plan { get; }
    public FilmFrame Frame => FilmTimeline.At(Plan, playback.Position);
    public bool Playing => playback.IsPlaying;
    public double Position=>playback.Position;
    public void SetVoiceBoundary(bool enabled,double? boundary)=>playback.SetVoiceBoundary(enabled,boundary);
    public FilmPlayback(FilmPlan plan, IMonotonicClock clock)
    {
        Plan = plan;
        playback = new(FilmTimeline.Duration(plan), clock);
        playback.SetRate(1);
        playback.SetHighlights(false);
    }
    public void Play() => playback.Play();
    public void Pause() => playback.Pause();
    public void Seek(double time) => playback.Seek(time);
    public void Restart() { playback.Restart(); playback.Play(); }
}
