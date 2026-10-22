import { ReplayEngine, at, keyboardAction } from "./replay.mjs";
import { broadcastFrame, prepareBroadcast, netPoints } from "./broadcast.mjs";
import { PopupHost } from "./popups.mjs";
import { pinnedEvidence, evidenceVisible, drawEvidence } from "./evidence.mjs";
import { FilmProjection } from "./director.mjs";
import { validateLens, drawLens } from "./lenses.mjs";
import { PlayerView, DEFAULT_VIEW, validateView, smartView, pickTarget } from "./player-view.mjs";
import { ARENA, viewportMap, backgroundTransform, applyField, fieldToViewport, pointerInDrawnViewport } from "./viewport.mjs";

let engine, canvas, viewport, context, background, observer, reference, frameId = null;
let pointerHandler, visibilityHandler, keyboardHandler, rangeDown, rangeUp, range, width = 0, height = 0, dragging = false;
let popups = null, lensPlayerId = null, canvasBox = null, geometryHandler = null;
let map = null, wideMap = null, markerSize = null;
let playerLayer = null;
let evidence = null;
let film = null;
let pitchLens = null, lensSequence = -1, visibleLens = null;
let playerView = null, viewSettings = DEFAULT_VIEW, drawnTargets = [], cameraOwner = "user", selectedGeometry = null;
let viewCameraStatus = null, viewImageStatus = null, viewWide = null, viewDetail = null, lastOwner = null;
let fontSet = null, fontLoadedHandler = null;
let ui = {};
let reducedMotion = false, motionQuery = null, motionHandler = null, lastClockTime = 0;
const playerTargets = new Map();
const now = () => performance.now();
const teams = () => engine.scenario.teams ?? [
    {id:"arsenal",name:"Arsenal",color:"#00ff87",crest:"images/arsenal_crest.png"},
    {id:"manchester-city",name:"Manchester City",color:"#ff83b8",crest:"images/manchester_city_crest.png"}
];
const club = id => teams().find(t=>t.id===id);

export function initialize(element, dotnet) {
    dispose();
    canvas = element; viewport = element.parentElement; reference = dotnet; context = canvas.getContext("2d");
    playerLayer = document.getElementById("pitch-players");
    if (!playerLayer) throw new Error("Pitch player controls are missing.");
    playerLayer.hidden = true;
    viewCameraStatus=document.getElementById("view-camera-status");viewImageStatus=document.getElementById("view-image-status");
    viewWide=document.getElementById("view-wide-camera");viewDetail=document.getElementById("view-detail-camera");
    ui=Object.fromEntries(["pitch-lens-tag","evidence-view-tag","film-position","film-insight","replay-clock",
        "commentary-caption","match-score","effective-speed","goal-banner","goal-title","goal-detail",
        "goal-crest","goal-team","goal-score","goal-replay-tag"].map(id=>[id,document.getElementById(id)]));
    playerView=new PlayerView(draw,message=>{
        console.error(message);if(viewImageStatus)viewImageStatus.textContent=message;
    });
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
        if (point) {
            const id=pickTarget(drawnTargets,point,width,height);
            if(id)engine?.selectPlayer(id);
        }
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
    const currentPlayerView=playerView;
    fontSet=document.fonts;
    fontLoadedHandler=()=>{if(playerView===currentPlayerView){currentPlayerView.clearNumberMetrics();draw();}};
    fontSet?.addEventListener?.("loadingdone",fontLoadedHandler);
    fontSet?.ready.then(fontLoadedHandler);
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
    const ballPoint=at(engine.scenario.ball,time);
    const selected = engine.scenario.players.find(p => p.id === engine.selectedId);
    cameraOwner=film?"film":pinned?"pinned":broadcast.phase!=="live"?"goal":"user";
    const detail=viewSettings.style===5&&viewSettings.smartDetail&&cameraOwner==="user";
    const camera=detail?smartView(selected?at(selected.frames,time):null,ballPoint):broadcast.view;
    const portraitDetail=detail||camera.width<ARENA.width*.9;
    if(lastOwner!==cameraOwner) {
        lastOwner=cameraOwner;
        if(viewCameraStatus)viewCameraStatus.textContent=cameraOwner==="user"?
            "Your camera is active. Detail follows the selected player and ball.":
            `${cameraOwner==="film"?"Director's Cut":cameraOwner==="goal"?"Goal highlight":"Pinned evidence"} owns the camera. Your Smart zoom choice resumes afterward.`;
        if(viewWide)viewWide.disabled=cameraOwner!=="user";
        if(viewDetail)viewDetail.disabled=cameraOwner!=="user";
    }
    engine.lastDrawTime = time; // Hit testing and following lenses use the actual replay camera, not a future live position.
    const following = popups?.floating?.id === "player-dialog";
    let lensPoint = null;
    map = viewportMap(width, height, canvas.width, canvas.height, camera);
    const size = `${Math.max(24, 4 * viewSettings.markerScale * map.field.scale)}px`;
    if (markerSize !== size) { playerLayer.style.setProperty("--player-size", size); markerSize = size; }
    const c = context; c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, canvas.width, canvas.height);
    c.setTransform(...backgroundTransform(wideMap, map)); c.drawImage(background, 0, 0);
    applyField(c, map);
    visibleLens = !film && !pinned && pitchLens && lensSequence===engine.clock.sequence &&
        pitchLens.window.cutoff<=clockTime+.000001 ? pitchLens : null;
    if(visibleLens) drawLens(c,visibleLens,engine.scenario.players);
    const lensTag=ui["pitch-lens-tag"];
    if(lensTag){
        lensTag.hidden=!visibleLens;
        const label=visibleLens?`${visibleLens.mode==="connections"?"HISTORICAL CONNECTIONS · hollow H = historical average":
            visibleLens.observedSeconds===0?"MOVEMENT · no elapsed player-time":`MOVEMENT · cell-time 0–${Math.max(...visibleLens.cells.map(c=>c.seconds)).toFixed(1)}s`} · ${visibleLens.window.start.toFixed(1)}–${visibleLens.window.cutoff.toFixed(1)}s`:"";
        if(lensTag.textContent!==label)lensTag.textContent=label;
    }
    if (pinned) drawEvidence(c, evidence.fact);
    if (filmFrame?.holding) {
        const points = engine.scenario.ball.filter(p => p.time >= filmFrame.scene.sourceStart && p.time <= clockTime);
        drawEvidence(c, { ball: [at(engine.scenario.ball, filmFrame.scene.sourceStart), ...points, at(engine.scenario.ball, clockTime)] });
    }
    const evidenceTag = ui["evidence-view-tag"];
    if (evidenceTag) {
        evidenceTag.hidden = !pinned;
        evidenceTag.textContent = pinned ? `PINNED EVIDENCE · ${evidence.fact.id} · ${evidence.fact.time.toFixed(1)}s · Paused historical view` : "";
    }
    if (selected) {
        c.beginPath();
        const points = selected.frames.filter(f => f.time < time).concat(at(selected.frames, time));
        points.forEach((p, i) => i ? c.lineTo(p.x, p.y) : c.moveTo(p.x, p.y));
        c.strokeStyle = "#d7b8ff"; c.lineWidth = .4; c.stroke();
    }
    drawnTargets=[];selectedGeometry=null;
    for (const p of engine.drawPlayers) {
        const point = at(p.frames, time);
        const geometry=playerView.geometry(p,point,ballPoint,engine.selectedId,viewSettings,map.field.scale,portraitDetail);
        if(p.id===engine.selectedId)selectedGeometry=geometry;
        if (following && p.id === lensPlayerId) lensPoint = point;
        playerView.draw(c,p,point,club(p.teamId).color,p.teamId===teams()[1].id,geometry,p.id===engine.selectedId,p.id===broadcast.goal?.scorerId);
        if (p.id === broadcast.goal?.scorerId && broadcast.phase === "reaction") {
            c.strokeStyle = "#f4f8ee"; c.lineWidth = .35;
            c.beginPath(); c.moveTo(point.x - 1, point.y - .5); c.lineTo(point.x - 2, point.y - 2);
            c.moveTo(point.x + 1, point.y - .5); c.lineTo(point.x + 2, point.y - 2); c.stroke();
            c.font = `600 ${Math.max(1.6, 16 / map.field.scale)}px Barlow, sans-serif`;
            c.textAlign="center";c.textBaseline="middle";
            c.fillStyle = "#fff"; c.fillText(broadcast.goal.scorer, point.x, point.y + 3.8);
        }
        const target = playerTargets.get(p.id);
        if (target) {
            const centre = fieldToViewport(map, point);
            const tw=Math.max(24,geometry.width*map.field.scale),th=Math.max(24,geometry.height*map.field.scale);
            const targetWidth=`${tw}px`,targetHeight=`${th}px`;
            if(target.button.style.width!==targetWidth)target.button.style.width=targetWidth;
            if(target.button.style.height!==targetHeight)target.button.style.height=targetHeight;
            target.button.style.borderRadius=geometry.style===2||geometry.style===3?"6px":"50%";
            target.button.style.transform = `translate(${centre.x}px, ${centre.y}px) translate(-50%, -50%)`;
            target.button.hidden = centre.x < 0 || centre.x > width || centre.y < 0 || centre.y > height;
            if(!target.button.hidden&&!film)drawnTargets.push({id:p.id,x:centre.x,y:centre.y,width:tw,height:th,
                round:geometry.style===0||geometry.style===1,photo:!!geometry.image,style:geometry.style,number:geometry.number,
                portraitPath:geometry.image?p.portrait.imagePath:null});
        }
    }
    const shot = (engine.scenario.goals ?? []).find(g => g.shotStart <= time && time <= g.time);
    if (shot) {
        const from = at(engine.scenario.ball, Math.max(shot.shotStart, time - .45)), to = at(engine.scenario.ball, time);
        c.strokeStyle = "#fff"; c.lineWidth = .25; c.beginPath(); c.moveTo(from.x, from.y); c.lineTo(to.x, to.y); c.stroke();
    }
    if (broadcast.contact) {
        const goal = broadcast.contact.goal, age = broadcast.contact.age, right = (goal.goalX ?? (goal.teamId===teams()[0].id?105:0)) > 52.5;
        drawNet(c, right, age, goal.goalY ?? 34);
        const settle = reducedMotion ? 1.5 : 1.8 * (1 - Math.exp(-age * 5));
        circle(c, (goal.goalX ?? (right ? 105 : 0)) + (right ? settle : -settle), goal.goalY ?? 34, .65, "#fff", "#14281c");
    } else if (!broadcast.deadBall || broadcast.replay) {
        c.lineWidth = .2; circle(c, ballPoint.x, ballPoint.y, .85, "#fff", "#000");
    }
    c.globalAlpha = 1;
    updateTransport(clockTime, broadcast);
    if (lensPoint && canvasBox) {
        const local = fieldToViewport(map, lensPoint);
        const scaleX = canvasBox.width / map.width, scaleY = canvasBox.height / map.height;
        const visible = local.x >= 0 && local.x <= width && local.y >= 0 && local.y <= height;
        popups.hidePlayer(!visible);
        if (visible) popups.follow({ x: canvasBox.left + local.x * scaleX, y: canvasBox.top + local.y * scaleY },
                canvasBox, Math.max(12, (selectedGeometry?Math.max(selectedGeometry.width,selectedGeometry.height)/2+.8:2.8) * map.field.scale * Math.max(scaleX, scaleY)) + 12);
    }
    playerLayer.hidden = !!film;
    const filmLabel = ui["film-position"], filmText = ui["film-insight"];
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
        button.setAttribute("aria-label", `Open ${p.name} player details, ${club(p.teamId).name}, shirt ${p.number}`);
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
        pitchLens = visibleLens = null; lensSequence = -1;
        drawnTargets=[];
        if(viewImageStatus)viewImageStatus.textContent="";
        playerView.prepare(engine.scenario);
        prepareBroadcast(engine.scenario);
        lastClockTime = 0;
        popups?.close("player-dialog", false); popups?.close("roster-dialog", false); popups?.close("analysis-dialog"); popups?.close("more-dialog"); lensPlayerId = null;
        buildPlayerTargets(); updateTransport(0); draw();
    }
}
function updateTransport(time, broadcast = broadcastFrame(engine.scenario, time, engine.clock.highlightsEnabled && !reducedMotion)) {
    const output = ui["replay-clock"];
    if (output) output.textContent = `${time.toFixed(1).padStart(4, "0")} / ${engine.scenario.duration.toFixed(1)} s`;
    if (range && !dragging) range.value = String(time);
    const caption = ui["commentary-caption"];
    if (caption) {
        const cue = engine.caption(time);
        caption.hidden = !cue;
        caption.setAttribute("data-goal", broadcast.goal && cue?.eventId === broadcast.goal.id ? "true" : "false");
        if (cue && caption.textContent !== cue.text) caption.textContent = cue.text;
        if (!cue && caption.textContent) caption.textContent = "";
    }
    const score = ui["match-score"];
    const value = `${broadcast.score[teams()[0].id]} : ${broadcast.score[teams()[1].id]}`;
    if (score && score.textContent !== value) score.textContent = value;
    const pace = ui["effective-speed"];
    if (pace) {
        const active = engine.clock.highlightsEnabled && engine.clock.windows.some(w => w.start <= time && time < w.end);
        const label = film ? `Match speed saved` : active ? `Goal highlight · ${engine.clock.effectiveRate(now())}x` : "";
        pace.hidden = !label;
        if (pace.textContent !== label) pace.textContent = label;
    }
    const banner = ui["goal-banner"];
    if (banner) {
        banner.hidden = !broadcast.goal;
        const title = ui["goal-title"], detail = ui["goal-detail"];
        if (broadcast.goal) {
            const heading = broadcast.goal.scorer;
            const facts = `${broadcast.goal.time.toFixed(1)}s · Synthetic match`;
            if (title.textContent !== heading) title.textContent = heading;
            if (detail.textContent !== facts) detail.textContent = facts;
            if (banner.getAttribute("data-team") !== broadcast.goal.teamId) banner.setAttribute("data-team", broadcast.goal.teamId);
            if (banner.getAttribute("data-phase") !== broadcast.phase) banner.setAttribute("data-phase", broadcast.phase);
            banner.style.setProperty("--goal-fade", String(broadcast.fade));
            const crest = ui["goal-crest"], team = ui["goal-team"], goalScore = ui["goal-score"];
            banner.style.setProperty("--goal-accent", club(broadcast.goal.teamId).color);
            const image = club(broadcast.goal.teamId).crest;
            if (crest.getAttribute("src") !== image) { crest.setAttribute("src", image); crest.alt = broadcast.goal.teamName; }
            if (team.textContent !== broadcast.goal.teamName) team.textContent = broadcast.goal.teamName;
            if (goalScore.textContent !== value) goalScore.textContent = value;
        }
    }
    const replayTag = ui["goal-replay-tag"];
    if (replayTag) replayTag.hidden = !broadcast.replay;
}
export function setCaptionsEnabled(enabled) { engine?.setCaptionsEnabled(enabled); draw(); }
export function setClock(json,lensJson) {
    if (engine?.update(JSON.parse(json), now())) {
        if(arguments.length>1)applyLens(lensJson,engine.clock.sequence);
        updateTransport(engine.clock.time(now()));
        draw();
    }
}
export function diagnostics() { return engine ? { ...engine.diagnostics, viewport: map, time: lastClockTime, renderTime: engine.lastDrawTime, lens: visibleLens,
    viewSettings,cameraOwner,drawnTargets:drawnTargets.map(t=>({...t})),imagesConstructed:playerView.constructed,imageFailures:playerView.failures } : null; }
export function setViewSettings(json) {
    const value=validateView(JSON.parse(json));
    viewSettings=value;draw();
}
export function setLens(json,sequence) {
    if(applyLens(json,sequence))draw();
}
function applyLens(json,sequence) {
    if(!engine?.scenario||sequence!==engine.clock.sequence||sequence<lensSequence)return;
    if(!json){pitchLens=visibleLens=null;lensSequence=sequence;return true;}
    const view=JSON.parse(json);
    if(view.window?.revision!==engine.scenario.revision)return;
    validateLens(view,engine.scenario);
    if(Math.abs(view.window.cutoff-engine.clock.position)>.000001)return;
    pitchLens=view;lensSequence=sequence;return true;
}
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
    popups?.close("player-dialog", false); popups?.close("roster-dialog", false); popups?.close("analysis-dialog"); popups?.close("more-dialog");
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
    popups?.close("analysis-dialog");
    for(const detail of document.querySelectorAll(".lens-data[open]"))detail.open=false;
    draw();
}
export function dispose() {
    film = null;
    evidence = null;
    fontSet?.removeEventListener?.("loadingdone",fontLoadedHandler);fontSet=fontLoadedHandler=null;
    playerView?.dispose();playerView=null;viewSettings=DEFAULT_VIEW;drawnTargets=[];selectedGeometry=null;
    ui={};
    viewCameraStatus=viewImageStatus=viewWide=viewDetail=null;lastOwner=null;cameraOwner="user";
    pitchLens = visibleLens = null; lensSequence = -1;
    const lensTag=document.getElementById("pitch-lens-tag");
    if(lensTag){lensTag.hidden=true;lensTag.textContent="";}
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
