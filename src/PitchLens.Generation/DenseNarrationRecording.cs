using PitchLens.Core;

namespace PitchLens.Generation;

public static class DenseNarrationRecording
{
    public const string ResponseHash="bb2b602c7c8dd2d582951ea12ba0b186669144d17904ebea4eb8b18e6d8f7dbb";
    public static string Json()
    {
        using var stream=typeof(DenseNarrationRecording).Assembly.GetManifestResourceStream("PitchLens.Generation.recorded_11v11_dense_narration.json")
            ??throw new InvalidOperationException("The validated recorded dense subtitle package is missing.");
        using var reader=new StreamReader(stream);
        return reader.ReadToEnd();
    }
    public static Match Create()=>NarrationPrompt.Attach(Json(),AuthoredMatchRecording.Create());
}
