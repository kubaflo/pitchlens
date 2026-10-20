import { ReplayEngine, at, keyboardAction } from "./replay.mjs";
import { broadcastFrame, prepareBroadcast, netPoints } from "./broadcast.mjs";
import { PopupHost } from "./popups.mjs";
import { pinnedEvidence, evidenceVisible, drawEvidence } from "./evidence.mjs";
import { FilmProjection } from "./director.mjs";
import { ARENA, viewportMap, backgroundTransform, applyField, fieldToViewport, pointerInDrawnViewport } from "./viewport.mjs";

let engine, canvas, viewport, context, background, observer, reference, frameId = null;
let pointerHandler, visibilityHandler, keyboardHandler, rangeDown, rangeUp, range, width = 0, height = 0, dragging = false;
let popups = null, lensPlayerId = null, canvasBox = null, geometryHandler = null;
let map = null, wideMap = null, markerSize = null;
let playerLayer = null;
let evidence = null;
let film = null;
let reducedMotion = false, motionQuery = null, motionHandler = null, lastClockTime = 0;
const playerTargets = new Map();
const now = () => performance.now();

export function initialize(element, dotnet) {
    dispose();
    canvas = element; viewport = element.parentElement; reference = dotnet; context = canvas.getContext("2d");
    playerLayer = document.getElementById("pitch-players");
    if (!playerLayer) throw new Error("Pitch player controls are missing.");
    playerLayer.hidden = true;
    background = document.createElement("canvas");
    motionQuery = window.matchMedia?.("(prefers-reduced-motion: reduce)") ?? null;
    reducedMotion = motionQuery?.matches ?? false;
    motionHandler = () => { reducedMotion = motionQuery.matches; draw(); };
    motionQuery?.addEventListener("change", motionHandler);
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
        if (film) return;
        const box = canvas.getBoundingClientRect();
        const point = map && pointerInDrawnViewport(map, box, event.clientX, event.clientY);
        if (point) engine?.select(point.x, point.y, map.width, map.height, now(), map.field);
    };
    canvas.addEventListener("pointerdown", pointerHandler);
    visibilityHandler = () => {
        if (document.hidden) {
            playerLayer.hidden = true; popups?.hidePlayer(true);
            if (film) {
                const time = film.frame(now()).filmTime; film.apply(time, false, now());
                reference?.invokeMethodAsync("FilmHidden").catch(error => {
                    console.error("Film visibility synchronization failed", error);
                    const banner = document.getElementById("blazor-error-ui");
                    if (banner) { banner.textContent = "Film paused; visibility synchronization failed. Return to the match."; banner.style.display = "block"; }
                });
            }
            else engine?.hide(now());
            stop();
        } else { cacheGeometry(); draw(); }
    };
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
    wideMap = viewportMap(width, height, canvas.width, canvas.height, ARENA);
    map = wideMap;
    markerSize = null;
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
    drawNet(c, false); drawNet(c, true);
    draw();
}
function drawNet(c, right, elapsed = null, goalY = 34) {
    const mesh = netPoints(right, elapsed, goalY, reducedMotion);
    if (elapsed === null) { c.fillStyle = "#18382c"; c.fillRect(right ? 105 : -3, 30.34, 3, 7.32); }
    c.lineWidth = .12; c.strokeStyle = elapsed === null ? "#b5cec1" : "#fff";
    for (const row of mesh) {
        c.beginPath(); row.forEach((p, i) => i ? c.lineTo(p.x, p.y) : c.moveTo(p.x, p.y)); c.stroke();
    }
    for (let column = 0; column < 5; column++) {
        c.beginPath(); mesh.forEach((row, i) => i ? c.lineTo(row[column].x, row[column].y) : c.moveTo(row[column].x, row[column].y)); c.stroke();
    }
    c.lineWidth = .4; c.strokeStyle = "#f4f8ee";
    c.beginPath(); c.moveTo(right ? 105 : 0, 30.34); c.lineTo(right ? 105 : 0, 37.66); c.stroke();
}
function circle(c, x, y, radius, fill, stroke) {
    c.beginPath(); c.arc(x, y, radius, 0, Math.PI * 2);
    if (fill) { c.fillStyle = fill; c.fill(); }
    if (stroke) { c.strokeStyle = stroke; c.stroke(); }
}
function draw() {
    stop();
    if (!engine?.scenario || !canvas || !width || !height || document.hidden) return;
    const filmFrame = film?.frame(now());
    const clockTime = filmFrame?.sourceTime ?? engine.frame(now());
    lastClockTime = clockTime;
    const pinned = evidenceVisible(evidence, engine.clock, clockTime);
    if (!pinned) evidence = null;
    let broadcast = broadcastFrame(engine.scenario, clockTime, !film && !pinned && engine.clock.highlightsEnabled && !reducedMotion);
    if (filmFrame) {
        const goal = engine.scenario.goals.find(g => g.id === filmFrame.scene.focusEventId);
        broadcast = { ...broadcast, time: clockTime, phase: filmFrame.holding ? "reaction" : "shot",
            view: !reducedMotion && filmFrame.scene.camera === "finish" && goal ? goal.cameraView : ARENA,
            replay: false, deadBall: false, contact: goal && filmFrame.holding ? { goal, age: 0 } : null };
    }
    const time = broadcast.time;
    engine.lastDrawTime = time; // Hit testing and following lenses use the actual replay camera, not a future live position.
    const following = popups?.floating?.id === "player-dialog";
    let lensPoint = null;
    map = viewportMap(width, height, canvas.width, canvas.height, broadcast.view);
    const size = `${Math.max(24, 4 * map.field.scale)}px`;
    if (markerSize !== size) { playerLayer.style.setProperty("--player-size", size); markerSize = size; }
    const c = context; c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, canvas.width, canvas.height);
    c.setTransform(...backgroundTransform(wideMap, map)); c.drawImage(background, 0, 0);
    applyField(c, map);
    if (pinned) drawEvidence(c, evidence.fact);
    if (filmFrame?.holding) {
        const points = engine.scenario.ball.filter(p => p.time >= filmFrame.scene.sourceStart && p.time <= clockTime);
        drawEvidence(c, { ball: [at(engine.scenario.ball, filmFrame.scene.sourceStart), ...points, at(engine.scenario.ball, clockTime)] });
    }
    const evidenceTag = document.getElementById("evidence-view-tag");
    if (evidenceTag) {
        evidenceTag.hidden = !pinned;
        evidenceTag.textContent = pinned ? `PINNED EVIDENCE · ${evidence.fact.id} · ${evidence.fact.time.toFixed(1)}s · Paused historical view` : "";
    }
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
        if (p.id === broadcast.goal?.scorerId) {
            c.lineWidth = .45;
            circle(c, point.x, point.y, 3, null, "#05f0ff");
        }
        if (p.id === engine.selectedId) { c.lineWidth = .6; circle(c, point.x, point.y, 2.8, null, "#fff"); }
        circle(c, point.x + .15, point.y + .3, 2.2, "#23382b", null);
        c.lineWidth = .18; circle(c, point.x, point.y, 2, p.teamId === "arsenal" ? "#00ff87" : "#ff83b8", "#f4f4ef");
        c.fillStyle = "#37003c"; c.font = "700 2.5px Barlow, sans-serif"; c.textAlign = "center"; c.textBaseline = "middle";
        c.fillText(String(p.number), point.x, point.y);
        if (p.id === broadcast.goal?.scorerId && broadcast.phase === "reaction") {
            c.strokeStyle = "#f4f8ee"; c.lineWidth = .35;
            c.beginPath(); c.moveTo(point.x - 1, point.y - .5); c.lineTo(point.x - 2, point.y - 2);
            c.moveTo(point.x + 1, point.y - .5); c.lineTo(point.x + 2, point.y - 2); c.stroke();
            c.font = `600 ${Math.max(1.6, 16 / map.field.scale)}px Barlow, sans-serif`;
            c.fillStyle = "#fff"; c.fillText(broadcast.goal.scorer, point.x, point.y + 3.8);
        }
        const target = playerTargets.get(p.id);
        if (target) {
            const centre = fieldToViewport(map, point);
            target.button.style.transform = `translate(${centre.x}px, ${centre.y}px) translate(-50%, -50%)`;
            target.button.hidden = centre.x < 0 || centre.x > width || centre.y < 0 || centre.y > height;
        }
    }
    const shot = (engine.scenario.goals ?? []).find(g => g.shotStart <= time && time <= g.time);
    if (shot) {
        const from = at(engine.scenario.ball, Math.max(shot.shotStart, time - .45)), to = at(engine.scenario.ball, time);
        c.strokeStyle = "#fff"; c.lineWidth = .25; c.beginPath(); c.moveTo(from.x, from.y); c.lineTo(to.x, to.y); c.stroke();
    }
    if (broadcast.contact) {
        const goal = broadcast.contact.goal, age = broadcast.contact.age, right = goal.teamId === "arsenal";
        drawNet(c, right, age, goal.goalY ?? 34);
        const settle = reducedMotion ? 1.5 : 1.8 * (1 - Math.exp(-age * 5));
        circle(c, (goal.goalX ?? (right ? 105 : 0)) + (right ? settle : -settle), goal.goalY ?? 34, .65, "#fff", "#14281c");
    } else if (!broadcast.deadBall || broadcast.replay) {
        const ball = at(engine.scenario.ball, time); c.lineWidth = .2; circle(c, ball.x, ball.y, .85, "#fff", "#000");
    }
    c.globalAlpha = 1;
    updateTransport(clockTime, broadcast);
    if (lensPoint && canvasBox) {
        const local = fieldToViewport(map, lensPoint);
        const scaleX = canvasBox.width / map.width, scaleY = canvasBox.height / map.height;
        const visible = local.x >= 0 && local.x <= width && local.y >= 0 && local.y <= height;
        popups.hidePlayer(!visible);
        if (visible) popups.follow({ x: canvasBox.left + local.x * scaleX, y: canvasBox.top + local.y * scaleY },
                canvasBox, Math.max(12, 2.8 * map.field.scale * Math.max(scaleX, scaleY)) + 12);
    }
    playerLayer.hidden = !!film;
    const filmLabel = document.getElementById("film-position"), filmText = document.getElementById("film-insight");
    if (filmLabel && filmFrame) filmLabel.textContent = `FILM ${filmFrame.filmTime.toFixed(1)} / ${filmFrame.duration.toFixed(1)}s · SOURCE ${clockTime.toFixed(1)}s · ${filmFrame.holding ? "FREEZE / EVIDENCE" : `RECORDED FOOTAGE ${filmFrame.scene.rate}x`}`;
    if (filmText) { filmText.hidden = !filmFrame?.holding; filmText.textContent = filmFrame?.holding ? filmFrame.scene.text : ""; }
    if (film ? film.playing && filmFrame.filmTime < filmFrame.duration : engine.clock.playing && clockTime < engine.clock.duration)
        frameId = requestAnimationFrame(draw);
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
            if (film) return;
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
        film = null;
        evidence = null;
        prepareBroadcast(engine.scenario);
        lastClockTime = 0;
        popups?.close("player-dialog", false); popups?.close("roster-dialog", false); lensPlayerId = null;
        buildPlayerTargets(); updateTransport(0); draw();
    }
}
function updateTransport(time, broadcast = broadcastFrame(engine.scenario, time, engine.clock.highlightsEnabled && !reducedMotion)) {
    const output = document.getElementById("replay-clock");
    if (output) output.textContent = `${time.toFixed(1).padStart(4, "0")} / ${engine.scenario.duration.toFixed(1)} s`;
    if (range && !dragging) range.value = String(time);
    const caption = document.getElementById("commentary-caption");
    if (caption) {
        const cue = engine.caption(time);
        caption.hidden = !cue;
        caption.setAttribute("data-goal", broadcast.goal && cue?.eventId === broadcast.goal.id ? "true" : "false");
        if (cue && caption.textContent !== cue.text) caption.textContent = cue.text;
        if (!cue && caption.textContent) caption.textContent = "";
    }
    const score = document.getElementById("match-score");
    const value = `${broadcast.score.arsenal} : ${broadcast.score["manchester-city"]}`;
    if (score && score.textContent !== value) score.textContent = value;
    const bugScore = document.getElementById("pitch-bug-score"), bugClock = document.getElementById("pitch-bug-clock");
    if (bugScore && bugScore.textContent !== value) bugScore.textContent = value;
    if (bugClock) bugClock.textContent = `${Math.floor(time / 60)}:${String(Math.floor(time % 60)).padStart(2, "0")}`;
    const pace = document.getElementById("effective-speed");
    if (pace) {
        const active = engine.clock.highlightsEnabled && engine.clock.windows.some(w => w.start <= time && time < w.end);
        const label = film ? `Match ${engine.clock.rate}x saved` : active ? `Highlight ${engine.clock.effectiveRate(now())}x · base ${engine.clock.rate}x` : `${engine.clock.rate}x`;
        if (pace.textContent !== label) pace.textContent = label;
    }
    const banner = document.getElementById("goal-banner");
    if (banner) {
        banner.hidden = !broadcast.goal;
        const title = document.getElementById("goal-title"), detail = document.getElementById("goal-detail");
        if (broadcast.goal) {
            const heading = broadcast.goal.scorer;
            const facts = `${broadcast.goal.time.toFixed(1)}s · Synthetic match`;
            if (title.textContent !== heading) title.textContent = heading;
            if (detail.textContent !== facts) detail.textContent = facts;
            if (banner.getAttribute("data-team") !== broadcast.goal.teamId) banner.setAttribute("data-team", broadcast.goal.teamId);
            if (banner.getAttribute("data-phase") !== broadcast.phase) banner.setAttribute("data-phase", broadcast.phase);
            banner.style.setProperty("--goal-fade", String(broadcast.fade));
            const crest = document.getElementById("goal-crest"), team = document.getElementById("goal-team"), goalScore = document.getElementById("goal-score");
            const image = `images/${broadcast.goal.teamId === "arsenal" ? "arsenal" : "manchester_city"}_crest.png`;
            if (crest.getAttribute("src") !== image) { crest.setAttribute("src", image); crest.alt = broadcast.goal.teamName; }
            if (team.textContent !== broadcast.goal.teamName) team.textContent = broadcast.goal.teamName;
            if (goalScore.textContent !== value) goalScore.textContent = value;
        }
    }
    const replayTag = document.getElementById("goal-replay-tag");
    if (replayTag) replayTag.hidden = !broadcast.replay;
}
export function setCaptionsEnabled(enabled) { engine?.setCaptionsEnabled(enabled); draw(); }
export function setClock(json) {
    if (engine?.update(JSON.parse(json), now())) {
        updateTransport(engine.clock.time(now()));
        draw();
    }
}
export function diagnostics() { return engine ? { ...engine.diagnostics, viewport: map, time: lastClockTime, renderTime: engine.lastDrawTime } : null; }
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
export function loadFilm(json) {
    if (!json) { film = null; draw(); return; }
    const plan = JSON.parse(json);
    if (plan.revision !== engine.scenario.revision) throw new Error("Film belongs to a different scene.");
    film = new FilmProjection(plan);
    popups?.close("player-dialog", false); popups?.close("roster-dialog", false);
    draw();
}
export function setFilmClock(position, playing, sequence, revision) {
    if (!film) throw new Error("No film loaded.");
    if (!Number.isSafeInteger(sequence) || revision !== engine.scenario.revision) throw new Error("Invalid film clock packet.");
    if (sequence <= film.sequence) return;
    film.sequence = sequence;
    film.apply(position, playing, now()); draw();
}
export function setEvidence(revision, json) {
    evidence = pinnedEvidence(revision, json, engine.clock);
    draw();
}
export function dispose() {
    film = null;
    evidence = null;
    const evidenceTag = document.getElementById("evidence-view-tag");
    if (evidenceTag) { evidenceTag.hidden = true; evidenceTag.textContent = ""; }
    popups?.dispose(); popups = null; lensPlayerId = null; canvasBox = null;
    stop(); observer?.disconnect();
    window.removeEventListener("resize", resize);
    if (canvas && pointerHandler) canvas.removeEventListener("pointerdown", pointerHandler);
    if (visibilityHandler) document.removeEventListener("visibilitychange", visibilityHandler);
    if (keyboardHandler) window.removeEventListener("keydown", keyboardHandler);
    if (geometryHandler) document.removeEventListener("scroll", geometryHandler, true);
    if (range && rangeDown) range.removeEventListener("pointerdown", rangeDown);
    if (rangeUp) window.removeEventListener("pointerup", rangeUp);
    motionQuery?.removeEventListener("change", motionHandler);
    const caption = document.getElementById("commentary-caption");
    if (caption) { caption.hidden = true; caption.textContent = ""; }
    const goalBanner = document.getElementById("goal-banner"), replayTag = document.getElementById("goal-replay-tag");
    if (goalBanner) goalBanner.hidden = true;
    if (replayTag) replayTag.hidden = true;
    engine?.dispose();
    clearPlayerTargets();
    engine = canvas = viewport = context = background = observer = reference = range = null;
    map = wideMap = null; markerSize = null;
    playerLayer = null;
    motionQuery = motionHandler = null; reducedMotion = false; lastClockTime = 0;
    geometryHandler = null;
    pointerHandler = visibilityHandler = keyboardHandler = rangeDown = rangeUp = null;
}
