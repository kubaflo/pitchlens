import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const { initialize, loadScenario, setClock, diagnostics, dispose, openPopup, closePopup, setCaptionsEnabled, setEvidence, loadFilm, setFilmClock, setLens } =
    await import(process.env.PITCH_TEST_RENDERER || new URL("../../src/PitchLens.App/wwwroot/js/pitch.mjs", import.meta.url));
import { at } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";
import { fitView, ARENA } from "../../src/PitchLens.App/wwwroot/js/viewport.mjs";

test("production renderer aligns22 accessible player buttons with shirts and dispatches click→popup at changing1x/2x", () => {
    const keys = ["window", "document", "ResizeObserver", "requestAnimationFrame", "cancelAnimationFrame", "performance"];
    const originals = Object.fromEntries(keys.map(k => [k, globalThis[k]]));
    const rect = { left: 40, top: 187, width: 1144, height: 595 };
    const native = [], callbacks = new Map(), documentHandlers = new Map(), observers = [];
    let wall = 0, nextFrame = null, canvasReads = 0, cardReads = 0;
    const caption = { hidden: true, textContent: "", setAttribute() {}, set innerHTML(value) { throw new Error("Model text must not be parsed as HTML"); } };
    const goalNodes = Object.fromEntries(["goal-banner", "goal-title", "goal-detail", "goal-team", "goal-score", "goal-crest", "goal-replay-tag", "match-score", "evidence-view-tag", "film-position", "film-insight", "pitch-lens-tag","effective-speed"].map(id => [id, {
        hidden: true, textContent: "", attributes: {}, style: { setProperty(name, value) { this[name] = value; } },
        setAttribute(name, value) { this.attributes[name] = value; }, getAttribute(name) { return this.attributes[name]; }
    }]));
    const motionHandlers = new Map();
    const motion = { matches: false, addEventListener: (name, fn) => motionHandlers.set(name, fn), removeEventListener: name => motionHandlers.delete(name) };
    let observation;
    class Context {
        matrix = [1, 0, 0, 1, 0, 0]; labels = []; field = null; fills = [];
        setTransform(...m) { this.matrix = m; }
        translate(x, y) {
            const m = this.matrix;
            m[4] += m[0] * x + m[2] * y; m[5] += m[1] * x + m[3] * y;
        }
        scale(x, y) { const m = this.matrix; m[0] *= x; m[1] *= x; m[2] *= y; m[3] *= y; }
        fillRect(x, y, width, height) { this.fills.push({ x, y, width, height, matrix:[...this.matrix] }); } clearRect() { this.labels = []; } drawImage() {}
        strokeRect(x, y, w, h) { if (w === 105 && h === 68) this.field = [...this.matrix]; }
        colors = [];
        beginPath() {} moveTo() {} lineTo() {} stroke() {} arc() {} fill() { this.colors.push(this.fillStyle); }
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
    const dialog = makeDialog("player-dialog"), roster = makeDialog("roster-dialog"), story = makeDialog("story-dialog"),analysis=makeDialog("analysis-dialog"),more=makeDialog("more-dialog");
    const dialogs = [dialog, roster, story,analysis,more];
    globalThis.window = { devicePixelRatio: 2, innerWidth: 1280, innerHeight: 900, matchMedia: () => motion, addEventListener() {}, removeEventListener() {} };
    globalThis.document = {
        hidden: false, activeElement: canvas,
        getElementById: id => dialogs.find(d => d.id === id) ?? goalNodes[id] ?? (id === "pitch-players" ? layer : id === "commentary-caption" ? caption : null),
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
        assert.ok(background.fills.some(r => r.x === -3 && r.width === 3 && r.height === 7.32), "left goal net has visible depth outside touchline");
        assert.ok(background.fills.some(r => r.x === 105 && r.width === 3 && r.height === 7.32), "right goal net has visible depth outside touchline");
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
                const transform = fitView(rect.width, rect.height, ARENA), raster = canvas.width / rect.width;
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
        const evidenceFact = { id: "observed-pass", start: 28, time: 30,
            ball: [28, 30].map(time => ({ time, ...at(clip.ball, time) })) };
        const evidenceNotifications = native.length;
        setEvidence(1, JSON.stringify(evidenceFact));
        assert.equal(goalNodes["evidence-view-tag"].hidden, false);
        assert.match(goalNodes["evidence-view-tag"].textContent, /PINNED EVIDENCE.*30.0s.*Paused historical/);
        assert.equal(native.length, evidenceNotifications, "pin drawing makes no SDK/native request");
        assert.equal(diagnostics().time, diagnostics().renderTime, "evidence never substitutes a different footage time");
        for (const player of clip.players) {
            const button = layer.children.find(b => b.dataset.playerId === player.id), box = button.getBoundingClientRect();
            const world = at(player.frames, 30), field = diagnostics().viewport.field;
            assert.ok(Math.abs(box.left + box.width / 2 - rect.left - field.left - world.x * field.scale) < 1e-8);
        }
        assert.throws(() => setEvidence(2, JSON.stringify(evidenceFact)), /paused match/);
        openPopup("player-dialog", "arsenal-saka");
        assert.equal(nextFrame, null, "opening while paused must not autoplay");
        const pausedTime = diagnostics().time;
        wall += 10000;
        closePopup("player-dialog");
        assert.equal(diagnostics().time, pausedTime);
        assert.equal(nextFrame, null);
        openPopup("player-dialog", "arsenal-saka");
        setClock(JSON.stringify({ revision: 1, sequence: ++sequence, time: 240, playing: false, rate: 5 }));
        assert.equal(goalNodes["evidence-view-tag"].hidden, true, "seeking removes pinned historical overlay");
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
        const goalClip = { ...clip, revision: 3, focusWindows: [{ start: 39, end: 59 }],
            ball: [{ time: 0, x: 20, y: 12 }, { time: 39, x: 75, y: 46 }, { time: 47, x: 87, y: 54 },
                { time: 49, x: 105, y: 34 }, { time: 59, x: 52.5, y: 34 }, { time: 300, x: 30, y: 18 }], goals: [{
            id: "g", time: 49, shotStart: 47, restartTime: 59, scorer: "Bukayo Saka", scorerId: "arsenal-saka",
            teamId: "arsenal", teamName: "Arsenal"
        }], players: clip.players.map(p => p.id === "arsenal-saka" ? { ...p,
            frames: [{ time: 0, x: 20, y: 12 }, { time: 49, x: 90, y: 56 }, { time: 300, x: 30, y: 18 }] } : p) };
        observation();
        loadScenario(JSON.stringify(goalClip));
        for (const rate of [1, 3, 5]) {
            setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 48, playing: true, rate, highlightsEnabled: true }));
            const startWall = wall;
            wall = startWall + 1000;
            assert.ok(nextFrame); nextFrame();
            assert.equal(diagnostics().time, 49, "one-second actual shot is visible for one wall second at every base rate");
            assert.equal(goalNodes["goal-title"].textContent, "Bukayo Saka");
            assert.equal(goalNodes["effective-speed"].hidden,false);
            assert.equal(goalNodes["effective-speed"].textContent,"Goal highlight · 1x");
        }
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 49, playing: false, rate: 3, highlightsEnabled: true }));
        assert.equal(goalNodes["goal-banner"].hidden, false);
        assert.equal(goalNodes["goal-title"].textContent, "Bukayo Saka");
        assert.equal(goalNodes["goal-team"].textContent, "Arsenal");
        assert.equal(goalNodes["goal-score"].textContent, "1 : 0");
        assert.equal(goalNodes["goal-crest"].attributes.src, "images/arsenal_crest.png");
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 52, playing: false, rate: 3, highlightsEnabled: true }));
        assert.equal(diagnostics().time, 52, "clock remains authoritative while camera shows goal footage");
        assert.ok(diagnostics().renderTime < 49);
        assert.equal(layer.children.find(b => b.dataset.playerId === "arsenal-raya").hidden, true, "off-camera targets cannot spill over transport");
        assert.equal(goalNodes["goal-replay-tag"].hidden, false);
        const filmed = at(goalClip.players.find(p => p.id === "arsenal-saka").frames, diagnostics().renderTime);
        const transform = diagnostics().viewport.field;
        callbacks.get("pointerdown")({ clientX: rect.left + transform.left + filmed.x * transform.scale,
            clientY: rect.top + transform.top + filmed.y * transform.scale });
        assert.deepEqual(native.at(-1), ["SelectFromPitch", "arsenal-saka"], "replay clicks hit the displayed footage, not later live positions");
        assert.equal(dialog.open, true);
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 52, playing: false, rate: 3, highlightsEnabled: false }));
        assert.equal(goalNodes["effective-speed"].hidden,true,"normal3x is shown only by the speed selector, not a duplicate status");
        assert.equal(goalNodes["effective-speed"].textContent,"");
        assert.equal(diagnostics().renderTime, 52);
        assert.equal(layer.children.find(b => b.dataset.playerId === "arsenal-raya").hidden, false);
        assert.equal(diagnostics().time, 52);
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 52, playing: false, rate: 3, highlightsEnabled: true }));
        assert.ok(diagnostics().renderTime < 49);
        motion.matches = true; motionHandlers.get("change")();
        assert.equal(diagnostics().renderTime, 52, "system reduced-motion preference suppresses extra camera jumps");
        motion.matches = false; motionHandlers.get("change")();
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 59, playing: true, rate: 5, highlightsEnabled: true }));
        assert.equal(diagnostics().renderTime, 59);
        assert.equal(goalNodes["goal-replay-tag"].hidden, true);
        assert.equal(goalNodes["match-score"].textContent, "1 : 0");
        assert.equal(goalNodes["effective-speed"].hidden,true,"temporary pace disappears after highlight boundary");
        setClock(JSON.stringify({ revision: 3, sequence: ++sequence, time: 0, playing: false, rate: 5 }));
        assert.equal(goalNodes["goal-banner"].hidden, true);
        assert.equal(goalNodes["match-score"].textContent, "0 : 0");
        setCaptionsEnabled(true);
        setClock(JSON.stringify({revision:3,sequence:++sequence,time:49,playing:true,rate:3,highlightsEnabled:false}));
        const captionPackets=native.length;wall+=1000;nextFrame();
        assert.equal(diagnostics().time,52,"subtitles do not slow or hold selected3x");
        assert.equal(caption.textContent,goalClip.captions[0].text);
        wall+=1000;nextFrame();assert.equal(diagnostics().time,55);
        assert.equal(caption.hidden,true,"expired cues are not stale filler");
        assert.equal(native.length,captionPackets,"caption changes never make native/model calls");
        setClock(JSON.stringify({revision:3,sequence:++sequence,time:0,playing:false,rate:5}));
        const edit = { revision: 3, factsHash: "test-edit", scenes: [
            { sourceStart: 39, sourceEnd: 49, rate: 1, holdSeconds: 4, camera: "finish", focusEventId: goalClip.goals[0].id, text: "The recorded finish." },
            { sourceStart: 59, sourceEnd: 65, rate: .5, holdSeconds: 3, camera: "tactical", focusEventId: "later", text: "The response." }
        ] };
        loadFilm(JSON.stringify(edit)); setFilmClock(9, false, ++sequence, 3);
        assert.equal(diagnostics().renderTime, 48, "film source comes from the film clock, not base5x/native main position");
        assert.equal(goalNodes["match-score"].textContent, "0 : 0");
        assert.equal(goalNodes["film-insight"].hidden, true, "finish narrative cannot leak into setup footage");
        assert.equal(layer.hidden, true, "player interaction is explicitly unavailable in film");
        const filmNotifications = native.length;
        callbacks.get("pointerdown")({ clientX: rect.left + 100, clientY: rect.top + 100 });
        layer.children[0].click();
        assert.equal(native.length, filmNotifications);
        setFilmClock(10, false, ++sequence, 3);
        assert.equal(diagnostics().renderTime, 49);
        assert.equal(goalNodes["match-score"].textContent, "1 : 0");
        assert.equal(goalNodes["film-insight"].textContent, "The recorded finish.");
        assert.equal(goalNodes["film-insight"].hidden, false);
        assert.equal(goalNodes["goal-replay-tag"].hidden, true, "film never recursively invokes the automatic goal replay");
        assert.match(goalNodes["film-position"].textContent, /FREEZE/);
        for (const density of [1, 2]) {
            window.devicePixelRatio = density; observation();
            assert.equal(diagnostics().renderTime, 49);
            const field = diagnostics().viewport.field, rasterX = canvas.width / rect.width, rasterY = canvas.height / rect.height;
            const shirtLabels = context.labels.filter(label => /^\d+$/.test(label.text));
            for (const [i, p] of [...goalClip.players].sort((a,b)=>a.id<b.id?1:-1).entries()) {
                const actual = at(p.frames,49), label = shirtLabels[i];
                assert.ok(Math.abs(label.x/rasterX - field.left - actual.x*field.scale)<1e-8);
                assert.ok(Math.abs(label.y/rasterY - field.top - actual.y*field.scale)<1e-8);
            }
            assert.equal(layer.hidden,true);
        }
        setFilmClock(14, false, ++sequence, 3);
        setFilmClock(0, false, sequence - 1, 3);
        assert.equal(diagnostics().renderTime, 59, "exact cut enters the next authored source clip");
        assert.equal(goalNodes["film-insight"].hidden, true);
        motion.matches = true; motionHandlers.get("change")();
        assert.equal(diagnostics().viewport.view, ARENA);
        motion.matches = false; motionHandlers.get("change")();
        loadFilm(null);
        assert.equal(layer.hidden, false);
        assert.equal(diagnostics().renderTime, 0, "exiting film restores canonical source snapshot");
        const clubs=[{id:"liverpool",name:"Liverpool",color:"#e32939",crest:"images/liverpool_crest.svg"},
            {id:"chelsea",name:"Chelsea",color:"#5291ff",crest:"images/chelsea_crest.png"}];
        const clubFixture={...goalClip,revision:4,teams:clubs,
            players:goalClip.players.map((p,i)=>({...p,id:`${i<11?"liverpool":"chelsea"}-player-${i}`,name:`Verified Player ${i}`,teamId:clubs[i<11?0:1].id})),
            goals:[{...goalClip.goals[0],teamId:"chelsea",teamName:"Chelsea",scorerId:"chelsea-player-12",scorer:"Verified Player 12",goalX:0,playerIds:["chelsea-player-12"]}]};
        loadScenario(JSON.stringify(clubFixture));context.colors=[];
        setClock(JSON.stringify({revision:4,sequence:++sequence,time:49,playing:false,highlightsEnabled:true}));
        assert.equal(goalNodes["match-score"].textContent,"0 : 1");
        assert.equal(goalNodes["goal-team"].textContent,"Chelsea");
        assert.equal(goalNodes["goal-crest"].attributes.src,"images/chelsea_crest.png");
        assert.ok(context.colors.includes("#e32939")&&context.colors.includes("#5291ff"),"actual shirts use captured club display colours");
        assert.match(layer.children[0].attributes["aria-label"],/Liverpool/);
        assert.match(layer.children[21].attributes["aria-label"],/Chelsea/);
        assert.ok(layer.children.every(b=>!b.attributes["aria-label"].includes("Arsenal")&&!b.attributes["aria-label"].includes("Manchester City")));
        setClock(JSON.stringify({revision:4,sequence:++sequence,time:48.9,playing:false}));
        assert.equal(goalNodes["match-score"].textContent,"0 : 0");
        assert.equal(layer.children.length,22);
        const network={window:{revision:4,fixture:"liverpool/chelsea",start:0,cutoff:49},
            mode:"connections",subjectId:"liverpool",nodes:[
                {playerId:clubFixture.players[0].id,x:10,y:20,samples:2},
                {playerId:clubFixture.players[1].id,x:40,y:30,samples:2}],
            edges:[{id:"pair",from:clubFixture.players[0].id,to:clubFixture.players[1].id,count:2,eventIds:["one","two"],passMetres:[10,20]}],
            cells:[],selectedEdgeId:"pair"};
        const beforeLenses=native.length;
        setClock(JSON.stringify({revision:4,sequence:++sequence,time:49,playing:false,rate:3,highlightsEnabled:true}),JSON.stringify(network));
        assert.equal(diagnostics().lens.selectedEdgeId,"pair");
        assert.match(goalNodes["pitch-lens-tag"].textContent,/HISTORICAL CONNECTIONS/);
        const originalSize={width:rect.width,height:rect.height};
        for(const [w,h,density] of [[1144,595,1],[700,800,2],[1144,595,2]]){
            rect.width=w;rect.height=h;window.devicePixelRatio=density;observation();
            const field=diagnostics().viewport.field,rasterX=canvas.width/rect.width,rasterY=canvas.height/rect.height;
            const historical=context.labels.find(l=>l.text==="H1");
            assert.ok(Math.abs(historical.x/rasterX-field.left-10*field.scale)<1e-8);
            assert.ok(Math.abs(historical.y/rasterY-field.top-20*field.scale)<1e-8);
            assert.equal(layer.children.length,22,"historical nodes never create/replace live click targets");
        }
        const heat={...network,mode:"movement",subjectId:clubFixture.players[0].id,nodes:[],edges:[],selectedCell:7,
            cells:Array.from({length:28},(_,i)=>({index:i,column:i%7,row:Math.floor(i/7),x:i%7*15,y:Math.floor(i/7)*17,
                width:15,height:17,seconds:i===7?49:0}))};
        context.fills=[];setLens(JSON.stringify(heat),sequence);
        assert.ok(context.fills.some(r=>r.x===0&&r.y===17&&r.width===15&&r.height===17),"exact time-weighted cell is drawn under the player layer");
        assert.equal(diagnostics().lens.selectedCell,7);
        for(const [w,h,density] of [[700,800,1],[1144,595,2]]){
            rect.width=w;rect.height=h;window.devicePixelRatio=density;context.fills=[];observation();
            const fill=context.fills.find(r=>r.x===0&&r.y===17&&r.width===15&&r.height===17);
            const field=diagnostics().viewport.field,rasterX=canvas.width/rect.width,rasterY=canvas.height/rect.height;
            assert.ok(Math.abs(fill.matrix[4]/rasterX-field.left)<1e-8,"heat uses the same camera X transform as players");
            assert.ok(Math.abs((fill.matrix[3]*17+fill.matrix[5])/rasterY-field.top-17*field.scale)<1e-8,"heat uses the same camera Y transform at both densities");
        }
        const overlayBodies=[...layer.children];
        setLens(JSON.stringify(network),sequence-1);
        assert.equal(diagnostics().lens.mode,"movement","late query packet cannot replace current selection");
        assert.deepEqual(layer.children,overlayBodies);
        assert.equal(native.length,beforeLenses,"all lens drawing and camera changes are local and read-only");
        layer.children[0].click();
        assert.deepEqual(native.at(-1),["SelectFromPitch",clubFixture.players[0].id],"movement shading never intercepts the real player target");
        assert.equal(dialog.open,true);assert.ok(diagnostics().lens);
        closePopup("player-dialog");
        setClock(JSON.stringify({revision:4,sequence:++sequence,time:48,playing:false,rate:3,highlightsEnabled:false}));
        assert.equal(diagnostics().lens,null,"backward seek hides stale later-cutoff data before replacement");
        setLens(JSON.stringify(network),sequence-1);
        assert.equal(diagnostics().lens,null);
        setLens(JSON.stringify({...network,window:{...network.window,cutoff:48}}),sequence);
        assert.ok(diagnostics().lens);
        openPopup("analysis-dialog");
        assert.equal(analysis.nonmodalOpens,1);assert.equal(analysis.modalOpens,0);
        assert.equal(analysis.style.transform,undefined);
        openPopup("more-dialog");assert.equal(analysis.open,false);assert.equal(more.nonmodalOpens,1);
        const lensFilm={revision:4,factsHash:"local",scenes:[{sourceStart:39,sourceEnd:49,rate:1,holdSeconds:3,camera:"finish",focusEventId:"g",text:"Observed finish."}]};
        loadFilm(JSON.stringify(lensFilm));setFilmClock(10,false,++sequence,4);
        assert.equal(analysis.open,false,"Director transition closes Analysis");
        assert.equal(more.open,false,"Director transition closes More");
        assert.equal(diagnostics().lens,null,"Director never paints lenses using the paused main-match cutoff");
        assert.equal(goalNodes["pitch-lens-tag"].hidden,true);
        loadFilm(null);
        assert.ok(diagnostics().lens,"film exit restores the paused match lens, not film coordinates");
        setLens(null,sequence-1); // clock's sequence predates the separate film packet
        assert.equal(diagnostics().lens,null);
        rect.width=originalSize.width;rect.height=originalSize.height;observation();
        const dense=JSON.parse(readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_11v11_dense_narration.json",import.meta.url),"utf8"));
        openPopup("analysis-dialog");
        openPopup("more-dialog");
        loadScenario(JSON.stringify({...clip,revision:5,captions:dense.cues}));
        assert.equal(analysis.open,false,"new fixture closes Analysis rather than reopening stale choices");
        assert.equal(more.open,false,"new fixture closes More");
        assert.equal(diagnostics().lens,null,"new revision clears all old fixture metrics");
        const beforeDense=native.length;
        for(const rate of [1,3,5]){
            const anchorWall=wall,seen=new Set();
            setClock(JSON.stringify({revision:5,sequence:++sequence,time:0,playing:true,rate,highlightsEnabled:false}));
            for(let time=0;time<=300;time+=.25){
                wall=anchorWall+time/rate*1000;nextFrame();
                const actual=diagnostics().time;
                assert.ok(Math.abs(actual-time)<1e-8,"dense captions cannot alter selected playback speed");
                const cue=dense.cues.find(c=>c.start<=actual&&actual<c.end);
                assert.equal(caption.hidden,!cue);
                if(cue){assert.equal(caption.textContent,cue.text);seen.add(cue.id);}
            }
            assert.deepEqual([...seen],dense.cues.map(c=>c.id),"all87 exact cues render in order at1/3/5x without a speech queue");
        }
        setClock(JSON.stringify({revision:5,sequence:++sequence,time:85.9,playing:false,rate:3,highlightsEnabled:false}));
        assert.equal(caption.textContent,dense.cues.find(c=>c.start<=85.9&&85.9<c.end).text);
        assert.ok(!caption.textContent.includes("Goal!"),"backward seek never retains the later goal call");
        const densePausedTime=diagnostics().time;wall+=10000;
        assert.equal(nextFrame,null,"paused captions do not keep an animation or audio loop alive");
        assert.equal(diagnostics().time,densePausedTime);
        assert.equal(native.length,beforeDense,"dense captions make no native/model/audio callback");
        dispose();
        assert.equal(caption.hidden, true);
        assert.equal(goalNodes["pitch-lens-tag"].hidden,true);
        assert.equal(layer.children.length, 0);
        assert.ok(replacementButtons.every(b => b.handlers.size === 0));
        assert.equal(callbacks.size, 0);
        assert.equal(documentHandlers.size, 0);
        assert.ok(observers.every(o => o.disconnected));
        assert.equal(motionHandlers.size, 0);
    } finally {
        dispose();
        for (const [key, value] of Object.entries(originals))
            if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
    }
});
