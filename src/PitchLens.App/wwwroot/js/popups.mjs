export function popupPosition(point, size, viewport, bounds = null, gap = 24) {
    const screen = { left: 16, top: 16, right: viewport.width - 16, bottom: viewport.height - 16 };
    const area = bounds ? {
        left: Math.max(screen.left, bounds.left + 8), top: Math.max(screen.top, bounds.top + 8),
        right: Math.min(screen.right, bounds.right - 8), bottom: Math.min(screen.bottom, bounds.bottom - 8)
    } : { ...screen };
    if (area.right - area.left < size.width) { area.left = screen.left; area.right = screen.right; }
    if (area.bottom - area.top < size.height) { area.top = screen.top; area.bottom = screen.bottom; }
    const candidates = [
        { left: point.x + gap, top: point.y - Math.min(60, size.height / 4) },
        { left: point.x - size.width - gap, top: point.y - Math.min(60, size.height / 4) },
        { left: point.x - size.width / 2, top: point.y + gap },
        { left: point.x - size.width / 2, top: point.y - size.height - gap }
    ];
    const best = candidates.map((p, index) => {
        const left = Math.max(area.left, Math.min(p.left, area.right - size.width));
        const top = Math.max(area.top, Math.min(p.top, area.bottom - size.height));
        const radius = Math.max(0, gap - 12);
        const covers = point.x + radius > left && point.x - radius < left + size.width &&
            point.y + radius > top && point.y - radius < top + size.height;
        return { left, top, cost: (covers ? 1e6 : 0) + Math.abs(left - p.left) + Math.abs(top - p.top) + index / 10 };
    }).sort((a, b) => a.cost - b.cost)[0];
    return { left: best.left, top: best.top };
}

const isPlayerFlow = id => id === "player-dialog" || id === "roster-dialog";
const isNonmodal = id => isPlayerFlow(id) || id === "analysis-dialog" || id === "more-dialog";

export class PopupHost {
    constructor(doc, win) {
        this.doc = doc; this.win = win; this.handlers = new Map(); this.openers = new Map(); this.restoreOnClose = new Map();
        this.floating = null; this.size = null; this.anchor = null; this.bounds = null; this.gap = 24;
        this.resize = () => { if (this.floating?.open) { this.measure(); this.position(); } };
        this.click = event => {
            const button = event.target.closest?.("[data-close-popup]");
            if (button) { this.close(button.closest("dialog")?.id); return; }
            if (this.floating?.open && !this.floating.contains(event.target)) {
                event.target.closest?.("button, input, select, a, [tabindex]")?.focus({ preventScroll: true });
                if (this.floating.id === "analysis-dialog" || this.floating.id === "more-dialog" ?
                    !event.target.closest?.(this.floating.id === "analysis-dialog" ? "#analysis-button" : "#more-button") :
                    !event.target.closest?.(".player-target, #players-button, #roster, .transport"))
                    this.close(this.floating.id, false);
            }
        };
        this.keydown = event => {
            if (event.key === "Escape" && this.floating?.open) {
                event.preventDefault(); this.close(this.floating.id);
            }
        };
        const Observer = win.ResizeObserver ?? globalThis.ResizeObserver;
        this.observer = Observer ? new Observer(() => { if (this.floating?.open) { this.measure(); this.position(); } }) : null;
        doc.addEventListener("click", this.click);
        doc.addEventListener("keydown", this.keydown);
        win.addEventListener("resize", this.resize);
    }
    measure() {
        const box = this.floating.getBoundingClientRect();
        this.size = { width: box.width, height: box.height };
    }
    position() {
        if (this.doc.hidden || this.floating?.id !== "player-dialog" || !this.floating.open || !this.anchor || !this.size) return;
        const p = popupPosition(this.anchor, this.size, { width: this.win.innerWidth, height: this.win.innerHeight }, this.bounds, this.gap);
        this.floating.style.transform = `translate(${p.left}px, ${p.top}px)`;
    }
    follow(point, bounds, gap) {
        if (this.floating?.id !== "player-dialog" || !this.floating.open) return;
        this.anchor = point; this.bounds = bounds; this.gap = gap;
        const height = `${Math.max(160, Math.min(this.win.innerHeight - 32, bounds.height - 16))}px`;
        if (this.floating.style.getPropertyValue("--player-lens-height") !== height) {
            this.floating.style.setProperty("--player-lens-height", height);
            this.measure();
        }
        this.position();
    }
    hidePlayer(hidden) {
        const player = this.doc.getElementById("player-dialog");
        if (player?.open) player.style.visibility = hidden ? "hidden" : "";
    }
    open(id) {
        const dialog = this.doc.getElementById(id);
        if (!dialog || dialog.tagName !== "DIALOG") throw new Error("Requested popup is missing.");
        if (!this.handlers.has(dialog)) {
            const close = () => {
                if (dialog.open) return;
                const restoreFocus = this.restoreOnClose.get(dialog) !== false;
                this.restoreOnClose.delete(dialog);
                if (this.floating === dialog) { this.observer?.disconnect(); this.floating = null; this.anchor = null; }
                if (this.doc.querySelector("dialog[open]") || !restoreFocus) return;
                const opener = this.openers.get(dialog);
                const target = opener?.isConnected && !opener.hidden && !opener.disabled ? opener : this.doc.getElementById("pitch");
                target?.focus({ preventScroll: true });
            };
            const backdrop = event => {
                if (isNonmodal(dialog.id) || event.target !== dialog) return;
                const box = dialog.getBoundingClientRect();
                if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom)
                    dialog.close();
            };
            dialog.addEventListener("close", close); dialog.addEventListener("click", backdrop);
            this.handlers.set(dialog, { close, backdrop });
        }
        let opener = this.doc.activeElement;
        const parent = opener?.closest?.("dialog");
        if (parent) opener = this.openers.get(parent);
        for (const open of this.doc.querySelectorAll("dialog[open]")) if (open !== dialog) this.close(open.id, false);
        if (!dialog.open || !parent) this.openers.set(dialog, opener);
        this.restoreOnClose.delete(dialog);
        const floating = isNonmodal(id);
        dialog.setAttribute("aria-modal", floating ? "false" : "true");
        if (!dialog.open) { if (floating) dialog.show(); else dialog.showModal(); }
        if (floating) {
            this.floating = dialog;
            if (id !== "player-dialog") { this.anchor = null; this.bounds = null; }
            this.measure();
            this.observer?.disconnect(); this.observer?.observe(dialog);
        }
    }
    close(id, restoreFocus = true) {
        const dialog = id ? this.doc.getElementById(id) : null;
        if (dialog?.open) { this.restoreOnClose.set(dialog, restoreFocus); dialog.close(); }
    }
    dispose() {
        this.doc.removeEventListener("click", this.click); this.doc.removeEventListener("keydown", this.keydown);
        this.win.removeEventListener("resize", this.resize); this.observer?.disconnect();
        for (const [dialog, handlers] of this.handlers) {
            dialog.removeEventListener("close", handlers.close); dialog.removeEventListener("click", handlers.backdrop);
            if (dialog.open) dialog.close();
        }
        this.handlers.clear(); this.openers.clear(); this.restoreOnClose.clear(); this.floating = this.size = this.anchor = this.bounds = null;
    }
}
