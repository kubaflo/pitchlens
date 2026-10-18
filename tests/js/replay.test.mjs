import test from "node:test";
import assert from "node:assert/strict";
import { ProjectionClock, ReplayEngine, at, fit, hit } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

const players = [
    { id: "h2", frames: [{ time: 0, x: 30, y: 30 }, { time: 72, x: 40, y: 30 }] },
    { id: "h1", frames: [{ time: 0, x: 30, y: 30 }, { time: 72, x: 40, y: 30 }] }
];
const clip = { revision: 1, duration: 72, players, ball: players[0].frames };
test("native anchors reset seeks and ignore stale clock packets", () => {
    const clock = new ProjectionClock();
    clock.reset(1, 72, 0);
    clock.apply({ revision: 1, sequence: 1, time: 10, playing: true }, 1000);
    assert.equal(clock.time(2000), 11);
    clock.apply({ revision: 1, sequence: 2, time: 3, playing: false }, 2000);
    assert.equal(clock.time(99999), 3);
    assert.equal(clock.apply({ revision: 1, sequence: 1, time: 60, playing: true }, 3000), false);
    assert.equal(clock.time(4000), 3);
    clock.reset(2, 72, 4000);
    assert.equal(clock.apply({ revision: 1, sequence: 9, time: 60, playing: true }, 4001), false);
    assert.equal(clock.time(5000), 0);
});
test("scenario replacement resets motion and rejects old clips and packets", () => {
    const engine = new ReplayEngine();
    engine.load(clip, 0);
    engine.update({ revision: 1, sequence: 1, time: 60, playing: true }, 0);
    assert.equal(engine.frame(1000), 61);
    engine.load({ ...clip, revision: 2 }, 1000);
    assert.equal(engine.frame(2000), 0);
    assert.equal(engine.load(clip, 2000), false);
    assert.equal(engine.update({ revision: 1, sequence: 99, time: 70, playing: true }, 2000), false);
    assert.equal(engine.diagnostics.scenarioLoads, 2);
});
test("600 animation frames perform zero native notifications and transfer no scenario again", () => {
    let nativeCalls = 0;
    const engine = new ReplayEngine(() => nativeCalls++);
    engine.load(clip, 0);
    engine.update({ revision: 1, sequence: 1, time: 0, playing: true, selectedId: "h1" }, 0);
    for (let i = 0; i < 600; i++) assert.ok(Math.abs(engine.frame(i * 1000 / 60) - i / 60) < 1e-10);
    assert.equal(nativeCalls, 0);
    assert.deepEqual(engine.diagnostics, { frames: 600, notifications: 0, scenarioLoads: 1, clockPackets: 1 });
});
test("tab hiding freezes and reports once; disposal releases callbacks", () => {
    const messages = [];
    const engine = new ReplayEngine((...args) => messages.push(args));
    engine.load(clip, 0);
    engine.update({ revision: 1, sequence: 1, time: 4, playing: true }, 0);
    engine.hide(2000);
    assert.deepEqual(messages, [["PageHidden", 6, 1]]);
    assert.equal(engine.frame(10000), 6);
    engine.dispose();
    engine.hide(11000);
    engine.select(300, 300, 1050, 680, 11000);
    assert.equal(engine.frame(11000), null);
    assert.equal(messages.length, 1);
});
test("click selection uses the last drawn positions, not a later clock prediction", () => {
    const messages = [];
    const engine = new ReplayEngine((...args) => messages.push(args));
    engine.load(clip, 0);
    engine.update({ revision: 1, sequence: 1, time: 0, playing: true }, 0);
    engine.frame(0);
    engine.select(300, 300, 1050, 680, 36000);
    assert.deepEqual(messages, [["SelectFromPitch", "h1"]]);
});
test("wide/tall transforms exclude letterboxes and select stable ordinal overlap winner", () => {
    for (const [w, h] of [[1200, 400], [400, 1200], [1050, 680]]) {
        const transform = fit(w, h);
        const x = transform.left + 30 * transform.scale, y = transform.top + 30 * transform.scale;
        assert.equal(hit(players, 0, transform, x, y), "h1");
        assert.equal(hit([...players].reverse(), 0, transform, x, y), "h1");
        assert.equal(hit(players, 0, transform, transform.left - 1, transform.top), null);
    }
    assert.throws(() => fit(0, 100), RangeError);
});
test("interpolation and end clamp are deterministic across frame frequencies", () => {
    assert.deepEqual(at([{ time: 0, x: 0, y: 0 }, { time: 2, x: 3, y: 4 }], 1), { x: 1.5, y: 2 });
    for (const rate of [24, 60, 144]) {
        const clock = new ProjectionClock(); clock.reset(1, 72, 0);
        clock.apply({ revision: 1, sequence: 1, time: 0, playing: true }, 0);
        for (let i = 0; i < rate * 20; i++) clock.time(i * 1000 / rate);
        assert.equal(clock.time(51123.45), 51.12345);
        assert.equal(clock.time(999999), 72);
    }
});
