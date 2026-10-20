using PitchLens.Core;

namespace PitchLens.Generation;

public static class MomentRecording
{
    public const string Hash = "5ec74bf7d05569c2784b139de0a8c8bbc35f134a3cd801fa0724dae0ee1e13e1";
    public static bool Available(Match match, long revision) => revision == 1 &&
        match.Events.Any(e => e.Id == "e28" && e.Time == 86 && e.Kind == EventKind.Goal) &&
        MomentEvidence.Capture(match, revision, 86, "e28").Hash == Hash;

    public static ExplainedMoment Load(Match match, long revision, double observedThrough)
    {
        if (observedThrough < 86 || !Available(match, revision))
            throw new InvalidOperationException("Recorded analysis requires the matching observed startup goal.");
        var evidence = MomentEvidence.Capture(match, revision, 86, "e28");
        using var stream = typeof(MomentRecording).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_moment_goal.json")
            ?? throw new InvalidOperationException("Recorded explanation resource is missing.");
        using var reader = new StreamReader(stream);
        return new(evidence, evidence.Validate(reader.ReadToEnd()), 5, false);
    }
}
