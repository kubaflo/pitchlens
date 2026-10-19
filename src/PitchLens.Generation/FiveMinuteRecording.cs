using PitchLens.Core;

namespace PitchLens.Generation;

public static class FiveMinuteRecording
{
    public const string Model = "gpt-6.1-sol";
    public static string Json() => Resource("recorded_300_plan.json");
    public static string CommentaryJson() => Resource("recorded_300_commentary.json");
    private static string Resource(string name)
    {
        using var stream = typeof(FiveMinuteRecording).Assembly.GetManifestResourceStream("PitchLens.Generation." + name)
            ?? throw new InvalidOperationException($"The verified five-minute Sol resource {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static Match Create() => CommentaryPrompt.Attach(CommentaryJson(), FiveMinutePlan.Parse(Json(), requireCommentary: false));
}
