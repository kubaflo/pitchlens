import test from "node:test";
import assert from "node:assert/strict";
import { broadcastFrame, netPoints, prepareBroadcast } from "../../src/PitchLens.App/wwwroot/js/broadcast.mjs";
import { ProjectionClock, advancePlayback, hit } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";
import { ARENA, viewportMap, fieldToViewport, viewportToField, backgroundTransform } from "../../src/PitchLens.App/wwwroot/js/viewport.mjs";

const scene = {
    duration: 300, players: [
        { id: "passer", frames: [{ time: 0, x: 50, y: 20 }, { time: 300, x: 55, y: 22 }] },
        { id: "striker", frames: [{ time: 0, x: 75, y: 34 }, { time: 49, x: 88, y: 34 }, { time: 300, x: 70, y: 34 }] }
    ], ball: [{ time: 0, x: 50, y: 20 }, { time: 48, x: 86, y: 34 }, { time: 49, x: 105, y: 34 }, { time: 59, x: 52.5, y: 34 }, { time: 300, x: 50, y: 20 }],
    goals: [{ id: "g1", time: 49, buildUpStart: 39, shotStart: 48, restartTime: 59, scorerId: "striker",
        playerIds: ["passer", "striker"], teamId: "arsenal", goalX: 105, goalY: 34 }],
    focusWindows: [{ start: 39, end: 59 }]
};
prepareBroadcast(scene);

test("context camera includes actual buildup, shot and visible goal depth, returning tactical at kickoff", () => {
    assert.deepEqual(broadcastFrame(scene, 38).view, ARENA);
    assert.equal(broadcastFrame(scene, 40).phase, "buildup");
    assert.equal(broadcastFrame(scene, 48).phase, "shot");
    const focus = broadcastFrame(scene, 48).view;
    assert.ok(focus.width < ARENA.width);
    for (const point of [{ x: 50, y: 20 }, { x: 88, y: 34 }, { x: 108, y: 37.66 }])
        assert.ok(point.x >= focus.x && point.x <= focus.x + focus.width && point.y >= focus.y && point.y <= focus.y + focus.height);
    assert.equal(broadcastFrame(scene, 49).phase, "reaction");
    assert.equal(broadcastFrame(scene, 50).time, 50, "reaction uses actual authored post-goal player positions, not invented celebration paths");
    const replay = broadcastFrame(scene, 53);
    assert.equal(replay.phase, "replay");
    assert.ok(replay.time >= 39 && replay.time < 49);
    assert.equal(replay.score.arsenal, 1);
    assert.deepEqual(broadcastFrame(scene, 53), replay, "pause is deterministic");
    const replayFinish = broadcastFrame(scene, 58.5);
    assert.equal(replayFinish.time, 49);
    assert.equal(replayFinish.contact.goal.id, "g1");
    assert.equal(replayFinish.score.arsenal, 1, "replayed net contact never counts another goal");
    const restart = broadcastFrame(scene, 59);
    assert.equal(restart.time, 59); assert.equal(restart.replay, false);
    assert.deepEqual(restart.view, ARENA);
    assert.equal(broadcastFrame(scene, 0).score.arsenal, 0);
});

test("authoritative goal pace is readable1x at selected1x/3x/5x, restores base exactly without double multiplication", () => {
    for (const base of [1, 3, 5]) {
        const clock = new ProjectionClock(); clock.reset(1, 300, 0, scene.focusWindows);
        clock.apply({ revision: 1, sequence: 1, time: 38, playing: true, rate: base, highlightsEnabled: true }, 0);
        const entryWall = 1000 / base;
        assert.equal(clock.time(entryWall), 39);
        assert.ok(Math.abs(clock.time(entryWall + 10000) - 49) < 1e-8);
        assert.equal(clock.effectiveRate(entryWall + 10000), 1);
        assert.ok(Math.abs(clock.time(entryWall + 20000) - 59) < 1e-8);
        assert.equal(clock.effectiveRate(entryWall + 20000), base);
        assert.ok(Math.abs(clock.time(entryWall + 21000) - (59 + base)) < 1e-8);
        clock.apply({ revision: 1, sequence: 2, time: 48, playing: true, rate: base, highlightsEnabled: true }, 0);
        assert.equal(clock.time(1000), 49, "actual one-second shot gets one full wall second");
        clock.apply({ revision: 1, sequence: 3, time: 49, playing: false, rate: base, highlightsEnabled: true }, 1000);
        assert.deepEqual(broadcastFrame(scene, clock.time(999999)), broadcastFrame(scene, 49));
        assert.equal(clock.apply({ revision: 1, sequence: 2, time: 280, playing: true, rate: 5 }, 2), false);
    }
});

test("camera backing/forward/inverse/22-target geometry agree at1x2x density and wide/tall viewports", () => {
    const players = Array.from({ length: 22 }, (_, i) => ({ id: `p${i}`, frames: [{ time: 0, x: 30 + i * 3, y: 8 + i % 5 * 11 }, { time: 300, x: 30 + i * 3, y: 8 + i % 5 * 11 }] }));
    for (const [width, height] of [[1144, 595], [800, 440], [440, 800]]) for (const dpr of [1, 2]) {
        const wide = viewportMap(width, height, width * dpr, height * dpr, ARENA);
        for (const view of [ARENA, scene.goals[0].cameraView]) {
            const map = viewportMap(width, height, width * dpr, height * dpr, view);
            const background = backgroundTransform(wide, map);
            for (const player of players) {
                const world = player.frames[0], point = fieldToViewport(map, world), inverse = viewportToField(map, point);
                assert.ok(Math.abs(inverse.x - world.x) < 1e-8 && Math.abs(inverse.y - world.y) < 1e-8);
                assert.equal(hit(players, 0, map.field, point.x, point.y), player.id);
                const pixel = wide.matrix[0] * world.x + wide.matrix[4];
                assert.ok(Math.abs(background[0] * pixel + background[4] - (map.matrix[0] * world.x + map.matrix[4])) < 1e-8);
            }
        }
        for (const point of [{ x: -3, y: 30.34 }, { x: 108, y: 37.66 }]) {
            const p = fieldToViewport(wide, point);
            assert.ok(p.x >= 0 && p.x <= width && p.y >= 0 && p.y <= height, "goal net is outside touchline but inside canvas");
        }
    }
});

test("net geometry responds at the actual goal point for perceptible1x contact and respects reduced motion", () => {
    const still = netPoints(true), moving = netPoints(true, .35, 34);
    assert.notDeepEqual(moving, still);
    assert.deepEqual(netPoints(true, .35, 34), moving);
    assert.deepEqual(netPoints(true, .35, 34, true), still);
    assert.deepEqual(netPoints(true, 3), still);
    assert.ok(moving.flat().every(p => p.x >= 105 && p.x <= 109 && p.y >= 30.34 && p.y <= 37.66));
});

test("seek, full tactical/reduced-motion view and rate-window integration have no stale replay or future facts", () => {
    assert.equal(broadcastFrame(scene, 53, false).time, 53);
    assert.deepEqual(broadcastFrame(scene, 53, false).view, ARENA);
    assert.equal(broadcastFrame(scene, 48.99).goal, null);
    assert.equal(broadcastFrame({ ...scene, goals: [] }, 53).goal, null);
    assert.equal(advancePlayback(38, 3, 300, 3, scene.focusWindows, false), 47);
    assert.equal(advancePlayback(299, 10, 300, 5, scene.focusWindows, true), 300);
});
