using System.Text.Json;

namespace PitchLens.Generation;

public static class FiveMinutePrompt
{
    public static string Text => """
        Invent an original synthetic FIVE-MINUTE (300-second) 6v6 Arsenal v Manchester City match.
        Real 2026/27 names only; invented actions, no real-performance claims. Output the compact plan schema, not hundreds of frames.
        EXACTLY ten 30-second phases cover 0-300. Each phase has 4-8 causal actions. Keep play active through the final seconds.
        Choose substantive actors, passes/interceptions, carries, finishes, responses, timing AND spatial targets.
        Title: Pressure and Space / The Turning Tide / A Game of Transitions (no winner spoilers).
        Phase kinds: possession / turnover / counterattack / finish / response, appropriate to actual actions.
        Arsenal attacks RIGHT (x105), City LEFT (x0). Pitch x0..105,y0..68.
        Each phase supplies five Arsenal and five City OUTFIELD shape anchors, coordinates x10..95,y7..61.
        Array order Arsenal: Saliba,Odegaard,Rice,Saka,Gyokeres. City: Dias,Foden,Cherki,Haaland,Doku.
        The engine smoothly interpolates shape changes and bounded role/support/defensive movement; choose genuinely changing,
        wide/deep shapes (roughly 15-25m anchor changes between phases), NOT stationary players or all players around the ball.
        Each team's shape must retain >=14m lateral width and >=25m separation at every5s.
        At least six outfielders, >=2 each club, must travel >=8m in EVERY20-second window, not just aggregate match distance.
        Keepers are handled deterministically at own ends: Raya x6, Donnarumma x99; don't include them in outfield shape arrays.
        Their starting/goal-area consistency is enforced. You may use Raya as initial owner.
        Action fields: id,kind,startTime,time,actorId,receiverId,completed,target{x,y}.
        Kinds only pass,carry,shot,kickoff. Goal is automatically paired with a scoring shot; no tackle/non-scoring shot types.
        All actions strictly chronological, non-overlapping, within their phase; startTime>=previous action end.
        Actor MUST own the ball. initialOwnerId selects first owner; successful or intercepted pass transfers owner to receiver.
        completed=true only for same-team receiver; false only for an opposing-team interception. receiverId and completed null for other kinds.
        Pass/shot startTime must be STRICTLY AFTER previous possession change (leave >=.5s).
        PASS target is the receiver's exact arrival position. Engine choreographs that receiver toward it up to10s beforehand.
        Keep targets reachable within8m/s; choose receiver target near its tactical shape, not arbitrary distant positions.
        Pass flight1-3s, <=40m/s. CARRY target is actor's exact end position; use3-7s moving carries covering8-24m.
        Carries maintain owner. SHOT target must be attacking goal(105,34 for Arsenal;0,34 forCity), flight1-3s.
        Every goal requires OPPOSING outfield KICKOFF >=3s later before further actions. Kickoff target(52.5,34), actor approaches centre.
        A kickoff still has positive startTime/time range after previous action. Don't use keeper for centre restart.
        Both clubs must complete passes in EACH60s minute, get multiple possessions and have >=1 scoring finish each.
        At least4 intercepted passes across the match. Include at least2 meaningful actions in every20s sliding window.
        Final action must end295..300s; no idle padding, repeated loop, slow-motion stretch or random jitter.
        Plan internally a coherent possession chain over ten distinct phases. The deterministic compiler supplies detail,
        but you must invent all action choices, role-shape anchors, receiver/carry targets and evolving match story.
        The match duration300s is separate from the bounded SDK REQUEST timeout. Do not emit commentary/markdown.
        Verified roster IDs/names/numbers:
        """ + JsonSerializer.Serialize(PremierLeagueRoster.Players.ToArray(), ScenarioJsonContext.Default.RosterPlayerArray);

    public const string Schema = """
    {"type":"object","additionalProperties":false,"required":["duration","title","initialOwnerId","phases"],
     "properties":{"duration":{"type":"number","const":300},"title":{"type":"string","enum":["Pressure and Space","The Turning Tide","A Game of Transitions"]},
       "initialOwnerId":{"type":"string"},"phases":{"type":"array","minItems":10,"maxItems":10,"items":{"$ref":"#/$defs/phase"}}},
     "$defs":{
       "point":{"type":"object","additionalProperties":false,"required":["x","y"],"properties":{"x":{"type":"number"},"y":{"type":"number"}}},
       "action":{"type":"object","additionalProperties":false,"required":["id","kind","startTime","time","actorId","receiverId","completed","target"],
         "properties":{"id":{"type":"string"},"kind":{"type":"string","enum":["pass","carry","shot","kickoff"]},"startTime":{"type":"number"},
           "time":{"type":"number"},"actorId":{"type":"string"},"receiverId":{"type":["string","null"]},"completed":{"type":["boolean","null"]},"target":{"$ref":"#/$defs/point"}}},
       "phase":{"type":"object","additionalProperties":false,"required":["id","kind","startTime","endTime","arsenal","city","actions"],
         "properties":{"id":{"type":"string"},"kind":{"type":"string","enum":["possession","turnover","counterattack","finish","response"]},
           "startTime":{"type":"number"},"endTime":{"type":"number"},"arsenal":{"type":"array","minItems":5,"maxItems":5,"items":{"$ref":"#/$defs/point"}},
           "city":{"type":"array","minItems":5,"maxItems":5,"items":{"$ref":"#/$defs/point"}},
           "actions":{"type":"array","minItems":4,"maxItems":8,"items":{"$ref":"#/$defs/action"}}}}}}
    """;
}
