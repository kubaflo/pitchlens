using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record CommentaryDocument(string ScenarioHash, CommentaryCue[] Cues);
public sealed record CommentaryFact(string Id, double Time, string Kind, string ActorId, string ActorName,
    string Club, string? ReceiverId, string? ReceiverName, bool? Completed);
public sealed record CommentaryFacts(double Duration, string? Title, CommentaryFact[] Events);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CommentaryDocument))]
[JsonSerializable(typeof(CommentaryFacts))]
public partial class CommentaryJsonContext : JsonSerializerContext;

public static class CommentaryPrompt
{
    public const string Instructions = """
        Write actual AI-generated football BROADCAST commentary: sharp, rhythmic, expressive, not a database feed.
        All actions are synthetic; never imply real athlete performance. Use ONLY the provided event facts.
        Produce 36-48 concise cues spread throughout all five minutes, at least two in each minute and one for EVERY goal.
        Each cue: id(unique ASCII/hyphen),eventId, start,end,playerIds,text.
        Anchor to one completed event ID; start at that event's time or up to2s afterward; duration2-6s.
        Sort by start, NO overlaps. End<=300s. Skip late events if there isn't time for a2s cue.
        Include goal event IDs (not shot IDs) for goal captions. No future events, predictions, later outcomes, cumulative score or statistics.
        Plain text8-140 characters, one line, no markup or digits. Never use 'will', 'later', 'eventually', 'winner', 'prediction', 'finally'.
        Say 'goal', 'scores' or 'scored' ONLY on a goal event. Use only involved actor/receiver names and IDs;1-2playerIds.
        Use full roster names when naming players; club names are okay. Name only one participant when two names make a line clumsy.
        Mix clipped action-first calls, name-first calls and occasional longer lines. Vary the opening words and punctuation.
        A routine pass can be understated; an interception should snap; a goal deserves an unmistakable GOAL call and scorer spotlight.
        Do NOT repeatedly write '[full name] finds [full name]' or 'passes to'. Do not announce 'pass completed' like a spreadsheet.
        Aim for many punchy14-75-character lines, not uniform sentences. Short bursts and a well-placed exclamation have room to breathe.
        Emotion is welcome, invention is not: no unsupported assists, pressure, skill, direction, crowd, shot power, goal corner or metrics.
        Don't repeat the synthetic disclaimer in the commentary; the app labels the match. No fake quotation or real-world broadcast attribution.
        Make each cue feel like someone reacting to THAT observed event, not reading a roster list.
        JSON only. No tools or additional requests.
        """;
    public const string CueSchema = """
        {"type":"object","additionalProperties":false,"required":["id","eventId","start","end","playerIds","text"],
        "properties":{"id":{"type":"string"},"eventId":{"type":"string"},"start":{"type":"number"},"end":{"type":"number"},
        "playerIds":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"string"}},"text":{"type":"string","minLength":8,"maxLength":140}}}
        """;
    public static string Schema => """
        {"type":"object","additionalProperties":false,"required":["scenarioHash","cues"],"properties":{
        "scenarioHash":{"type":"string"},"cues":{"type":"array","minItems":24,"maxItems":64,"items":
        """ + CueSchema + "}}}";

    private static string Facts(Match match) => JsonSerializer.Serialize(new CommentaryFacts(match.Duration, match.Story?.Title,
        match.Events.Select(e => new CommentaryFact(e.Id, e.Time, e.Kind.ToString().ToLowerInvariant(), e.ActorId,
            match.Player(e.ActorId).Name, match.Teams.Single(t => t.Id == match.Player(e.ActorId).TeamId).Name,
            e.ReceiverId, e.ReceiverId is null ? null : match.Player(e.ReceiverId).Name, e.Completed)).ToArray()),
        CommentaryJsonContext.Default.CommentaryFacts);
    public static string Hash(Match match) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Facts(match))));
    public static string For(Match match) => Instructions + "\nCopy scenarioHash exactly: " + Hash(match) + "\nMatch facts:\n" + Facts(match);
    public static Match Attach(string json, Match match)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 40_000) throw new ArgumentException("Empty or oversized commentary.");
            var options = new JsonSerializerOptions(CommentaryJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 16 };
            var context = new CommentaryJsonContext(options);
            var document = JsonSerializer.Deserialize(json, context.CommentaryDocument) ?? throw new ArgumentException("Missing commentary.");
            if (document.ScenarioHash != Hash(match) || document.Cues is null) throw new ArgumentException("Commentary does not belong to this exact scenario.");
            return match.WithCommentary(document.Cues, CopilotScenarioSource.Model);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { throw new ScenarioValidationException($"Invalid AI commentary: {e.Message}", e); }
    }
}
