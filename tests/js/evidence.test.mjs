import test from "node:test";
import assert from "node:assert/strict";
import { pinnedEvidence, evidenceVisible, drawEvidence } from "../../src/PitchLens.App/wwwroot/js/evidence.mjs";
import { viewportMap, fieldToViewport, viewportToField, ARENA } from "../../src/PitchLens.App/wwwroot/js/viewport.mjs";

const fact = { id: "pass", start: 10, time: 12, ball: [{ time: 10, x: 40, y: 20 }, { time: 12, x: 60, y: 30 }] };
test("evidence pins only to the exact paused scene and disappears on play, seek or replacement", () => {
    const clock = { revision: 3, playing: false, position: 12 };
    const pin = pinnedEvidence(3, JSON.stringify(fact), clock);
    assert.ok(evidenceVisible(pin, clock, 12));
    assert.equal(evidenceVisible(pin, { ...clock, playing: true }, 12), false);
    assert.equal(evidenceVisible(pin, clock, 11), false);
    assert.equal(evidenceVisible(pin, { ...clock, revision: 4 }, 12), false);
    assert.throws(() => pinnedEvidence(2, JSON.stringify(fact), clock));
    assert.throws(() => pinnedEvidence(3, JSON.stringify(fact), { ...clock, playing: true }));
    assert.throws(() => pinnedEvidence(3, JSON.stringify({ ...fact, ball: [{ time: 300, x: 40, y: 20 }] }), clock));
    assert.throws(() => pinnedEvidence(3, JSON.stringify({ ...fact, ball: [{ time: 10, x: -1, y: 20 }] }), clock));
});
test("recorded overlay uses exact authored coordinates and the same wide/tall density-independent inverse as shirts/lenses", () => {
    const drawn = [];
    const c = { beginPath() {}, moveTo: (x, y) => drawn.push([x, y]), lineTo: (x, y) => drawn.push([x, y]), stroke() {}, arc() {} };
    drawEvidence(c, fact);
    assert.deepEqual(drawn, [[40, 20], [60, 30]]);
    for (const [w, h] of [[1200, 600], [600, 900]]) for (const density of [1, 2]) {
        const map = viewportMap(w, h, w * density, h * density, ARENA);
        for (const point of fact.ball) {
            const back = viewportToField(map, fieldToViewport(map, point));
            assert.ok(Math.abs(back.x - point.x) < 1e-10 && Math.abs(back.y - point.y) < 1e-10);
        }
    }
});
