import { ReplayEngine, at, keyboardAction, goalState } from "./replay.mjs";
import { PopupHost } from "./popups.mjs";
import { viewportMap, applyField, fieldToViewport, pointerInDrawnViewport } from "./viewport.mjs";

let engine, canvas, viewport, context, background, observer, reference, frameId = null;
let pointerHandler, visibilityHandler, keyboardHandler, rangeDown, rangeUp, range, width = 0, height = 0, dragging = false;
let popups = null, lensPlayerId = null, canvasBox = null, geometryHandler = null;
let map = null;
let playerLayer = null;
const playerTargets = new Map();
const now = () => performance.now();

export function initialize(element, dotnet) {
    dispose();
    canvas = element; viewport = element.parentElement; reference = dotnet; context = canvas.getContext("2d");
    playerLayer = document.getElementById("pitch-players");
    if (!playerLayer) throw new Error("Pitch player controls are missing.");
    playerLayer.hidden = true;
    background = document.createElement("canvas");
    engine = new ReplayEngine((method, ...args) => {
        if (reference) reference.invokeMethodAsync(method, ...args).catch(error => {
            console.error("Pitch interaction failed", error);
            const banner = document.getElementById("blazor-error-ui");
            if (banner) {
                banner.textContent = "Pitch interaction failed. Close and reopen the app.";
                banner.style.display = "block";
            }
            dispose();
        });
    });
    observer = new ResizeObserver(resize); observer.observe(viewport);
    window.addEventListener("resize", resize);
    pointerHandler = event => {
        const box = canvas.getBoundingClientRect();
        const point = map && pointerInDrawnViewport(map, box, event.clientX, event.clientY);
        if (point) engine?.select(point.x, point.y, map.width, map.height, now());
    };
    canvas.addEventListener("pointerdown", pointerHandler);
    visibilityHandler = () => { if (document.hidden) { playerLayer.hidden = true; popups?.hidePlayer(true); engine?.hide(now()); stop(); } else { cacheGeometry(); draw(); } };
    document.addEventListener("visibilitychange", visibilityHandler);
    keyboardHandler = event => {
        if (document.querySelector?.("dialog[open][aria-modal='true']")) return;
        const command = keyboardAction(event);
        if (command) { event.preventDefault(); engine?.keyboard(command); }
    };
    window.addEventListener("keydown", keyboardHandler);
    range = document.getElementById("timeline");
    rangeDown = () => { dragging = true; };
    rangeUp = () => { dragging = false; };
    range?.addEventListener("pointerdown", rangeDown);
    window.addEventListener("pointerup", rangeUp);
    geometryHandler = () => { if (canvas) { cacheGeometry(); draw(); } };
    document.addEventListener("scroll", geometryHandler, true);
    resize();
    document.fonts?.ready.then(() => { if (canvas) draw(); });
}
function stop() { if (frameId !== null) cancelAnimationFrame(frameId); frameId = null; }
function cacheGeometry() {
    const box = canvas.getBoundingClientRect();
    canvasBox = { left: box.left, top: box.top, width: box.width, height: box.height,
        right: box.left + box.width, bottom: box.top + box.height };
}
function resize() {
    if (!canvas || !viewport) return;
    const box = viewport.getBoundingClientRect();
    width = box.width; height = box.height;
    if (width <= 0 || height <= 0) { playerLayer.hidden = true; popups?.hidePlayer(true); canvasBox = null; stop(); return; }
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
    background.width = canvas.width; background.height = canvas.height;
    map = viewportMap(width, height, canvas.width, canvas.height);
    cacheGeometry();
    playerLayer.style.setProperty("--player-size", `${Math.max(24, 4 * map.field.scale)}px`);
    const c = background.getContext("2d");
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.fillStyle = "#23372f"; c.fillRect(0, 0, background.width, background.height);
    applyField(c, map);
    c.fillStyle = "#4b7355"; c.fillRect(0, 0, 105, 68);
    c.fillStyle = "#50795a";
    for (let i = 0; i < 10; i += 2) c.fillRect(i * 10.5, 0, 10.5, 68);
    c.strokeStyle = "#d2dfd0"; c.lineWidth = .25;
    c.strokeRect(0, 0, 105, 68); c.beginPath(); c.moveTo(52.5, 0); c.lineTo(52.5, 68); c.stroke();
    c.beginPath(); c.arc(52.5, 34, 9.15, 0, Math.PI * 2); c.stroke();
    c.strokeRect(0, 13.84, 16.5, 40.32); c.strokeRect(88.5, 13.84, 16.5, 40.32);
    c.strokeRect(0, 24.84, 5.5, 18.32); c.strokeRect(99.5, 24.84, 5.5, 18.32);
    c.lineWidth = .65; c.beginPath(); c.moveTo(0, 30.34); c.lineTo(0, 37.66); c.moveTo(105, 30.34); c.lineTo(105, 37.66); c.stroke();
    draw();
}
function circle(c, x, y, radius, fill, stroke) {
    c.beginPath(); c.arc(x, y, radius, 0, Math.PI * 2);
    if (fill) { c.fillStyle = fill; c.fill(); }
    if (stroke) { c.strokeStyle = stroke; c.stroke(); }
}
function draw() {
    stop();
    if (!engine?.scenario || !canvas || !width || !height || document.hidden) return;
    const time = engine.frame(now());
    const following = popups?.floating?.id === "player-dialog";
    let lensPoint = null;
    const c = context; c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, canvas.width, canvas.height); c.drawImage(background, 0, 0);
    applyField(c, map);
    const selected = engine.scenario.players.find(p => p.id === engine.selectedId);
    if (selected) {
        c.beginPath();
        const points = selected.frames.filter(f => f.time < time).concat(at(selected.frames, time));
        points.forEach((p, i) => i ? c.lineTo(p.x, p.y) : c.moveTo(p.x, p.y));
        c.strokeStyle = "#d7b8ff"; c.lineWidth = .4; c.stroke();
    }
    for (const p of engine.drawPlayers) {
        const point = at(p.frames, time);
        if (following && p.id === lensPlayerId) lensPoint = point;
        if (p.id === engine.selectedId) { c.lineWidth = .6; circle(c, point.x, point.y, 2.8, null, "#fff"); }
        circle(c, point.x + .15, point.y + .3, 2.2, "#23382b", null);
        c.lineWidth = .18; circle(c, point.x, point.y, 2, p.teamId === "arsenal" ? "#00ff87" : "#ff83b8", "#f4f4ef");
        c.fillStyle = "#37003c"; c.font = "700 2.5px Barlow, sans-serif"; c.textAlign = "center"; c.textBaseline = "middle";
        c.fillText(String(p.number), point.x, point.y);
        const target = playerTargets.get(p.id);
        if (target) {
            const centre = fieldToViewport(map, point);
            target.button.style.transform = `translate(${centre.x}px, ${centre.y}px) translate(-50%, -50%)`;
        }
    }
    const ball = at(engine.scenario.ball, time); c.lineWidth = .2; circle(c, ball.x, ball.y, .85, "#fff", "#000");
    updateTransport(time);
    if (lensPoint && canvasBox) {
        const local = fieldToViewport(map, lensPoint);
        const scaleX = canvasBox.width / map.width, scaleY = canvasBox.height / map.height;
        popups.hidePlayer(false);
        popups.follow({ x: canvasBox.left + local.x * scaleX, y: canvasBox.top + local.y * scaleY },
            canvasBox, Math.max(12, 2.8 * map.field.scale * Math.max(scaleX, scaleY)) + 12);
    }
    playerLayer.hidden = false;
    if (engine.clock.playing && time < engine.clock.duration) frameId = requestAnimationFrame(draw);
}
function clearPlayerTargets() {
    for (const { button, activate } of playerTargets.values()) button.removeEventListener("click", activate);
    playerTargets.clear();
    playerLayer?.replaceChildren();
}
function buildPlayerTargets() {
    clearPlayerTargets();
    const revision = engine.scenario.revision;
    for (const p of engine.scenario.players) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "player-target";
        button.dataset.playerId = p.id;
        button.setAttribute("aria-label", `Open ${p.name} player details, ${p.teamId === "arsenal" ? "Arsenal" : "Manchester City"}, shirt ${p.number}`);
        button.setAttribute("aria-controls", "player-dialog");
        button.style.zIndex = String(engine.drawPlayers.findIndex(player => player.id === p.id) + 1);
        const activate = () => {
            if (engine?.scenario?.revision !== revision) return;
            button.focus({ preventScroll: true });
            engine.selectPlayer(p.id);
        };
        button.addEventListener("click", activate);
        playerTargets.set(p.id, { button, activate });
        playerLayer.appendChild(button);
    }
}
export function loadScenario(json) {
    if (engine?.load(JSON.parse(json), now())) {
        popups?.close("player-dialog", false); popups?.close("roster-dialog", false); lensPlayerId = null;
        buildPlayerTargets(); draw();
    }
}
function updateTransport(time) {
    const output = document.getElementById("replay-clock");
    if (output) output.textContent = `${time.toFixed(1).padStart(4, "0")} / ${engine.scenario.duration.toFixed(1)} s`;
    if (range && !dragging) range.value = String(time);
    const caption = document.getElementById("commentary-caption");
    if (caption) {
        const cue = engine.caption(time);
        caption.hidden = !cue;
        if (cue && caption.textContent !== cue.text) caption.textContent = cue.text;
        if (!cue && caption.textContent) caption.textContent = "";
    }
    const state = goalState(engine.scenario, time);
    const score = document.getElementById("match-score");
    const value = `${state.score.arsenal} : ${state.score["manchester-city"]}`;
    if (score && score.textContent !== value) score.textContent = value;
    const banner = document.getElementById("goal-banner");
    if (banner) {
        banner.hidden = !state.celebration;
        const title = document.getElementById("goal-title"), detail = document.getElementById("goal-detail");
        if (state.celebration) {
            const heading = `Goal · ${state.celebration.scorer}`;
            const facts = `${state.celebration.teamName} · ${value} · Synthetic match`;
            if (title.textContent !== heading) title.textContent = heading;
            if (detail.textContent !== facts) detail.textContent = facts;
        }
    }
}
export function setCaptionsEnabled(enabled) { engine?.setCaptionsEnabled(enabled); draw(); }
export function setClock(json) {
    if (engine?.update(JSON.parse(json), now())) {
        updateTransport(engine.clock.time(now()));
        draw();
    }
}
export function diagnostics() { return engine ? { ...engine.diagnostics, viewport: map, time: engine.lastDrawTime } : null; }
export function openPopup(id, selectedId = engine?.selectedId) {
    popups ??= new PopupHost(document, window);
    if (id === "player-dialog" && !engine?.scenario?.players.some(p => p.id === selectedId)) throw new Error("Selected player is missing.");
    if (id === "roster-dialog") document.getElementById("players-button")?.focus({ preventScroll: true });
    lensPlayerId = id === "player-dialog" ? selectedId : null;
    cacheGeometry();
    popups.open(id);
    draw();
}
export function closePopup(id) { popups?.close(id); }
export function dispose() {
    popups?.dispose(); popups = null; lensPlayerId = null; canvasBox = null;
    stop(); observer?.disconnect();
    window.removeEventListener("resize", resize);
    if (canvas && pointerHandler) canvas.removeEventListener("pointerdown", pointerHandler);
    if (visibilityHandler) document.removeEventListener("visibilitychange", visibilityHandler);
    if (keyboardHandler) window.removeEventListener("keydown", keyboardHandler);
    if (geometryHandler) document.removeEventListener("scroll", geometryHandler, true);
    if (range && rangeDown) range.removeEventListener("pointerdown", rangeDown);
    if (rangeUp) window.removeEventListener("pointerup", rangeUp);
    const caption = document.getElementById("commentary-caption");
    if (caption) { caption.hidden = true; caption.textContent = ""; }
    engine?.dispose();
    clearPlayerTargets();
    engine = canvas = viewport = context = background = observer = reference = range = null;
    map = null;
    playerLayer = null;
    geometryHandler = null;
    pointerHandler = visibilityHandler = keyboardHandler = rangeDown = rangeUp = null;
}
