using PitchLens.Core;

namespace PitchLens.Generation;

public static class NarrationRecording
{
    public const string FactsHash = "318b2f66780d03907762d80568c9c5228947446aa5617660e40efa0e381aae72";
    public static string Json()
    {
        using var stream = typeof(NarrationRecording).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_11v11_narration.json")
            ?? throw new InvalidOperationException("The validated recorded narration/story package is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static Match Create() => NarrationPrompt.Attach(Json(), AuthoredMatchRecording.Create());
}
