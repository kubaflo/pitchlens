import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { captionAt } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

test("actual40-cue story package uses exact observed bounds and the same seek-safe production caption selector", () => {
    const packageData = JSON.parse(readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_11v11_narration.json", import.meta.url)));
    const paths = JSON.parse(readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_11v11_paths.json", import.meta.url)));
    assert.equal(packageData.cues.length, 40);
    assert.equal(packageData.chapters.length, 12);
    const events = new Map(paths.events.map(e => [e.id, e]));
    for (const cue of packageData.cues) {
        assert.notEqual(captionAt(packageData.cues, cue.start - .0001)?.id, cue.id);
        assert.equal(captionAt(packageData.cues, cue.start)?.id, cue.id);
        assert.notEqual(captionAt(packageData.cues, cue.end)?.id, cue.id);
        for (const id of cue.evidenceIds) assert.ok(events.get(id).time <= cue.start);
    }
    for (const time of [300, 0, 176, 85.9, 86, 175.9, 120, 0]) {
        const cue = captionAt(packageData.cues, time);
        if (cue) assert.ok(events.get(cue.eventId).time <= time);
        for (const chapter of packageData.chapters.filter(c => c.revealTime <= time))
            for (const id of chapter.eventIds) assert.ok(events.get(id).time <= time);
    }
    assert.equal(captionAt(packageData.cues, 86).eventId, "e28");
    assert.equal(captionAt(packageData.cues, 176).eventId, "e55");
});
