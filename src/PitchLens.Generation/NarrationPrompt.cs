using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record NarrationDocument(string FactsHash, CommentaryCue[] Cues, NarrativeChapter[] Chapters);
public sealed record NarrationPlayer(string Id, string Name, string TeamId);
public sealed record NarrationFact(string Id, double Time, double Start, string Kind, string ActorId, string? ReceiverId,
    bool? Completed, MomentPoint[] Ball, double? CarryMetres, int ArsenalGoals, int CityGoals);
public sealed record NarrationFacts(double Duration, NarrationPlayer[] Players, NarrationFact[] Events);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(NarrationDocument))]
[JsonSerializable(typeof(NarrationFacts))]
public partial class NarrationJsonContext : JsonSerializerContext;

public static class NarrationPrompt
{
    public const string Instructions = """
        Write ONE original football storytelling package: contextual broadcast cues AND Fan/Analyst story chapters.
        Everything is synthetic, not actual athlete performance. Use only supplied measured facts/IDs/coordinates.
        This response replaces the existing commentary stage; no tools, agents or additional model calls.
        The entire match is supplied for packaging, NOT permission for a cue/chapter to know future events.
        Treat each output as an independent observer at that output's reveal time.

        CUES: 36-44 varied, vivid, short calls across all five minutes, at least two per minute, one for EVERY goal.
        id(unique ASCII/hyphen), eventId(primary completed event), start,end,playerIds,text,evidenceIds.
        Start at primary event time or up to two seconds afterward, duration two-six seconds, end<=300.
        Order by start, NO overlaps. Each text8-140characters, one plain-text line, no digits or markup.
        evidenceIds: one-six distinct known IDs including eventId, ALL completed by start, no more than twelve seconds old.
        playerIds: one-six distinct IDs from those evidence participants. Only name those people.
        Use action-first, clipped reactions and connected setup/payoff, not full-roster roll calls or repeated pass notifications.
        Short familiar names are fine; names should earn their place. No fake broadcast quotation/attribution.
        Earlier pass/carry geometry can explain how THIS observed move developed. Do not announce a later goal beforehand.
        Goal/scores/scored/opener/lead/level/equalizer language ONLY on primary goal events.
        Be expressive about observed action, never invent crowd/noise/power/corner/shot quality/assists.

        CHAPTERS: eight-twelve chronological turning points, not a chapter every thirty seconds or every pass.
        Include EVERY goal, at least two observed interceptions, and at least one connected buildup.
        Each: id(unique ASCII/hyphen),revealTime,kind,focusEventId,eventIds,playerIds,heading,fan,analyst.
        revealTime EXACTLY focus event completion time, strictly increasing between chapters.
        eventIds: one-six distinct known IDs including focus, ALL completed by revealTime and within its prior twenty seconds.
        playerIds: one-six distinct actual evidence participants, MUST include focus actor.
        kind: goal(focus goal),turnover(focus intercepted pass),buildup(at least two pass/carry references),
        response(focus after a kickoff within twenty seconds). Do not invent a type that the facts cannot prove.
        heading4-48characters, fan8-160, analyst8-220. All plain text, no digits.
        Fan: concise hook explaining why this observed moment matters, not another event list.
        Analyst: DISTINCT short interpretation of the observed sequence/geometry. Use may/suggests for inferred purpose.
        Give buildup, breakthrough, turnover and response different rhythms. Avoid generic slogans and repetitive headings.
        Do not say equalizer unless focus is a goal whose LOCAL supplied score is tied.
        Do not write final/winner/outcome-bearing global titles; the viewer reveals chapters only as observed.

        Both outputs: no xG, pressure, formation, statistics calculated by you, predictions, later outcomes,
        unobserved intentions, causal certainty, unsupported skill/defender/crowd claims or real-world context.
        Never use will/later/eventually/winner/prediction/finally/guaranteed/certainly.
        The app supplies numerical score/consequence/distance badges from local facts, not your arithmetic.
        JSON only. Copy factsHash exactly. Original text only, no copied game or broadcast writing.
        """;
    private const string CueSchema = """
        {"type":"object","additionalProperties":false,"required":["id","eventId","start","end","playerIds","text","evidenceIds"],
        "properties":{"id":{"type":"string"},"eventId":{"type":"string"},"start":{"type":"number"},"end":{"type":"number"},
        "playerIds":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"string"}},"text":{"type":"string","minLength":8,"maxLength":140},
        "evidenceIds":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"string"}}}}
        """;
    private const string ChapterSchema = """
        {"type":"object","additionalProperties":false,"required":["id","revealTime","kind","focusEventId","eventIds","playerIds","heading","fan","analyst"],
        "properties":{"id":{"type":"string"},"revealTime":{"type":"number"},"kind":{"type":"string","enum":["goal","turnover","buildup","response"]},
        "focusEventId":{"type":"string"},"eventIds":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"string"}},
        "playerIds":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"string"}},
        "heading":{"type":"string","minLength":4,"maxLength":48},"fan":{"type":"string","minLength":8,"maxLength":160},
        "analyst":{"type":"string","minLength":8,"maxLength":220}}}
        """;
    public static string Schema => """
        {"type":"object","additionalProperties":false,"required":["factsHash","cues","chapters"],"properties":{
        "factsHash":{"type":"string"},"cues":{"type":"array","minItems":24,"maxItems":64,"items":
        """ + CueSchema + """
        },"chapters":{"type":"array","minItems":8,"maxItems":12,"items":
        """ + ChapterSchema + "}}}";

    public static string Facts(Match match)
    {
        var events = match.Events.Select(e =>
        {
            var start = e.StartTime ?? e.Time;
            var ball = match.Ball.Frames.Where(f => f.Time > start && f.Time < e.Time)
                .Prepend(new(start, match.Ball.At(start))).Append(new(e.Time, match.Ball.At(e.Time)))
                .DistinctBy(f => f.Time).Select(f => new MomentPoint(f.Time, f.Position.X, f.Position.Y)).ToArray();
            var score = Replay.At(match, e.Time).Score;
            return new NarrationFact(e.Id, e.Time, start, e.Kind.ToString().ToLowerInvariant(), e.ActorId, e.ReceiverId, e.Completed,
                ball, e.Kind == EventKind.Carry ? match.Player(e.ActorId).Path.Measure(e.Time).Distance -
                    match.Player(e.ActorId).Path.Measure(start).Distance : null, score["arsenal"], score["manchester-city"]);
        }).ToArray();
        return JsonSerializer.Serialize(new NarrationFacts(match.Duration,
            match.Players.Select(p => new NarrationPlayer(p.Id, p.Name, p.TeamId)).ToArray(), events), NarrationJsonContext.Default.NarrationFacts);
    }
    public static string Hash(Match match) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Facts(match))));
    public static string For(Match match) => Instructions + "\nCopy factsHash: " + Hash(match) + "\nMeasured match facts:\n" + Facts(match);
    public static NarrationDocument Document(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 60000) throw new ArgumentException("Empty or oversized narration package.");
            var options = new JsonSerializerOptions(NarrationJsonContext.Default.Options) { AllowDuplicateProperties = false, MaxDepth = 16 };
            return JsonSerializer.Deserialize(json, new NarrationJsonContext(options).NarrationDocument)
                ?? throw new ArgumentException("Narration package missing.");
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { throw new ScenarioValidationException($"Invalid narration package: {e.Message}", e); }
    }
    public static Match Attach(string json, Match match) => Attach(Document(json), match);
    public static Match Attach(NarrationDocument document, Match match)
    {
        try
        {
            if (document.FactsHash != Hash(match) || document.Cues is null || document.Chapters is null ||
                document.Cues.Any(c => c is null || c.EvidenceIds is null))
                throw new ArgumentException("Narration/captions must share this exact measured-facts hash and explicit evidence bounds.");
            return match.WithNarrative(document.Cues, document.Chapters, CopilotScenarioSource.Model, document.FactsHash);
        }
        catch (ArgumentException e) { throw new ScenarioValidationException($"Invalid narration package: {e.Message}", e); }
    }
}
