using PitchLens.Core;

namespace PitchLens.Generation;

public static class AuthoredMatchRecording
{
    public const string Model = "gpt-6.1-sol";
    public static string Json() => Resource("recorded_11v11_paths.json");
    public static string CommentaryJson() => Resource("recorded_11v11_commentary_broadcast.json");
    private static string Resource(string name)
    {
        using var stream = typeof(AuthoredMatchRecording).Assembly.GetManifestResourceStream("PitchLens.Generation." + name)
            ?? throw new InvalidOperationException($"The verified 11v11 Sol resource {name} is missing; historical 6v6 data is not a substitute.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static Match Create() => CommentaryPrompt.Attach(CommentaryJson(), AuthoredMatchPlan.Parse(Json(), requireCommentary: false));
}
