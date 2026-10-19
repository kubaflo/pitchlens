import test from "node:test";
import assert from "node:assert/strict";
import { broadcastFrame, goalParticles } from "../../src/PitchLens.App/wwwroot/js/broadcast.mjs";
import { ProjectionClock } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

const scene = { duration: 300, goals: [
    { id: "g1", time: 49, shotStart: 47, restartTime: 59, scorerId: "striker", teamId: "arsenal" },
    { id: "g2", time: 176, shotStart: 174, restartTime: 186, scorerId: "away-striker", teamId: "manchester-city" }
] };

test("goal broadcast uses actual past goal footage during dead ball and cuts back at kickoff without moving the clock", () => {
    assert.equal(broadcastFrame(scene, 48.999).goal, null);
    const impact = broadcastFrame(scene, 49);
    assert.equal(impact.time, 49); assert.equal(impact.phase, "impact"); assert.equal(impact.score.arsenal, 1);
    const replay = broadcastFrame(scene, 52);
    assert.equal(replay.phase, "replay"); assert.equal(replay.replay, true); assert.equal(replay.deadBall, true);
    assert.ok(replay.time >= 45 && replay.time < 49);
    assert.equal(replay.score.arsenal, 1);
    assert.deepEqual(broadcastFrame(scene, 52), replay, "pause has no wall-clock animation or timer");
    assert.equal(broadcastFrame(scene, 58.999).replay, true);
    const restart = broadcastFrame(scene, 59);
    assert.equal(restart.time, 59); assert.equal(restart.replay, false); assert.equal(restart.deadBall, false);
    assert.equal(broadcastFrame(scene, 0).score.arsenal, 0);
    assert.equal(broadcastFrame(scene, 48).replay, false);
    assert.deepEqual(broadcastFrame(scene, 52), replay, "seeking back into a goal replays deterministically without scoring again");
});

test("1x/3x/5x goal presentation never changes authoritative elapsed time or pause/seek state", () => {
    for (const rate of [1, 3, 5]) {
        const clock = new ProjectionClock(); clock.reset(1, 300, 0);
        clock.apply({ revision: 1, sequence: 1, time: 48, playing: true, rate }, 0);
        const t = clock.time(2000);
        broadcastFrame(scene, t);
        assert.equal(clock.time(2000), 48 + rate * 2);
        assert.equal(clock.playing, true);
        clock.apply({ revision: 1, sequence: 2, time: 52, playing: false, rate }, 2000);
        const frozen = broadcastFrame(scene, clock.time(2000));
        assert.deepEqual(broadcastFrame(scene, clock.time(999999)), frozen);
        assert.equal(clock.position, 52);
    }
});

test("reduced-motion/full-timeline option and unknown restart metadata never invent a replay", () => {
    const plain = broadcastFrame(scene, 52, false);
    assert.equal(plain.time, 52); assert.equal(plain.replay, false); assert.equal(plain.deadBall, true);
    assert.equal(broadcastFrame({ goals: [{ id: "g", time: 49, teamId: "arsenal" }] }, 52).time, 52);
    assert.equal(broadcastFrame({ goals: [] }, 52).goal, null);
    assert.equal(broadcastFrame(scene, 300).time, 300);
    assert.equal(broadcastFrame(scene, 175).score["manchester-city"], 0);
    assert.equal(broadcastFrame(scene, 176).score["manchester-city"], 1);
});

test("decorative goal burst is deterministic, bounded to the pitch, ends in replay time and respects reduced motion", () => {
    const points = goalParticles(scene.goals[0], .7);
    assert.ok(points.length > 0);
    assert.deepEqual(goalParticles(scene.goals[0], .7), points);
    assert.ok(points.every(p => p.x >= .5 && p.x <= 104.5 && p.y >= .5 && p.y <= 67.5 && p.alpha <= 1));
    assert.deepEqual(goalParticles(scene.goals[0], .7, true), []);
    assert.deepEqual(goalParticles(scene.goals[0], 3), []);
    assert.deepEqual(goalParticles(null, .7), []);
});
