import test from "node:test";
import assert from "node:assert/strict";
const { initialize, loadScenario, setClock, diagnostics, dispose, openPopup } =
    await import(process.env.PITCH_TEST_RENDERER || new URL("../../src/PitchLens.App/wwwroot/js/pitch.mjs", import.meta.url));
import { at, fit } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

test("production renderer uses the same backing transform for field, all shirts and actual click→popup at changing1x/2x", () => {
    const keys = ["window", "document", "ResizeObserver", "requestAnimationFrame", "cancelAnimationFrame"];
    const originals = Object.fromEntries(keys.map(k => [k, globalThis[k]]));
    const rect = { left: 40, top: 187, width: 1144, height: 595 };
    const native = [], callbacks = new Map();
    let observation;
    class Context {
        matrix = [1, 0, 0, 1, 0, 0]; labels = []; field = null;
        setTransform(...m) { this.matrix = m; }
        translate(x, y) {
            const m = this.matrix;
            m[4] += m[0] * x + m[2] * y; m[5] += m[1] * x + m[3] * y;
        }
        scale(x, y) { const m = this.matrix; m[0] *= x; m[1] *= x; m[2] *= y; m[3] *= y; }
        fillRect() {} clearRect() { this.labels = []; } drawImage() {}
        strokeRect(x, y, w, h) { if (w === 105 && h === 68) this.field = [...this.matrix]; }
        beginPath() {} moveTo() {} lineTo() {} stroke() {} arc() {} fill() {}
        fillText(text, x, y) {
            const m = this.matrix;
            this.labels.push({ text, x: m[0] * x + m[2] * y + m[4], y: m[1] * x + m[3] * y + m[5] });
        }
    }
    const context = new Context(), background = new Context();
    const canvas = {
        width: 300, height: 150, isConnected: true, focus() {}, closest: () => null,
        parentElement: { getBoundingClientRect: () => ({ ...rect }) },
        getBoundingClientRect: () => ({ ...rect }), getContext: () => context,
        addEventListener: (name, fn) => callbacks.set(name, fn), removeEventListener: name => callbacks.delete(name)
    };
    const dialog = {
        id: "player-dialog", tagName: "DIALOG", open: false, style: {}, handlers: new Map(),
        addEventListener(name, fn) { this.handlers.set(name, fn); }, removeEventListener(name) { this.handlers.delete(name); },
        showModal() { this.open = true; }, close() { this.open = false; this.handlers.get("close")?.(); },
        getBoundingClientRect: () => ({ left: 100, top: 100, width: 376, height: 460 })
    };
    globalThis.window = { devicePixelRatio: 2, innerWidth: 1280, innerHeight: 900, addEventListener() {}, removeEventListener() {} };
    globalThis.document = {
        hidden: false, activeElement: canvas,
        getElementById: id => id === dialog.id ? dialog : null,
        querySelector: () => dialog.open ? dialog : null,
        querySelectorAll: () => dialog.open ? [dialog] : [],
        addEventListener() {}, removeEventListener() {},
        createElement: () => ({ width: 0, height: 0, getContext: () => background })
    };
    globalThis.ResizeObserver = class {
        constructor(callback) { observation = callback; } observe() {} disconnect() {}
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => {};
    const clip = {
        revision: 1, duration: 300, goals: [],
        players: [
            { id: "arsenal-raya", teamId: "arsenal", number: 1, frames: [{ time: 0, x: 6, y: 34 }, { time: 300, x: 8, y: 36 }] },
            { id: "manchester-city-donnarumma", teamId: "manchester-city", number: 2, frames: [{ time: 0, x: 99, y: 34 }, { time: 300, x: 96, y: 32 }] },
            { id: "arsenal-saka", teamId: "arsenal", number: 7, frames: [{ time: 0, x: 20, y: 12 }, { time: 150, x: 90, y: 56 }, { time: 300, x: 30, y: 18 }] }
        ],
        ball: [{ time: 0, x: 6, y: 34 }, { time: 300, x: 8, y: 36 }]
    };
    let sequence = 0;
    try {
        initialize(canvas, { invokeMethodAsync(method, ...args) {
            native.push([method, ...args]);
            if (method === "SelectFromPitch") openPopup("player-dialog", true, true);
            return Promise.resolve();
        } });
        loadScenario(JSON.stringify(clip));
        for (const density of [2, 1, 2]) {
            window.devicePixelRatio = density;
            observation();
            // Reproduce the screenshot's exact failure condition: density changes after the background was rasterized.
            if (!process.env.PITCH_TEST_STABLE_DPR) window.devicePixelRatio = density === 2 ? 1 : 2;
            for (const time of [0, 66.8, 120, 180, 240, 300]) {
                setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time, playing: false, selectedId: "arsenal-saka" }));
                const transform = fit(rect.width, rect.height), raster = canvas.width / rect.width;
                for (const player of clip.players) {
                    const position = at(player.frames, time);
                    const drawn = context.labels.find(label => label.text === String(player.number));
                    assert.ok(Math.abs(drawn.x / raster - (transform.left + position.x * transform.scale)) < 1e-8);
                    assert.ok(Math.abs(drawn.y / (canvas.height / rect.height) - (transform.top + position.y * transform.scale)) < 1e-8);
                }
                if (diagnostics().viewport) assert.deepEqual(background.field, diagnostics().viewport.matrix);
                const keeper = clip.players[1], point = at(keeper.frames, time);
                callbacks.get("pointerdown")({ clientX: rect.left + transform.left + point.x * transform.scale,
                    clientY: rect.top + transform.top + point.y * transform.scale });
                assert.deepEqual(native.at(-1), ["SelectFromPitch", keeper.id]);
                assert.equal(dialog.open, true);
                dialog.close();
            }
            rect.width = density === 2 ? 800 : 1144; rect.height = density === 2 ? 440 : 595;
        }
        dispose();
        assert.equal(callbacks.size, 0);
    } finally {
        dispose();
        for (const [key, value] of Object.entries(originals))
            if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
    }
});
