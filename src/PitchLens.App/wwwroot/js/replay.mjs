export function at(frames, time) {
    if (time <= frames[0].time) return { x: frames[0].x, y: frames[0].y };
    let low = 1, high = frames.length - 1;
    while (low < high) { const mid = (low + high) >>> 1; if (frames[mid].time < time) low = mid + 1; else high = mid; }
    const b = frames[low], a = frames[low - 1];
    if (time <= b.time) {
        const f = (time - a.time) / (b.time - a.time);
        return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f };
    }
    return { x: frames.at(-1).x, y: frames.at(-1).y };
}
export function fit(width, height) {
    if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) throw new RangeError("Invalid viewport.");
    const scale = Math.min(width / 105, height / 68);
    return { scale, left: (width - 105 * scale) / 2, top: (height - 68 * scale) / 2 };
}
export function hit(players, time, transform, x, y) {
    const { left, top, scale } = transform;
    if (x < left || y < top || x > left + 105 * scale || y > top + 68 * scale) return null;
    const px = (x - left) / scale, py = (y - top) / scale;
    let winner = null, distance = Infinity;
    for (const player of players) {
        const p = at(player.frames, time), d = Math.hypot(px - p.x, py - p.y);
        if (d <= 2 && (d < distance || (d === distance && player.id < winner))) {
            winner = player.id; distance = d;
        }
    }
    return winner;
}
export function keyboardAction(event) {
    const target = event.target;
    if (event.defaultPrevented || event.repeat || event.metaKey || event.ctrlKey || event.altKey || event.shiftKey ||
        target?.isContentEditable || ["INPUT", "TEXTAREA", "SELECT", "BUTTON", "A", "SUMMARY"].includes(target?.tagName?.toUpperCase()) ||
        target?.closest?.("input,textarea,select,button,a,summary,[contenteditable='true'],[role='textbox']")) return null;
    return event.key === " " ? "toggle" : event.key === "ArrowLeft" ? "back" : event.key === "ArrowRight" ? "forward" : null;
}
export function goalState(scenario, time) {
    const score = { arsenal: 0, "manchester-city": 0 };
    let celebration = null;
    for (const goal of scenario.goals || []) {
        if (goal.time > time) continue;
        score[goal.teamId]++;
        if (time < goal.time + 8) celebration = goal;
    }
    return { score, celebration };
}
export function captionAt(cues, time, enabled = true) {
    if (!Number.isFinite(time)) throw new RangeError("Invalid subtitle time.");
    if (!enabled || !cues?.length) return null;
    let low = 0, high = cues.length;
    while (low < high) { const mid = (low + high) >>> 1; if (cues[mid].start <= time) low = mid + 1; else high = mid; }
    return low > 0 && time < cues[low - 1].end ? cues[low - 1] : null;
}
export function advancePlayback(position, wallSeconds, duration, rate, windows = [], enabled = false) {
    if (!enabled || !windows.length) return Math.min(duration, position + wallSeconds * rate);
    while (position < duration && wallSeconds > 0) {
        const active = windows.find(w => w.start <= position && position < w.end);
        const boundary = active?.end ?? windows.find(w => w.start > position)?.start ?? duration;
        const effective = active ? 1 : rate, required = (boundary - position) / effective;
        if (wallSeconds <= required) return Math.min(duration, position + wallSeconds * effective);
        position = boundary; wallSeconds -= required;
    }
    return position;
}
export class ProjectionClock {
    revision = 0; sequence = -1; position = 0; anchor = 0; playing = false; duration = 0; rate = 3;
    windows = []; highlightsEnabled = false;
    reset(revision, duration, now, windows = []) {
        this.revision = revision; this.duration = duration; this.sequence = -1;
        this.position = 0; this.anchor = now; this.playing = false; this.rate = 3;
        this.windows = [];
        for (const window of [...windows].sort((a, b) => a.start - b.start)) {
            if (!Number.isFinite(window.start) || !Number.isFinite(window.end) || window.start < 0 || window.end <= window.start || window.end > duration)
                throw new RangeError("Invalid highlight rate window.");
            const previous = this.windows.at(-1);
            if (previous && window.start <= previous.end) previous.end = Math.max(previous.end, window.end);
            else this.windows.push({ ...window });
        }
        this.highlightsEnabled = false;
    }
    apply(packet, now) {
        if (packet.revision !== this.revision || packet.sequence <= this.sequence) return false;
        if (!Number.isFinite(packet.time) || packet.time < 0 || packet.time > this.duration) throw new RangeError("Invalid clock packet.");
        const rate = packet.rate ?? 3;
        if (![1, 2, 3, 5].includes(rate)) throw new RangeError("Invalid replay rate.");
        this.sequence = packet.sequence; this.position = packet.time;
        this.anchor = now; this.playing = packet.playing; this.rate = rate;
        this.highlightsEnabled = packet.highlightsEnabled ?? false;
        return true;
    }
    time(now) {
        return this.playing ? advancePlayback(this.position, Math.max(0, now - this.anchor) / 1000,
            this.duration, this.rate, this.windows, this.highlightsEnabled) : this.position;
    }
    effectiveRate(now) {
        const time = this.time(now);
        return this.highlightsEnabled && this.windows.some(w => w.start <= time && time < w.end) ? 1 : this.rate;
    }
    freeze(now) {
        this.position = this.time(now); this.anchor = now; this.playing = false;
        return this.position;
    }
}
export class ReplayEngine {
    clock = new ProjectionClock(); scenario = null; selectedId = null; disposed = false;
    lastDrawTime = 0;
    captionsEnabled = true;
    diagnostics = { frames: 0, notifications: 0, scenarioLoads: 0, clockPackets: 0 };
    constructor(notify = () => {}) { this.notify = notify; }
    load(scenario, now) {
        if (this.disposed || (this.scenario && scenario.revision <= this.scenario.revision)) return false;
        this.scenario = scenario;
        this.lastDrawTime = 0;
        this.drawPlayers = [...scenario.players].sort((a, b) => a.id < b.id ? 1 : -1);
        this.clock.reset(scenario.revision, scenario.duration, now, scenario.focusWindows ?? []);
        this.diagnostics.scenarioLoads++; return true;
    }
    update(packet, now) {
        if (this.disposed || !this.clock.apply(packet, now)) return false;
        this.selectedId = packet.selectedId; this.diagnostics.clockPackets++; return true;
    }
    frame(now) {
        if (this.disposed || !this.scenario) return null;
        this.diagnostics.frames++;
        this.lastDrawTime = this.clock.time(now);
        return this.lastDrawTime;
    }
    caption(time = this.lastDrawTime) { return this.disposed ? null : captionAt(this.scenario?.captions, time, this.captionsEnabled); }
    setCaptionsEnabled(enabled) { if (!this.disposed) this.captionsEnabled = enabled; }
    select(x, y, width, height, now, transform = null) {
        if (this.disposed || !this.scenario) return;
        const id = hit(this.scenario.players, this.lastDrawTime, transform ?? fit(width, height), x, y);
        if (id) this.selectPlayer(id);
    }
    selectPlayer(id) {
        if (this.disposed || !this.scenario) return;
        if (!this.scenario.players.some(p => p.id === id)) throw new RangeError("Unknown pitch player.");
        this.diagnostics.notifications++; this.notify("SelectFromPitch", id);
    }
    hide(now) {
        if (this.disposed || !this.scenario) return;
        const time = this.clock.freeze(now);
        this.diagnostics.notifications++; this.notify("PageHidden", time, this.clock.revision);
    }
    keyboard(command) {
        if (this.disposed) return;
        this.diagnostics.notifications++; this.notify("KeyboardCommand", command);
    }
    dispose() { this.disposed = true; this.clock.playing = false; this.scenario = null; this.notify = () => {}; }
}
