namespace PitchLens.Generation;

public static class DirectorPrompt
{
    public const string Instructions = """
        Direct an original45–60 WALL SECOND in-app2D football highlight film from this synthetic observed match.
        Make real editorial choices: a few meaningful setup, explanation, payoff and response scenes, not a generic goal playlist.
        Return ONE JSON edit plan; no tools, agents, additional requests, executable code or HTML.
        Copy factsHash/revision/cutoff exactly. ALL events and player knots supplied are observed through cutoff.
        Choose FOUR to SIX purposeful scenes in chronological source-start order.
        Each: id(unique ASCII/hyphen),sourceStart,sourceEnd,rate,holdSeconds,camera,focusEventId,evidenceIds,text.
        sourceStart>=0,sourceEnd<=cutoff. Clip length TWO to TWELVE source seconds.
        sourceEnd EXACTLY equals focusEventId's event completion time.
        rate must be0.5 or1; holdSeconds TWO to SIX. Film wall duration is sum((end-start)/rate+holdSeconds), MUST45–60seconds.
        Include at least one actual goal and at least one non-goal setup/response/turnover, prefer coherent buildup→payoff→opposing response/equalizer when observed.
        Do not loop the same goal or fill time with unrelated tiny cuts. Choose evidence and pacing deliberately.
        camera tactical or finish; finish permitted ONLY when focus is a goal.
        evidenceIds one-six distinct known IDs including focus, all completed WITHIN sourceStart..sourceEnd.
        The explanation text appears ONLY in the final hold AFTER all cited actions complete. Keep8–160characters, no digits/markup.
        Explain why this observed sequence matters in a concise original sentence; may/suggests for interpretation, not invented intent.
        Earlier moving footage uses its own source-as-of score/captions, never your later scene text or final score.
        No future-outcome global title, xG/pressure/formation/shotquality/crowd/assists/real-athlete-performance/causal certainty.
        Never use will/later/eventually/winner/prediction/finally/guaranteed/certainly.
        Goal/lead/equalizer wording only on goal focus; equalizer requires that event's supplied local score tied.
        Numerical score/distance badges are rendered from local facts, not your arithmetic.
        Original editorial writing only, no copied broadcast/game text. No MP4/audio: this is an interactive in-app film.
        """;
    public const string Schema = """
        {"type":"object","additionalProperties":false,"required":["factsHash","revision","cutoff","scenes"],"properties":{
        "factsHash":{"type":"string"},"revision":{"type":"integer"},"cutoff":{"type":"number"},
        "scenes":{"type":"array","minItems":4,"maxItems":6,"items":{"type":"object","additionalProperties":false,
        "required":["id","sourceStart","sourceEnd","rate","holdSeconds","camera","focusEventId","evidenceIds","text"],
        "properties":{"id":{"type":"string"},"sourceStart":{"type":"number"},"sourceEnd":{"type":"number"},
        "rate":{"type":"number","enum":[0.5,1]},"holdSeconds":{"type":"number","minimum":2,"maximum":6},
        "camera":{"type":"string","enum":["tactical","finish"]},"focusEventId":{"type":"string"},
        "evidenceIds":{"type":"array","minItems":1,"maxItems":6,"items":{"type":"string"}},
        "text":{"type":"string","minLength":8,"maxLength":160}}}}}}
        """;
    public static string For(DirectorEvidence evidence) => Instructions + "\nfactsHash=" + evidence.Hash + "\nObserved facts:\n" + evidence.Json;
}
