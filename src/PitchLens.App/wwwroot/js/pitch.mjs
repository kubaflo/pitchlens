import { ReplayEngine, at, fit } from "./replay.mjs";

let engine, canvas, context, background, observer, reference, frameId = null;
let pointerHandler, visibilityHandler, rangeDown, rangeUp, range, width = 0, height = 0, dragging = false;
const now = () => performance.now();

export function initialize(element, dotnet) {
    dispose();
    canvas = element; reference = dotnet; context = canvas.getContext("2d");
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
    observer = new ResizeObserver(resize); observer.observe(canvas);
    window.addEventListener("resize", resize);
    pointerHandler = event => {
        const box = canvas.getBoundingClientRect();
        engine?.select(event.clientX - box.left, event.clientY - box.top, box.width, box.height, now());
    };
    canvas.addEventListener("pointerdown", pointerHandler);
    visibilityHandler = () => { if (document.hidden) { engine?.hide(now()); stop(); } else draw(); };
    document.addEventListener("visibilitychange", visibilityHandler);
    range = document.getElementById("timeline");
    rangeDown = () => { dragging = true; };
    rangeUp = () => { dragging = false; };
    range?.addEventListener("pointerdown", rangeDown);
    window.addEventListener("pointerup", rangeUp);
    resize();
}
function stop() { if (frameId !== null) cancelAnimationFrame(frameId); frameId = null; }
function resize() {
    if (!canvas) return;
    const box = canvas.getBoundingClientRect();
    width = box.width; height = box.height;
    if (width <= 0 || height <= 0) { stop(); return; }
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
    background.width = canvas.width; background.height = canvas.height;
    const c = background.getContext("2d"), t = fit(width, height);
    c.setTransform(dpr, 0, 0, dpr, 0, 0);
    c.fillStyle = "#242333"; c.fillRect(0, 0, width, height);
    c.translate(t.left, t.top); c.scale(t.scale, t.scale);
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
    const time = engine.frame(now()), t = fit(width, height), dpr = window.devicePixelRatio || 1;
    const c = context; c.setTransform(1, 0, 0, 1, 0, 0); c.clearRect(0, 0, canvas.width, canvas.height); c.drawImage(background, 0, 0);
    c.setTransform(dpr, 0, 0, dpr, 0, 0); c.translate(t.left, t.top); c.scale(t.scale, t.scale);
    const selected = engine.scenario.players.find(p => p.id === engine.selectedId);
    if (selected) {
        c.beginPath();
        const points = selected.frames.filter(f => f.time < time).concat(at(selected.frames, time));
        points.forEach((p, i) => i ? c.lineTo(p.x, p.y) : c.moveTo(p.x, p.y));
        c.strokeStyle = "#d8b9f0"; c.lineWidth = .4; c.stroke();
    }
    for (const p of engine.drawPlayers) {
        const point = at(p.frames, time);
        if (p.id === engine.selectedId) { c.lineWidth = .6; circle(c, point.x, point.y, 2.8, null, "#fff"); }
        circle(c, point.x + .15, point.y + .3, 2.2, "#23382b", null);
        c.lineWidth = .18; circle(c, point.x, point.y, 2, p.teamId === "arsenal" ? "#00ff87" : "#ff2882", "#f4f4ef");
        c.fillStyle = "#37003c"; c.font = "bold 2.5px -apple-system, sans-serif"; c.textAlign = "center"; c.textBaseline = "middle";
        c.fillText(String(p.number), point.x, point.y);
    }
    const ball = at(engine.scenario.ball, time); c.lineWidth = .2; circle(c, ball.x, ball.y, .85, "#fff", "#000");
    updateTransport(time);
    if (engine.clock.playing && time < engine.clock.duration) frameId = requestAnimationFrame(draw);
}
export function loadScenario(json) { if (engine?.load(JSON.parse(json), now())) draw(); }
function updateTransport(time) {
    const output = document.getElementById("replay-clock");
    if (output) output.textContent = `${time.toFixed(1).padStart(4, "0")} / ${engine.scenario.duration.toFixed(1)} s`;
    if (range && !dragging) range.value = String(time);
}
export function setClock(json) {
    if (engine?.update(JSON.parse(json), now())) {
        updateTransport(engine.clock.time(now()));
        draw();
    }
}
export function diagnostics() { return engine ? { ...engine.diagnostics } : null; }
export function dispose() {
    stop(); observer?.disconnect();
    window.removeEventListener("resize", resize);
    if (canvas && pointerHandler) canvas.removeEventListener("pointerdown", pointerHandler);
    if (visibilityHandler) document.removeEventListener("visibilitychange", visibilityHandler);
    if (range && rangeDown) range.removeEventListener("pointerdown", rangeDown);
    if (rangeUp) window.removeEventListener("pointerup", rangeUp);
    engine?.dispose();
    engine = canvas = context = background = observer = reference = range = null;
    pointerHandler = visibilityHandler = rangeDown = rangeUp = null;
}
