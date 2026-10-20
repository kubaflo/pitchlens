export function pinnedEvidence(revision, json, clock) {
    const fact = JSON.parse(json);
    if (revision !== clock.revision || clock.playing || Math.abs(clock.position - fact.time) > .01)
        throw new Error("Evidence must be pinned to this paused match and exact recorded event time.");
    if (typeof fact.id !== "string" || !Number.isFinite(fact.start) || !Number.isFinite(fact.time) ||
        fact.start < 0 || fact.start > fact.time || !Array.isArray(fact.ball) || fact.ball.length < 1 || fact.ball.length > 512 ||
        fact.ball.some((p, i) => !Number.isFinite(p.time) || p.time < fact.start || p.time > fact.time ||
            (i > 0 && p.time <= fact.ball[i - 1].time) ||
            !Number.isFinite(p.x) || !Number.isFinite(p.y) || p.x < 0 || p.x > 105 || p.y < 0 || p.y > 68))
        throw new Error("Invalid recorded evidence geometry.");
    return { revision, fact };
}
export function evidenceVisible(pinned, clock, time) {
    return !!pinned && pinned.revision === clock.revision && !clock.playing && Math.abs(time - pinned.fact.time) < .01;
}
export function drawEvidence(context, fact) {
    context.strokeStyle = "#ffe46b"; context.lineWidth = .5;
    context.beginPath();
    fact.ball.forEach((p, i) => i ? context.lineTo(p.x, p.y) : context.moveTo(p.x, p.y));
    context.stroke();
    for (const p of [fact.ball[0], fact.ball.at(-1)]) {
        context.beginPath(); context.arc(p.x, p.y, 1.15, 0, Math.PI * 2);
        context.stroke();
    }
}
