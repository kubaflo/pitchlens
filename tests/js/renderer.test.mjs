import test from "node:test";
import assert from "node:assert/strict";
const { initialize, loadScenario, setClock, diagnostics, dispose, openPopup, closePopup, setCaptionsEnabled } =
    await import(process.env.PITCH_TEST_RENDERER || new URL("../../src/PitchLens.App/wwwroot/js/pitch.mjs", import.meta.url));
import { at, fit } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

test("production renderer aligns22 accessible player buttons with shirts and dispatches click→popup at changing1x/2x", () => {
    const keys = ["window", "document", "ResizeObserver", "requestAnimationFrame", "cancelAnimationFrame", "performance"];
    const originals = Object.fromEntries(keys.map(k => [k, globalThis[k]]));
    const rect = { left: 40, top: 187, width: 1144, height: 595 };
    const native = [], callbacks = new Map(), documentHandlers = new Map(), observers = [];
    let wall = 0, nextFrame = null, canvasReads = 0, cardReads = 0;
    const caption = { hidden: true, textContent: "", set innerHTML(value) { throw new Error("Model text must not be parsed as HTML"); } };
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
        getBoundingClientRect: () => { canvasReads++; return { ...rect }; }, getContext: () => context,
        addEventListener: (name, fn) => callbacks.set(name, fn), removeEventListener: name => callbacks.delete(name)
    };
    const makeDialog = id => ({
        id, tagName: "DIALOG", open: false, attributes: {}, style: {
            setProperty(name, value) { this[name] = value; }, getPropertyValue(name) { return this[name] ?? ""; }
        }, handlers: new Map(), modalOpens: 0, nonmodalOpens: 0,
        setAttribute(name, value) { this.attributes[name] = value; },
        addEventListener(name, fn) { this.handlers.set(name, fn); }, removeEventListener(name) { this.handlers.delete(name); },
        showModal() { this.open = true; this.modalOpens++; }, show() { this.open = true; this.nonmodalOpens++; },
        close() { this.open = false; this.handlers.get("close")?.(); },
        contains(target) { return target === this; },
        getBoundingClientRect() {
            cardReads++;
            return { left: 100, top: 100, width: 376, height: Math.min(460, parseFloat(this.style["--player-lens-height"] ?? "460")) };
        }
    });
    const dialog = makeDialog("player-dialog"), roster = makeDialog("roster-dialog"), story = makeDialog("story-dialog");
    const dialogs = [dialog, roster, story];
    globalThis.window = { devicePixelRatio: 2, innerWidth: 1280, innerHeight: 900, addEventListener() {}, removeEventListener() {} };
    globalThis.document = {
        hidden: false, activeElement: canvas,
        getElementById: id => dialogs.find(d => d.id === id) ?? (id === "pitch-players" ? layer : id === "commentary-caption" ? caption : null),
        querySelector: selector => dialogs.find(d => d.open && (!selector.includes("aria-modal") || d.attributes["aria-modal"] === "true")),
        querySelectorAll: () => dialogs.filter(d => d.open),
        addEventListener: (name, fn) => documentHandlers.set(name, fn), removeEventListener: name => documentHandlers.delete(name),
        createElement: tag => tag === "button" ? new Button() : ({ width: 0, height: 0, getContext: () => background })
    };
    globalThis.ResizeObserver = class {
        constructor(callback) { this.callback = callback; this.disconnected = false; observers.push(this); }
        observe(target) { this.target = target; this.disconnected = false; if (target === canvas.parentElement) observation = this.callback; }
        disconnect() { this.disconnected = true; }
    };
    globalThis.performance = { now: () => wall };
    globalThis.requestAnimationFrame = callback => { nextFrame = callback; return 1; };
    globalThis.cancelAnimationFrame = () => { nextFrame = null; };
    const clip = {
        revision: 1, duration: 300, goals: [], captions: [{ id: "goal", start: 49, end: 53, text: "Goal! Erling Haaland scores!" }],
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
            ["manchester-city-doku", "Jérémy Doku", 11, 65, 12, 55, 14],
            ["arsenal-timber", "Jurrien Timber", 12, 26, 6, 38, 8],
            ["arsenal-gabriel", "Gabriel", 6, 23, 42, 30, 44],
            ["arsenal-calafiori", "Riccardo Calafiori", 33, 24, 62, 38, 60],
            ["arsenal-zubimendi", "Martin Zubimendi", 36, 36, 40, 44, 38],
            ["arsenal-martinelli", "Gabriel Martinelli", 11, 68, 62, 75, 58],
            ["manchester-city-lewis", "Rico Lewis", 82, 78, 62, 72, 60],
            ["manchester-city-guehi", "Marc Guéhi", 6, 80, 42, 74, 44],
            ["manchester-city-ait-nouri", "Rayan Aït-Nouri", 21, 79, 6, 72, 8],
            ["manchester-city-anderson", "Elliot Anderson", 5, 64, 40, 56, 38],
            ["manchester-city-kovacic", "Mateo Kovačić", 8, 56, 20, 50, 22]
        ].map(([id, name, number, x, y, endX, endY]) => ({ id, name, number,
            teamId: id.startsWith("arsenal") ? "arsenal" : "manchester-city",
            frames: [{ time: 0, x, y }, { time: 300, x: endX, y: endY }] })),
        ball: [{ time: 0, x: 6, y: 34 }, { time: 300, x: 8, y: 36 }]
    };
    let sequence = 0;
    try {
        initialize(canvas, { invokeMethodAsync(method, ...args) {
            native.push([method, ...args]);
            if (method === "SelectFromPitch") openPopup("player-dialog", args[0]);
            return Promise.resolve();
        } });
        loadScenario(JSON.stringify(clip));
        assert.equal(layer.children.length, 22);
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
                    assert.equal(button.attributes["aria-label"], `Open ${player.name} player details, ${player.teamId === "arsenal" ? "Arsenal" : "Manchester City"}, shirt ${player.number}`);
                    assert.equal(button.attributes["aria-haspopup"], undefined);
                    assert.equal(button.attributes["aria-controls"], "player-dialog");
                    const box = button.getBoundingClientRect();
                    assert.ok(Math.abs(box.left + box.width / 2 - rect.left - drawn.x / raster) < 1e-8);
                    assert.ok(Math.abs(box.top + box.height / 2 - rect.top - drawn.y / (canvas.height / rect.height)) < 1e-8);
                    button.click();
                    assert.deepEqual(native.at(-1), ["SelectFromPitch", player.id]);
                    assert.equal(dialog.open, true);
                    assert.equal(dialog.modalOpens, 0);
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
        const position = () => {
            const [, left, top] = dialog.style.transform.match(/translate\(([-\d.e]+)px, ([-\d.e]+)px\)/);
            return { left: Number(left), top: Number(top) };
        };
        for (const rate of [1, 3, 5]) {
            wall = 0;
            setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 30, playing: true, rate, selectedId: "arsenal-saka" }));
            layer.children.find(b => b.dataset.playerId === "arsenal-saka").click();
            const start = position(), beforeReads = [canvasReads, cardReads], beforeNotifications = native.length;
            for (let frame = 1; frame <= 120; frame++) {
                wall = frame * 1000 / 60;
                assert.ok(nextFrame, "opening a lens must not cancel ongoing playback");
                nextFrame();
            }
            assert.ok(Math.abs(diagnostics().time - (30 + rate * 2)) < 1e-8);
            assert.notDeepEqual(position(), start, "card follows the moving selected shirt");
            assert.deepEqual([canvasReads, cardReads], beforeReads, "following performs no per-frame layout reads");
            assert.equal(native.length, beforeNotifications, "following has no per-frame native callbacks");
            layer.children.find(b => b.dataset.playerId === "arsenal-raya").click();
            assert.equal(dialog.open, true);
            const keeperCard = position();
            layer.children.find(b => b.dataset.playerId === "arsenal-raya").click();
            assert.deepEqual(position(), keeperCard);
            const preScrollTime = diagnostics().time;
            rect.left += 13; rect.top += 7;
            documentHandlers.get("scroll")();
            assert.equal(diagnostics().time, preScrollTime);
            assert.notDeepEqual(position(), keeperCard, "scroll/layout geometry updates viewport anchoring");
            let prevented = false;
            documentHandlers.get("keydown")({ key: "Escape", preventDefault() { prevented = true; } });
            assert.equal(prevented, true); assert.equal(dialog.open, false);
            assert.ok(nextFrame, "closing a lens must leave playback running");
            openPopup("roster-dialog");
            assert.equal(roster.open, true); assert.equal(roster.modalOpens, 0);
            assert.ok(nextFrame, "roster flow must also preserve playback");
            openPopup("player-dialog", "manchester-city-donnarumma");
            assert.equal(roster.open, false);
            assert.ok(position().left < rect.left + rect.width - 100, "far-edge player flips the card inward");
            const replayControl = { closest: selector => selector.includes(".transport") ? replayControl : null };
            documentHandlers.get("click")({ target: replayControl });
            assert.equal(dialog.open, true, "replay transport remains usable without dismissing the lens");
            const transport = { closest: () => null };
            document.activeElement = transport;
            documentHandlers.get("click")({ target: transport });
            assert.equal(dialog.open, false);
            assert.equal(document.activeElement, transport, "outside close must not steal transport focus");
        }
        setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 30, playing: false, rate: 3 }));
        openPopup("player-dialog", "arsenal-saka");
        assert.equal(nextFrame, null, "opening while paused must not autoplay");
        const pausedTime = diagnostics().time;
        wall += 10000;
        closePopup("player-dialog");
        assert.equal(diagnostics().time, pausedTime);
        assert.equal(nextFrame, null);
        openPopup("player-dialog", "arsenal-saka");
        setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 240, playing: false, rate: 5 }));
        const seekCard = position();
        setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 0, playing: false, rate: 5 }));
        assert.notDeepEqual(position(), seekCard);
        openPopup("story-dialog");
        assert.equal(dialog.open, false);
        assert.equal(story.modalOpens, 1, "story stays modal");
        closePopup("story-dialog");
        setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 49, playing: false, rate: 5 }));
        assert.equal(caption.hidden, false);
        assert.equal(caption.textContent, clip.captions[0].text);
        setCaptionsEnabled(false);
        assert.equal(caption.hidden, true);
        assert.equal(caption.textContent, "");
        setCaptionsEnabled(true);
        assert.equal(caption.hidden, false);
        openPopup("player-dialog", "arsenal-saka");
        loadScenario(JSON.stringify({ ...clip, revision: 2, captions: [] }));
        assert.equal(dialog.open, false, "replacement clears the old scene's lens");
        assert.equal(caption.hidden, true);
        assert.equal(caption.textContent, "");
        assert.equal(layer.children.length, 22);
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
        assert.equal(caption.hidden, true);
        assert.equal(layer.children.length, 0);
        assert.ok(replacementButtons.every(b => b.handlers.size === 0));
        assert.equal(callbacks.size, 0);
        assert.equal(documentHandlers.size, 0);
        assert.ok(observers.every(o => o.disconnected));
    } finally {
        dispose();
        for (const [key, value] of Object.entries(originals))
            if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
    }
});
