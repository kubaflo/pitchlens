using System.Text.Json;

namespace PitchLens.Generation;

public static class AuthoredMatchPrompt
{
    public static string Text => """
        Author a complete original synthetic300s11v11 Arsenal-ManchesterCity match. YOU generate ALL events AND ALL22 PLAYER PATHS.
        No procedural role solver will supply movement. The engine ONLY linearly interpolates your explicit [time,x,y] knots.
        Output compact JSON, no markdown. Roster identities below are factual; all performance/actions invented, not actual startingXIs/results.
        duration300; neutral title Pressure and Space / The Turning Tide / A Game of Transitions.
        players: ALL22 IDs, each frames array31..81 explicit triples[time,x,y] from0 to300, strictly increasing, maxgap12s.
        Prefer35-50 knots per player, variable/staggered times, integer/one-decimal coordinates; add exact action/restart knots when useful.
        Every player's changing positions must be authored in the response. No sparse seeds, copied loops, synchronized oscillations or stationary background fillers.
        Pitchx0..105,y0..68. Arsenal attacksRIGHT, CityLEFT. GK Raya atownLEFTx4..15,y22..46;Donnarumma ownRIGHTx90..101,y22..46.
        Start with illustrative4-3-3 lines: defenders behind midfield behind attackers (opposite forCity); use given role IDs.
        Continuous varied phases, individual supporting runs, tracking/marking, recovery, calm recycling, overlaps, acceleration/deceleration.
        Not everyone sprints. Allow slower/settled intervals but sustain meaningful action through300s forBOTHclubs.
        CORRECTION from measured prior output: all outfield peak speeds were <=2.69m/s (almost all <=1.35), so it was walking, not credible football.
        Author real accelerations/support/recovery bursts with extra timed knots: >=4outfielders EACHclub have a >=3m/s segment lasting>=2s;
        >=2outfielders EACHclub have a >=4m/s segment lasting>=2s. These must be explicit actual path distances/times, not claimed labels.
        Use extra3-5s knots around12-24m runs, ease back to slower movement, and preserve width/cover/onside/restart constraints.
        Include>=8controlled carry events,>=3EACHclub, not a walking pass-only circulation exercise.
        Speed<=8m/s. Discrete velocitychange / mean adjacent segmentduration <=2.5m/s²; no teleports/sharp highspeed reversals.
        Keep EACHteamoutfield width>=30m, depth>=22m, own-endkeeper, at alltimes. Retain defenders/cover, stretch wings sensibly.
        Avoid collisions(<.4m); max1pair at a sampledtime; no pileup of>5players within5m. Individual paths, not a translated formation.
        At least6/10outfielders EACHclub make>=2purposeful directionchanges (segments>=4m, cosine<.85) overwholegame.
        At least6outfielders overall,>=2eachclub, travel>=8m per20swindow. Don't forceall22continuoussprints.
        events45..90, ordered(time,order). Kinds pass/carry/shot/goal/kickoff.
        Every event has id,time,order,kind,actorId,receiverId,completed,startTime; nulls explicit.
        Normal events order0; a goal follows same-actor same-time shot with order1. No other equal-time events.
        Pass has receiverId, completedtrue iff sameclub, false iff opposing interception. Others receiverId/completednull.
        Pass/carry/shot startTime/time positive duration1..10s, nonoverlap(start>=last action end). Goal/kickoff startTimenull.
        initialOwnerId selects firstowner. Actor MUST ownball. Carry retainsowner; pass transfers to receiver ONLYatresolvedtime.
        Ball follows your authored ownerpath. Carries must travel>=3m. Completed pass receiver must be ONSIDE at release:
        inownhalf OR not beyond BOTH ball andsecond-lastopponent towardgoal (1.5m tolerance). Plan defendingline and receiver runs accordingly.
        Ballflight forpass/shot is derived between your actorpath atstart and receiverpath atend / attackinggoal.
        Choose start/time and paths so flight<=40m/s; typical1-3s. Flightstart strictly AFTER lastpossessionchange.
        ONLY scoring shots inthisboundedprototype: shot atattackinggoal, explicit pairedGOAL event. Atleastonegoal TOTAL; eitherclub may score none.
        Opposing outfield KICKOFF>=3saftergoal, authored kickerpath EXACTLY(52.5,34) atkickofftime. Plan reachable run tocentre.
        Aftergoal no otheraction beforekickoff. Don't reuse ownball actor after opponent interception. No own-goal/direction contradictions.
        >=4interceptedpasses. >=2pass/carry/shot events ineach sliding20swindow. Last meaningfulaction>=295s.
        Completedpasses BOTHclubs inEACH60sminute; >=7distinct outfieldactors EACHclub overmatch, including newlyaddedplayers.
        Both clubs should have credible possessions/attacks/responses, but NOT necessarily goals. Choose a varied result, never force a draw.
        phaseKinds exactly10entries for0..30,30..60,...270..300: possession/turnover/counterattack/finish/response.
        finish requiresactualgoal inphase;turnover requiresactualinterception. Usepossessionifuncertain, no unsupported tacticalmetrics.
        Think through coherent possession/actions first, then coordinate ALL paths and realistic teamshape with those events.
        Do not emit commentary inthisfirststage; aftervalidation anotherboundedrequest authors evidence-anchored CC.
        ALL final numerical paths/events must be present inJSON. The application will reject, NOT repair, unrealistic or invalid output.
        VERIFIEDillustrativeroster IDs/names/numbers/roles (not an actualcoachlineup):
        """ + JsonSerializer.Serialize(RegulationRoster.Players.ToArray(), AuthoredJsonContext.Default.MatchRosterPlayerArray);
    public const string Schema = """
        {"type":"object","additionalProperties":false,"required":["duration","title","initialOwnerId","players","events","phaseKinds"],
        "properties":{"duration":{"type":"number","const":300},"title":{"type":"string","enum":["Pressure and Space","The Turning Tide","A Game of Transitions"]},
        "initialOwnerId":{"type":"string"},"players":{"type":"array","minItems":22,"maxItems":22,"items":{"type":"object","additionalProperties":false,
        "required":["id","frames"],"properties":{"id":{"type":"string"},"frames":{"type":"array","minItems":31,"maxItems":81,"items":{"type":"array","minItems":3,"maxItems":3,"items":{"type":"number"}}}}}},
        "events":{"type":"array","minItems":45,"maxItems":90,"items":{"type":"object","additionalProperties":false,"required":["id","time","order","kind","actorId","receiverId","completed","startTime"],
        "properties":{"id":{"type":"string"},"time":{"type":"number"},"order":{"type":"integer"},"kind":{"type":"string","enum":["pass","carry","shot","goal","kickoff"]},
        "actorId":{"type":"string"},"receiverId":{"type":["string","null"]},"completed":{"type":["boolean","null"]},"startTime":{"type":["number","null"]}}}},
        "phaseKinds":{"type":"array","minItems":10,"maxItems":10,"items":{"type":"string","enum":["possession","turnover","counterattack","finish","response"]}}}}
        """;
}
