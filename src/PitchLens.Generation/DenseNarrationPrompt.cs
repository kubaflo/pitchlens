using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CaptionSlot[]))]
[JsonSerializable(typeof(NarrationSlot[]))]
public partial class CaptionSlotJsonContext:JsonSerializerContext;
public sealed record NarrationSlot(string EventId,double Start,double End,string PrimaryKind,bool ScoringLanguageAllowed);

public static class DenseNarrationPrompt
{
    public static NarrationSlot[] Slots(Match match)=>CaptionSchedule.For(match).Select(slot=>{
        var primary=match.Events.Single(e=>e.Id==slot.EventId);
        return new NarrationSlot(slot.EventId,slot.Start,slot.End,primary.Kind.ToString().ToLowerInvariant(),primary.Kind==EventKind.Goal);
    }).ToArray();
    public static string For(Match match)
    {
        CaptionSchedule.RequireOpportunities(match);
        var old=NarrationPrompt.For(match);
        var chapters=old[old.IndexOf("CHAPTERS:",StringComparison.Ordinal)..];
        return """
            Author ONE original dense ON-SCREEN football broadcast subtitle + Fan/Analyst story package.
            Synthetic performance only, exact supplied real roster identities. No tools, additional requests, audio or providers.
            Return captionContract:2, factsHash, cues, chapters. Copy factsHash exactly.
            The viewer LOCAL POLICY has already chosen exact observed caption slots. YOU author ALL wording.
            Fill EVERY slot once in exact order. Copy eventId/start/end EXACTLY; never shorten, stretch, merge or omit a slot.
            id uniqueASCII-hyphen; playerIds1–3 actual cited participants; evidenceIds1–6 including eventId.
            All refs completed BY slot start, within preceding12seconds. No future goal/payoff in earlier captions.
            EACH slot supplies read-only primaryKind and scoringLanguageAllowed. In each required cue object copy eventId/start/end exactly.
            Do NOT return those two helper fields; still provide all schema-required id/playerIds/text/evidenceIds fields.
            Scoring language is permitted ONLY when scoringLanguageAllowed:true (primaryKind:goal).
            A pass/carry/kickoff slot MUST NOT say goal, goals, scores, scored, scoring, opener, equalizer/equaliser, lead or level.
            An earlier goal in evidenceIds or a goal later in the full match NEVER grants scoring language to a non-goal primary slot.
            React to that slot's observed pass/carry/restart instead; do not celebrate, recap or anticipate a goal there.
            Narrow spatial phrases toward/towards goal, closer to goal, goal area/mouth/line and in front of goal
            require the supplied observed geometry; they are not permission to claim a finish. Prefer simple action wording if uncertain.
            All supplied source facts are stored for packaging, not permission to reveal future outcomes.
            TEXT: punchy2–9words, at most65characters; at least75% MUST have3–8words, ideally20–60characters; no digits/markup/newlines.
            Vary length and openings. Present/past REACTIONS, not commands/coaching: no 'Push on', 'Take it', 'Find', 'Bring', 'Work', 'Drive'.
            Use brief familiar names only when useful. Avoid full-name rollcalls and repeating '<name> passes/finds <name>'.
            Alternate clipped reactions, observed route/connection context, possession switches, finish reactions, concise setup/payoff.
            Routine passes can be understated, interceptions should snap, goals deserve an unmistakable factual call.
            Even frequent captions should sound like football, not a spreadsheet/event logger.
            Examples of STYLE only, not reusable facts: 'Across the pitch. A new angle.' / 'Cut out in midfield!' / 'An inward link opens the route.'
            For repeated possession/restart context, react to THAT observed event; no fake new action, padding, looped slogans or imaginary crowd.
            Don't state a goal/lead/equalizer before primary goal evidence. Spatial goal wording needs actual supplied geometry.
            No xG/pressure/formation/shotquality/assists/power/crowd, real-world attribution, numerical stats, invented motive or causal certainty.
            No will/later/eventually/winner/prediction/finally/guaranteed/certainly. App computes score/metrics, not you.
            Keep chapters useful and distinctive, but DO NOT expand caption text into chapter-length prose.
            """+"\n"+chapters+"\nEXACT LOCAL OBSERVED SUBTITLE SLOTS:\n"+
            JsonSerializer.Serialize(Slots(match),CaptionSlotJsonContext.Default.NarrationSlotArray);
    }
    public static string Schema
    {
        get
        {
            var schema=JsonNode.Parse(NarrationPrompt.Schema)!.AsObject();
            schema["required"]!.AsArray().Add((JsonNode?)JsonValue.Create("captionContract"));
            schema["properties"]!["captionContract"]=new JsonObject{["type"]="integer",["const"]=2};
            var cues=schema["properties"]!["cues"]!;
            cues["minItems"]=1;cues["maxItems"]=90;
            cues["items"]!["properties"]!["text"]!["maxLength"]=65;
            cues["items"]!["properties"]!["playerIds"]!["maxItems"]=3;
            return schema.ToJsonString();
        }
    }
}
