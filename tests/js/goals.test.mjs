import test from "node:test";
import assert from "node:assert/strict";
import { goalState, ProjectionClock } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

test("300s score and celebration use exact playback event time, including pause, seek and restart", () => {
    const scene = { duration: 300, goals: [
        { id: "g1", time: 49, scorer: "Erling Haaland", teamId: "manchester-city" },
        { id: "g2", time: 120, scorer: "Viktor Gyokeres", teamId: "arsenal" }
    ] };
    assert.equal(goalState(scene, 48.999).score["manchester-city"], 0);
    assert.equal(goalState(scene, 49).score["manchester-city"], 1);
    assert.equal(goalState(scene, 49).celebration.scorer, "Erling Haaland");
    const clock = new ProjectionClock();
    clock.reset(1, 300, 0);
    clock.apply({ revision: 1, sequence: 1, time: 50, playing: false }, 0);
    assert.equal(goalState(scene, clock.time(999999)).celebration.id, "g1");
    assert.equal(goalState(scene, 56.999).celebration.id, "g1");
    assert.equal(goalState(scene, 57).celebration, null);
    assert.equal(goalState(scene, 120).score.arsenal, 1);
    assert.equal(goalState(scene, 300).celebration, null);
    assert.equal(goalState(scene, 0).score.arsenal, 0);
    assert.equal(goalState(scene, 0).score["manchester-city"], 0);
    assert.equal(goalState(scene, 120).score.arsenal, 1);
});
