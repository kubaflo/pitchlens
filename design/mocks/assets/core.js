(function (root, factory) {
    const api = factory();
    if (typeof module === "object" && module.exports) module.exports = api;
    root.PitchLensMockCore = api;
}(globalThis, function () {
    "use strict";
    function at(frames, time) {
        if (time <= frames[0].time) return { x: frames[0].x, y: frames[0].y };
        for (let i = 1; i < frames.length; i++) {
            const a = frames[i - 1], b = frames[i];
            if (time <= b.time) {
                const f = (time - a.time) / (b.time - a.time);
                return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f };
            }
        }
        return { x: frames[frames.length - 1].x, y: frames[frames.length - 1].y };
    }
    function prepare(data) {
        const model = { duration: data.scenario.duration, players: [], events: data.scenario.events, story: data.scenario.story };
        model.players = data.scenario.players.map(p => ({ ...p, ...data.roster.find(r => r.id === p.id) }));
        model.player = id => {
            const player = model.players.find(p => p.id === id);
            if (!player) throw new Error("Unknown scripted player.");
            return player;
        };
        model.possession = [{ time: 0, id: data.scenario.initialOwnerId }];
        for (const event of model.events) {
            if (event.kind === "pass" || event.kind === "shot")
                model.possession.push({ time: event.startTime, id: null }, { time: event.time, id: event.receiverId });
            if (event.kind === "kickoff") model.possession.push({ time: event.time, id: event.actorId });
        }
        return model;
    }
    function owner(model, time) {
        return model.possession.filter(p => p.time <= time).at(-1).id;
    }
    function ball(model, time) {
        const flight = model.events.find(e => ["pass", "shot"].includes(e.kind) && e.startTime <= time && time <= e.time);
        if (flight) {
            const from = at(model.player(flight.actorId).frames, flight.startTime);
            const to = flight.receiverId ? at(model.player(flight.receiverId).frames, flight.time)
                : { x: model.player(flight.actorId).team === "arsenal" ? 105 : 0, y: 34 };
            const f = (time - flight.startTime) / (flight.time - flight.startTime);
            return { x: from.x + (to.x - from.x) * f, y: from.y + (to.y - from.y) * f };
        }
        const id = owner(model, time);
        if (id) return at(model.player(id).frames, time);
        const goal = model.events.filter(e => e.kind === "goal" && e.time <= time).at(-1);
        const restart = model.events.find(e => e.kind === "kickoff" && e.time > goal.time);
        const from = model.player(goal.actorId).team === "arsenal" ? 105 : 0;
        const f = restart ? Math.min(1, (time - goal.time) / (restart.time - goal.time)) : 0;
        return { x: from + (52.5 - from) * f, y: 34 };
    }
    function measure(frames, time) {
        let distance = 0, maxSpeed = 0;
        for (let i = 1; i < frames.length; i++) {
            const a = frames[i - 1], b = frames[i], dt = Math.max(0, Math.min(time - a.time, b.time - a.time));
            if (!dt) break;
            const speed = Math.hypot(b.x - a.x, b.y - a.y) / (b.time - a.time);
            distance += speed * dt; maxSpeed = Math.max(maxSpeed, speed);
        }
        return { distance, maxSpeed };
    }
    function stats(model, id, time) {
        const events = model.events.filter(e => e.actorId === id && e.time <= time);
        const passes = events.filter(e => e.kind === "pass");
        return {
            passes: passes.length, completed: passes.filter(e => e.completed).length,
            accuracy: passes.length ? passes.filter(e => e.completed).length / passes.length : null,
            goals: events.filter(e => e.kind === "goal").length,
            shots: events.filter(e => e.kind === "shot").length,
            ...measure(model.player(id).frames, time)
        };
    }
    function score(model, time) {
        const result = { arsenal: 0, "manchester-city": 0 };
        model.events.filter(e => e.kind === "goal" && e.time <= time).forEach(e => result[model.player(e.actorId).team]++);
        return result;
    }
    class Clock {
        constructor(duration, position = 0) { this.duration = duration; this.position = position; this.anchor = 0; this.playing = false; this.rate = 1; }
        time(now) {
            const time = Math.min(this.duration, this.position + (this.playing ? Math.max(0, now - this.anchor) * this.rate / 1000 : 0));
            if (time >= this.duration) { this.position = time; this.playing = false; }
            return time;
        }
        pause(now) { this.position = this.time(now); this.playing = false; }
        play(now) { if (this.position >= this.duration) this.position = 0; if (!this.playing) { this.anchor = now; this.playing = true; } }
        seek(time, now) {
            if (!Number.isFinite(time)) throw new RangeError("Invalid mock time.");
            this.position = Math.max(0, Math.min(this.duration, time)); this.anchor = now;
            if (this.position === this.duration) this.playing = false;
        }
        speed(rate, now) {
            if (![.75, 1, 1.25].includes(rate)) throw new RangeError("Unknown preview pace.");
            this.position = this.time(now); this.anchor = now; this.rate = rate;
        }
    }
    return { prepare, at, owner, ball, measure, stats, score, Clock };
}));
