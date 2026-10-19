using PitchLens.Core;

namespace PitchLens.Generation;

public static class FiveMinuteRecording
{
    public const string Model = "gpt-6.1-sol";
    public static string Json()
    {
        using var stream = typeof(FiveMinuteRecording).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_300_plan.json")
            ?? throw new InvalidOperationException("The verified five-minute Sol recording is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static Match Create() => FiveMinutePlan.Parse(Json());
}
