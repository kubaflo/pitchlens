using PitchLens.Core;

namespace PitchLens.Generation;

public static class DirectorRecording
{
    public const string Hash = "6c912d0f09c54d9f297b70d910f16e4ef03a57866a670e030092426537e90bfe";
    public static bool Available(Match match, long revision, double observedThrough) =>
        revision == 1 && observedThrough >= 300 && match.Duration == 300 &&
        DirectorEvidence.CanCapture(match, 300) && DirectorEvidence.Capture(match, revision, 300).Hash == Hash;
    public static DirectedFilm Load(Match match, long revision, double observedThrough)
    {
        if (!Available(match, revision, observedThrough)) throw new InvalidOperationException("Recorded Director preview requires the matching startup scene observed through300s.");
        using var stream = typeof(DirectorRecording).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_director.json")
            ?? throw new InvalidOperationException("Recorded Director resource missing.");
        using var reader = new StreamReader(stream);
        var evidence = DirectorEvidence.Capture(match, revision, 300);
        return new(evidence, evidence.Validate(reader.ReadToEnd()), false);
    }
}
