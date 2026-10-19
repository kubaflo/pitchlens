import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { ProjectionClock, ReplayEngine, captionAt, goalState } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

const cues = [
    { id: "pass", start: 46, end: 49, text: "Doku finds Haaland." },
    { id: "goal", start: 49, end: 53, text: "Goal! Haaland scores!" },
    { id: "restart", start: 54, end: 57, text: "Arsenal restart." }
];
const scene = { revision: 1, duration: 300, players: [], captions: cues,
    goals: [{ id: "goal", time: 49, scorer: "Haaland", teamId: "manchester-city" }] };

test("default projection/reset is3x while explicit rate packets remain authoritative", () => {
    const clock = new ProjectionClock();
    assert.equal(clock.rate, 3);
    clock.reset(1, 300, 0);
    clock.apply({ revision: 1, sequence: 1, time: 0, playing: true }, 0);
    assert.equal(clock.time(10000), 30);
    clock.apply({ revision: 1, sequence: 2, time: 30, playing: true, rate: 1 }, 10000);
    assert.equal(clock.time(20000), 40);
    clock.reset(2, 300, 20000);
    assert.equal(clock.rate, 3);
});

test("all40 current11v11 Sol cues follow the same start-inclusive/end-exclusive selection contract as core", () => {
    const recorded = JSON.parse(readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_11v11_commentary.json", import.meta.url), "utf8")).cues;
    assert.equal(recorded.length, 40);
    for (const time of [0, 300, ...recorded.flatMap(c => [c.start - .001, c.start, c.end - .001, c.end])].reverse()) {
        assert.equal(captionAt(recorded, time), recorded.find(c => c.start <= time && time < c.end) ?? null);
        assert.equal(captionAt(recorded, time, false), null);
    }
    assert.throws(() => captionAt(recorded, NaN), RangeError);
});

test("rate packets reanchor once, remain seek/pause safe and reject stale or invalid rates", () => {
    const clock = new ProjectionClock(); clock.reset(1, 300, 0);
    clock.apply({ revision: 1, sequence: 1, time: 10, playing: true, rate: 2 }, 0);
    assert.equal(clock.time(1000), 12);
    clock.apply({ revision: 1, sequence: 2, time: 12, playing: true, rate: 5 }, 1000);
    assert.equal(clock.time(1000), 12);
    assert.equal(clock.time(2000), 17);
    clock.apply({ revision: 1, sequence: 3, time: 17, playing: false, rate: 3 }, 2000);
    assert.equal(clock.time(90000), 17);
    assert.equal(clock.apply({ revision: 1, sequence: 2, time: 200, playing: true, rate: 1 }, 90000), false);
    clock.apply({ revision: 1, sequence: 4, time: 40, playing: true, rate: 5 }, 90000);
    assert.equal(clock.time(91000), 45);
    assert.equal(clock.time(999999), 300);
    assert.throws(() => clock.apply({ revision: 1, sequence: 5, time: 20, playing: true, rate: 4 }, 0), RangeError);
    clock.reset(2, 300, 0);
    assert.equal(clock.apply({ revision: 1, sequence: 99, time: 50, playing: true, rate: 5 }, 0), false);
});

test("1x/2x/3x/5x projection is exact across render frequencies without double multiplication", () => {
    for (const rate of [1, 2, 3, 5]) for (const fps of [24, 60, 144]) {
        const clock = new ProjectionClock(); clock.reset(1, 300, 0);
        clock.apply({ revision: 1, sequence: 1, time: 10, playing: true, rate }, 1000);
        for (let i = 0; i <= fps * 10; i++) assert.ok(Math.abs(clock.time(1000 + i * 1000 / fps) - (10 + i / fps * rate)) < 1e-8);
    }
});

test("captions coalesce at5x with goals on the same replay clock; pause/CC/seek/replace/dispose have no backlog", () => {
    const engine = new ReplayEngine();
    engine.load(scene, 0);
    engine.update({ revision: 1, sequence: 1, time: 46, playing: true, rate: 5 }, 0);
    assert.equal(engine.caption(engine.frame(599)).id, "pass");
    assert.equal(engine.caption(engine.frame(600)).id, "goal");
    assert.equal(goalState(scene, engine.lastDrawTime).celebration.id, "goal");
    assert.equal(engine.caption(engine.frame(1600)).id, "restart");
    engine.update({ revision: 1, sequence: 2, time: 50, playing: false, rate: 5 }, 2000);
    assert.equal(engine.caption(engine.frame(999999)).id, "goal");
    engine.setCaptionsEnabled(false);
    assert.equal(engine.caption(), null);
    engine.setCaptionsEnabled(true);
    assert.equal(engine.caption().id, "goal");
    engine.update({ revision: 1, sequence: 3, time: 0, playing: false, rate: 5 }, 999999);
    assert.equal(engine.caption(engine.frame(999999)), null);
    engine.load({ ...scene, revision: 2, captions: [] }, 0);
    assert.equal(engine.caption(engine.frame(0)), null);
    engine.dispose();
    assert.equal(engine.caption(49), null);
    assert.equal(captionAt(cues, 53), null);
});
