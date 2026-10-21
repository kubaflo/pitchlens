using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record PhaseEventEvidence(string Id,double Time,string Kind,bool? Completed);
public sealed record AuthoredPhaseIssue(string PhaseId,double Start,double End,string Kind,string RequiredEvidence,PhaseEventEvidence[] Observed);
public sealed class AuthoredPhaseValidationException(IReadOnlyList<AuthoredPhaseIssue> issues)
    : ScenarioValidationException(Describe(issues))
{
    public IReadOnlyList<AuthoredPhaseIssue> Issues{get;}=issues;
    private static string Describe(IReadOnlyList<AuthoredPhaseIssue> issues)=>string.Join("; ",issues.Select(i=>
        FormattableString.Invariant($"{i.PhaseId} ({i.Start:0.#},{i.End:0.#}]s tagged '{i.Kind}' lacks {i.RequiredEvidence}. Observed: ")+
        string.Join(", ",i.Observed.Select(e=>FormattableString.Invariant($"{e.Id}@{e.Time:0.#}s/{e.Kind}")+
            (e.Completed is bool completed?$"/completed={completed.ToString().ToLowerInvariant()}":"")))));
}
public static class AuthoredPhaseEvidence
{
    public static void Validate(IReadOnlyList<MatchEvent> events,IReadOnlyList<string> kinds)
    {
        var issues=new List<AuthoredPhaseIssue>();
        for(var n=0;n<kinds.Count;n++)
        {
            var observed=events.Where(e=>e.Time>n*30&&e.Time<=(n+1)*30).ToArray();
            if(observed.Length==0)throw new ScenarioValidationException(
                FormattableString.Invariant($"phase-{n+1} ({n*30},{(n+1)*30}]s needs observed actions. Phase-tag correction cannot invent events."));
            var required=kinds[n] switch
            {
                "turnover" when !observed.Any(e=>e.Kind==EventKind.Pass&&e.Completed==false)=>"a completed intercepted pass",
                "finish" when !observed.Any(e=>e.Kind==EventKind.Goal)=>"a completed goal",
                _=>null
            };
            if(required is not null)issues.Add(new("phase-"+(n+1).ToString(CultureInfo.InvariantCulture),n*30,(n+1)*30,kinds[n],required,
                observed.Select(e=>new PhaseEventEvidence(e.Id,e.Time,e.Kind.ToString().ToLowerInvariant(),e.Completed)).ToArray()));
        }
        if(issues.Count>0)throw new AuthoredPhaseValidationException(issues.AsReadOnly());
    }
}

public sealed record PhaseTag([property:JsonRequired] string Id,[property:JsonRequired] double Start,
    [property:JsonRequired] double End,[property:JsonRequired] string Kind);
public sealed record AuthoredPhaseCorrection([property:JsonRequired] PhaseTag[] Phases);
public sealed record PhaseCorrectionContext(double Duration,PhaseTag[] Phases,EventDocument[] Events,AuthoredPhaseIssue[] Issues);
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AuthoredPhaseCorrection))]
[JsonSerializable(typeof(PhaseCorrectionContext))]
public partial class PhaseCorrectionJsonContext:JsonSerializerContext;

public static class AuthoredPhaseRefinement
{
    public const string Schema="""
        {"type":"object","additionalProperties":false,"required":["phases"],"properties":{
        "phases":{"type":"array","minItems":10,"maxItems":10,"items":{"type":"object","additionalProperties":false,
        "required":["id","start","end","kind"],"properties":{
        "id":{"type":"string","enum":["phase-1","phase-2","phase-3","phase-4","phase-5","phase-6","phase-7","phase-8","phase-9","phase-10"]},
        "start":{"type":"number"},"end":{"type":"number"},
        "kind":{"type":"string","enum":["possession","turnover","counterattack","finish","response"]}}}}}}
        """;
    public static string Prompt(AuthoredMatchDocument original,AuthoredPhaseValidationException failure)=>
        """
        Correct ONLY the ten phaseKinds labels for this already-authored synthetic match.
        Return ONLY {"phases":[{"id":"phase-1","start":0,"end":30,"kind":"possession"},...all ten...]}.
        Copy EXACT existing phase IDs/count/order/start/end. Only kind may change. No missing/duplicate/moved phases.
        No events, paths, prose, tools or extra fields.
        All existing events/IDs/actors/receivers/completion outcomes/timestamps, all22 player paths, fixture,
        duration and initial owner are IMMUTABLE. Do NOT invent or change an interception/goal to justify a label.
        This uses the ONE optional correction slot SHARED with path refinement; no second correction is allowed.
        Phase1 evidence is event COMPLETION time >0 and <=30; phase2 >30 and <=60; ... phase10 >270 and <=300.
        An event EXACTLY at30 belongs ONLY to phase1. A flight starting at29 and completing at32 belongs ONLY to phase2.
        turnover requires an actual pass with completed:false completing INSIDE that exact interval.
        finish requires an actual goal completing INSIDE that exact interval.
        A completed:true pass is NOT an interception. A nearby event outside the interval does NOT support its label.
        Use possession for a phase without a supported turnover/finish interpretation; YOU author all final labels.
        Keep already-supported labels unchanged unless their interpretation needs correcting; no unsupported narrative.
        The full unchanged ownership/physics/events/paths/phase validator will run after your response.
        Structured observed failures and immutable event sequence:
        """+JsonSerializer.Serialize(new PhaseCorrectionContext(original.Duration,Tags(original),original.Events,failure.Issues.ToArray()),
            PhaseCorrectionJsonContext.Default.PhaseCorrectionContext);
    public static PhaseTag[] Tags(AuthoredMatchDocument original)=>original.PhaseKinds.Select((kind,n)=>
        new PhaseTag("phase-"+(n+1).ToString(CultureInfo.InvariantCulture),n*30,(n+1)*30,kind)).ToArray();
    public static AuthoredMatchDocument Apply(AuthoredMatchDocument original,string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>4000)throw new ScenarioValidationException("Empty or oversized AI phase-tag correction.");
        try
        {
            var options=new JsonSerializerOptions(PhaseCorrectionJsonContext.Default.Options){AllowDuplicateProperties=false,MaxDepth=8};
            var correction=JsonSerializer.Deserialize(json,new PhaseCorrectionJsonContext(options).AuthoredPhaseCorrection)
                ??throw new ScenarioValidationException("Null AI phase-tag correction.");
            if(correction.Phases is not{Length:10}||correction.Phases.Any(p=>p is null))
                throw new ScenarioValidationException("AI phase correction requires all ten immutable phases.");
            for(var n=0;n<10;n++)
            {
                var phase=correction.Phases[n];
                if(phase.Id!="phase-"+(n+1).ToString(CultureInfo.InvariantCulture)||phase.Start!=n*30||phase.End!=(n+1)*30)
                    throw new ScenarioValidationException("AI phase correction changed immutable phase IDs, order or interval bounds.");
                if(phase.Kind is not("possession" or "turnover" or "counterattack" or "finish" or "response"))
                    throw new ScenarioValidationException("AI phase correction contains an unsupported label.");
            }
            return original with{PhaseKinds=correction.Phases.Select(p=>p.Kind).ToArray()};
        }
        catch(JsonException e){throw new ScenarioValidationException("Invalid AI phase-tag correction JSON.",e);}
    }
}
