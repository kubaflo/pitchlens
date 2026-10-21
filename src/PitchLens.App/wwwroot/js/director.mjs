export function filmDuration(plan) {
    return plan.scenes.reduce((total, s) => total + (s.sourceEnd - s.sourceStart) / s.rate + s.holdSeconds, 0);
}
export function filmAt(plan, seconds) {
    const duration = filmDuration(plan), filmTime = Math.max(0, Math.min(duration, seconds));
    let offset = filmTime;
    for (let i = 0; i < plan.scenes.length; i++) {
        const scene = plan.scenes[i], footage = (scene.sourceEnd - scene.sourceStart) / scene.rate;
        if (offset < footage + scene.holdSeconds || i === plan.scenes.length - 1)
            return { filmTime, sourceTime: Math.min(scene.sourceEnd, scene.sourceStart + Math.min(offset, footage) * scene.rate),
                scene, sceneIndex: i, holding: offset >= footage, duration };
        offset -= footage + scene.holdSeconds;
    }
    throw new Error("Film has no scenes.");
}
export class FilmProjection {
    constructor(plan) { this.plan = plan; this.position = 0; this.anchor = 0; this.playing = false; this.sequence = -1; }
    apply(position, playing, now) {
        if (!Number.isFinite(position) || position < 0 || position > filmDuration(this.plan)) throw new Error("Invalid film anchor.");
        this.position = position; this.playing = playing; this.anchor = now;
    }
    frame(now) { return filmAt(this.plan, this.position + (this.playing ? Math.max(0, now - this.anchor) / 1000 : 0)); }
}
