namespace PitchLens.Generation;

// Recording validation uses these contracts without shipping the desktop SDK on iOS.
public sealed partial class CopilotScenarioSource
{
    public const string Model = "gpt-6.1-sol";
}

public sealed partial class CopilotMomentSource
{
    private const string ClaimSchema = """
        {"type":"object","additionalProperties":false,"required":["text","evidenceIds"],"properties":{
        "text":{"type":"string","minLength":1,"maxLength":280},
        "evidenceIds":{"type":"array","minItems":1,"maxItems":4,"items":{"type":"string"}}}}
        """;
    public static string Schema => """
        {"type":"object","additionalProperties":false,"required":["snapshotHash","fan","analyst"],"properties":{
        "snapshotHash":{"type":"string"},"fan":{"type":"array","minItems":1,"maxItems":4,"items":
        """ + ClaimSchema + """ 
        },"analyst":{"type":"array","minItems":1,"maxItems":4,"items":
        """ + ClaimSchema + "}}}";
}
