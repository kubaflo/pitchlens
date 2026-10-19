import test from "node:test";
import assert from "node:assert/strict";
const { initialize, loadScenario, setClock, diagnostics, dispose, openPopup } =
    await import(process.env.PITCH_TEST_RENDERER || new URL("../../src/PitchLens.App/wwwroot/js/pitch.mjs", import.meta.url));
import { at, fit } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

test("production renderer aligns12 accessible player buttons with shirts and dispatches click→popup at changing1x/2x", () => {
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
    const layer = {
        hidden: true, children: [], style: { setProperty(name, value) { this[name] = value; } },
        appendChild(button) { this.children.push(button); button.isConnected = true; },
        replaceChildren() { this.children.forEach(b => { b.isConnected = false; }); this.children = []; }
    };
    class Button {
        tagName = "BUTTON"; dataset = {}; attributes = {}; style = {}; handlers = new Map(); isConnected = false; focusCount = 0;
        setAttribute(name, value) { this.attributes[name] = value; }
        addEventListener(name, fn) { this.handlers.set(name, fn); }
        removeEventListener(name) { this.handlers.delete(name); }
        focus() { this.focusCount++; document.activeElement = this; }
        click() { this.focus(); this.handlers.get("click")?.({ detail: 0 }); }
        getBoundingClientRect() {
            const [, x, y] = this.style.transform.match(/translate\(([-\d.e]+)px, ([-\d.e]+)px\)/);
            const size = parseFloat(layer.style["--player-size"]);
            return { left: rect.left + Number(x) - size / 2, top: rect.top + Number(y) - size / 2, width: size, height: size };
        }
    }
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
        getElementById: id => id === dialog.id ? dialog : id === "pitch-players" ? layer : null,
        querySelector: () => dialog.open ? dialog : null,
        querySelectorAll: () => dialog.open ? [dialog] : [],
        addEventListener() {}, removeEventListener() {},
        createElement: tag => tag === "button" ? new Button() : ({ width: 0, height: 0, getContext: () => background })
    };
    globalThis.ResizeObserver = class {
        constructor(callback) { observation = callback; } observe() {} disconnect() {}
    };
    globalThis.requestAnimationFrame = () => 1;
    globalThis.cancelAnimationFrame = () => {};
    const clip = {
        revision: 1, duration: 300, goals: [],
        players: [
            ["arsenal-raya", "David Raya", 1, 6, 34, 8, 36],
            ["arsenal-saliba", "William Saliba", 2, 25, 18, 38, 20],
            ["arsenal-odegaard", "Martin Odegaard", 8, 40, 30, 55, 32],
            ["arsenal-rice", "Declan Rice", 41, 35, 50, 50, 48],
            ["arsenal-saka", "Bukayo Saka", 7, 20, 12, 90, 56],
            ["arsenal-gyokeres", "Viktor Gyokeres", 14, 75, 40, 80, 44],
            ["manchester-city-donnarumma", "Gianluigi Donnarumma", 1, 99, 34, 96, 32],
            ["manchester-city-dias", "Rúben Dias", 3, 80, 20, 70, 22],
            ["manchester-city-foden", "Phil Foden", 47, 60, 48, 50, 46],
            ["manchester-city-cherki", "Rayan Cherki", 10, 55, 30, 45, 32],
            ["manchester-city-haaland", "Erling Haaland", 9, 35, 42, 25, 44],
            ["manchester-city-doku", "Jérémy Doku", 11, 65, 12, 55, 14]
        ].map(([id, name, number, x, y, endX, endY]) => ({ id, name, number,
            teamId: id.startsWith("arsenal") ? "arsenal" : "manchester-city",
            frames: [{ time: 0, x, y }, { time: 300, x: endX, y: endY }] })),
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
        assert.equal(layer.children.length, 12);
        const originalButtons = [...layer.children];
        const drawnOrder = [...clip.players].sort((a, b) => a.id < b.id ? 1 : -1);
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
                    const drawn = context.labels[drawnOrder.findIndex(p => p.id === player.id)];
                    assert.ok(Math.abs(drawn.x / raster - (transform.left + position.x * transform.scale)) < 1e-8);
                    assert.ok(Math.abs(drawn.y / (canvas.height / rect.height) - (transform.top + position.y * transform.scale)) < 1e-8);
                    const button = layer.children.find(b => b.dataset.playerId === player.id);
                    assert.equal(button.type, "button");
                    assert.equal(button.attributes["aria-label"], `${player.name}, ${player.teamId === "arsenal" ? "Arsenal" : "Manchester City"}, shirt ${player.number}`);
                    assert.equal(button.attributes["aria-haspopup"], "dialog");
                    const box = button.getBoundingClientRect();
                    assert.ok(Math.abs(box.left + box.width / 2 - rect.left - drawn.x / raster) < 1e-8);
                    assert.ok(Math.abs(box.top + box.height / 2 - rect.top - drawn.y / (canvas.height / rect.height)) < 1e-8);
                    button.click();
                    assert.deepEqual(native.at(-1), ["SelectFromPitch", player.id]);
                    assert.equal(dialog.open, true);
                    dialog.close();
                    assert.equal(document.activeElement, button);
                }
                if (diagnostics().viewport) assert.deepEqual(background.field, diagnostics().viewport.matrix);
                const keeper = clip.players[6], point = at(keeper.frames, time);
                callbacks.get("pointerdown")({ clientX: rect.left + transform.left + point.x * transform.scale,
                    clientY: rect.top + transform.top + point.y * transform.scale });
                assert.deepEqual(native.at(-1), ["SelectFromPitch", keeper.id]);
                assert.equal(dialog.open, true);
                dialog.close();
            }
            rect.width = density === 2 ? 800 : 1144; rect.height = density === 2 ? 440 : 595;
        }
        assert.deepEqual(layer.children, originalButtons); // Clock updates move existing nodes, never rebuild them.
        loadScenario(JSON.stringify({ ...clip, revision: 2 }));
        assert.equal(layer.children.length, 12);
        const notifications = native.length;
        for (const button of originalButtons) {
            assert.equal(button.handlers.size, 0);
            button.click();
        }
        assert.equal(native.length, notifications);
        const replacementButtons = [...layer.children];
        loadScenario(JSON.stringify(clip)); // Stale scene must not replace the current controls.
        assert.deepEqual(layer.children, replacementButtons);
        dispose();
        assert.equal(layer.children.length, 0);
        assert.ok(replacementButtons.every(b => b.handlers.size === 0));
        assert.equal(callbacks.size, 0);
    } finally {
        dispose();
        for (const [key, value] of Object.entries(originals))
            if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
    }
});
