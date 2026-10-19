import test from "node:test";
import assert from "node:assert/strict";
import { initialize, dispose } from "../../src/PitchLens.App/wwwroot/js/pitch.mjs";

test("resize follows the grid wrapper, not the canvas backing size, and releases observation", () => {
    const originals = Object.fromEntries(["window", "document", "ResizeObserver", "cancelAnimationFrame"].map(k => [k, globalThis[k]]));
    let observer;
    const rect = { width: 800, height: 300 };
    const viewport = { getBoundingClientRect: () => ({ ...rect }) };
    const context = Object.fromEntries(["setTransform", "fillRect", "translate", "scale", "strokeRect", "beginPath",
        "moveTo", "lineTo", "stroke", "arc"].map(k => [k, () => {}]));
    const canvas = {
        width: 300, height: 150, parentElement: viewport,
        getContext: () => context,
        getBoundingClientRect() { return { width: this.width, height: this.height }; },
        addEventListener() {}, removeEventListener() {}
    };
    const listeners = new Map();
    const playerLayer = { hidden: false, style: { setProperty() {} }, replaceChildren() {} };
    globalThis.window = {
        devicePixelRatio: 2,
        addEventListener: (name, handler) => listeners.set(name, handler),
        removeEventListener: name => listeners.delete(name)
    };
    globalThis.document = {
        hidden: false, createElement: () => ({ getContext: () => context }),
        getElementById: id => id === "pitch-players" ? playerLayer : null, addEventListener() {}, removeEventListener() {}
    };
    globalThis.ResizeObserver = class {
        constructor(callback) { this.callback = callback; observer = this; }
        observe(target) { this.target = target; }
        disconnect() { this.disconnected = true; }
    };
    globalThis.cancelAnimationFrame = () => {};
    try {
        initialize(canvas, { invokeMethodAsync: () => Promise.resolve() });
        assert.equal(observer.target, viewport);
        assert.deepEqual([canvas.width, canvas.height], [1600, 600]);
        observer.callback();
        observer.callback();
        assert.deepEqual([canvas.width, canvas.height], [1600, 600]);
        rect.width = 550; rect.height = 230;
        observer.callback();
        assert.deepEqual([canvas.width, canvas.height], [1100, 460]);
        rect.width = rect.height = 0;
        observer.callback();
        assert.equal(playerLayer.hidden, true);
        rect.width = 800; rect.height = 300;
        observer.callback();
        assert.deepEqual([canvas.width, canvas.height], [1600, 600]);
        const retired = observer;
        dispose();
        assert.equal(retired.disconnected, true);
        assert.equal(listeners.size, 0);
        retired.callback();
        assert.deepEqual([canvas.width, canvas.height], [1600, 600]);
    } finally {
        dispose();
        for (const [key, value] of Object.entries(originals))
            if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
    }
});
