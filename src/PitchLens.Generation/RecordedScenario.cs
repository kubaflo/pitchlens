namespace PitchLens.Generation;

public static class RecordedScenario
{
    public const string Model = "gpt-5.4";
    public static string Json()
    {
        using var stream = typeof(RecordedScenario).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_match.json")
            ?? throw new InvalidOperationException("The reviewed SDK match example is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static PitchLens.Core.Match Create() => ScenarioValidator.Parse(Json());
}
