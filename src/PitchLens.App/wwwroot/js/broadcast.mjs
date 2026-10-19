import { goalState } from "./replay.mjs";

export function broadcastFrame(scenario, clockTime, replaysEnabled = true) {
    const state = goalState(scenario, clockTime);
    const latest = [...(scenario.goals || [])].reverse().find(g => g.time <= clockTime);
    const deadBall = !!latest && Number.isFinite(latest.restartTime) && clockTime < latest.restartTime;
    const elapsed = latest ? clockTime - latest.time : 0;
    let time = clockTime, phase = "live", replay = false;
    if (deadBall && replaysEnabled) {
        const resetLength = latest.restartTime - latest.time;
        const impactLength = Math.min(.8, resetLength / 4);
        if (elapsed < impactLength) { time = latest.time; phase = "impact"; }
        else {
            const start = Math.max(0, Math.min(latest.shotStart ?? latest.time - 3, latest.time - 4));
            const progress = Math.min(1, (elapsed - impactLength) / (resetLength - impactLength));
            time = start + (latest.time - start) * progress;
            phase = "replay"; replay = true;
        }
    }
    return {
        time, phase, replay, deadBall, score: state.score, goal: state.celebration,
        elapsed, reveal: Math.min(1, elapsed / .45),
        fade: state.celebration ? Math.min(1, (8 - elapsed) / 1.2) : 0,
        impact: state.celebration ? Math.max(0, 1 - elapsed / 1.2) : 0
    };
}

export function goalParticles(goal, elapsed, reducedMotion = false) {
    if (!goal || reducedMotion || elapsed < 0 || elapsed > 2.4) return [];
    const right = goal.teamId === "arsenal";
    return Array.from({ length: 18 }, (_, index) => {
        const angle = (index / 18) * Math.PI * 2;
        const reach = (4 + index % 4) * elapsed;
        return {
            x: (right ? 101 : 4) + Math.cos(angle) * reach,
            y: 34 + Math.sin(angle) * reach + elapsed * elapsed * .7,
            alpha: Math.max(0, 1 - elapsed / 2.4), radius: .15 + index % 3 * .12
        };
    }).filter(p => p.x >= .5 && p.x <= 104.5 && p.y >= .5 && p.y <= 67.5);
}
