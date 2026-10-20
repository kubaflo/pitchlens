import { at, goalState } from "./replay.mjs";
import { ARENA } from "./viewport.mjs";

const smooth = value => { const t = Math.max(0, Math.min(1, value)); return t * t * (3 - 2 * t); };

export function goalCameraView(scenario, goal) {
    const start = goal.buildUpStart ?? Math.max(0, goal.time - 10);
    const participants = scenario.players.filter(p => (goal.playerIds ?? [goal.scorerId]).includes(p.id));
    const goalX = goal.goalX ?? (goal.teamId === (scenario.teams?.[0]?.id ?? "arsenal") ? 105 : 0);
    const points = [{ x: goalX, y: 30.34 }, { x: goalX, y: 37.66 }];
    for (const player of participants) {
        points.push(at(player.frames, start), at(player.frames, goal.time));
        points.push(...player.frames.filter(f => f.time >= start && f.time <= goal.time));
    }
    points.push(at(scenario.ball, start), at(scenario.ball, goal.time));
    points.push(...scenario.ball.filter(f => f.time >= start && f.time <= goal.time));
    const minX = Math.max(ARENA.x, Math.min(...points.map(p => p.x)) - 8);
    const maxX = Math.min(ARENA.x + ARENA.width, Math.max(...points.map(p => p.x)) + 8);
    const minY = Math.max(ARENA.y, Math.min(...points.map(p => p.y)) - 9);
    const maxY = Math.min(ARENA.y + ARENA.height, Math.max(...points.map(p => p.y)) + 9);
    const width = Math.max(36, maxX - minX), height = Math.max(30, maxY - minY);
    return { x: Math.max(ARENA.x, Math.min((minX + maxX - width) / 2, ARENA.x + ARENA.width - width)),
        y: Math.max(ARENA.y, Math.min((minY + maxY - height) / 2, ARENA.y + ARENA.height - height)), width, height };
}

export function prepareBroadcast(scenario) {
    for (const goal of scenario.goals ?? []) goal.cameraView = goalCameraView(scenario, goal);
}

export function broadcastFrame(scenario, clockTime, enabled = true) {
    const state = goalState(scenario, clockTime);
    const latest = [...(scenario.goals || [])].reverse().find(g => g.time <= clockTime);
    const context = (scenario.goals ?? []).find(g => (g.buildUpStart ?? Math.max(0, g.time - 10)) <= clockTime &&
        clockTime < (g.restartTime ?? g.time + 8));
    const deadBall = !!latest && Number.isFinite(latest.restartTime) && clockTime < latest.restartTime;
    const elapsed = latest ? clockTime - latest.time : 0;
    let time = clockTime, phase = "live", replay = false, contact = null;
    if (enabled && context) {
        phase = clockTime < (context.shotStart ?? context.time) ? "buildup" : clockTime < context.time ? "shot" : "reaction";
        if (deadBall) {
            const reaction = Math.min(2, (latest.restartTime - latest.time) / 3);
            if (elapsed >= reaction) {
                const start = latest.buildUpStart ?? Math.max(0, latest.time - 10);
                const replayLength = latest.restartTime - latest.time - reaction;
                const finishHold = Math.min(1, replayLength / 4);
                const footageLength = replayLength - finishHold;
                const progress = Math.min(1, (elapsed - reaction) / footageLength);
                time = start + (latest.time - start) * progress; phase = "replay"; replay = true;
                if (progress === 1) contact = { goal: latest, age: elapsed - reaction - footageLength };
            }
            else contact = { goal: latest, age: elapsed };
        }
    }
    let view = ARENA;
    if (enabled && context?.cameraView) {
        const weight = smooth((clockTime - (context.buildUpStart ?? context.time - 10)) / 1.2);
        const focus = context.cameraView;
        view = Object.fromEntries(["x", "y", "width", "height"].map(key => [key, ARENA[key] + (focus[key] - ARENA[key]) * weight]));
    }
    return { time, phase, replay, deadBall, view, context, contact, score: state.score, goal: state.celebration, elapsed,
        fade: state.celebration ? Math.min(1, (8 - elapsed) / 1.2) : 0 };
}

export function netPoints(right, elapsed = null, goalY = 34, reducedMotion = false) {
    return Array.from({ length: 5 }, (_, row) => Array.from({ length: 5 }, (_, column) => {
        const depth = column * .75, y = 30.34 + row * 7.32 / 4;
        const wave = elapsed === null || reducedMotion || elapsed < 0 || elapsed > 2 ? 0 :
            Math.sin(elapsed * 14 - depth * 1.1 - Math.abs(y - goalY) * .7) * Math.exp(-elapsed * 1.8) * .65 * depth / 3;
        return { x: (right ? 105 : 0) + (right ? 1 : -1) * (depth + wave), y };
    }));
}
