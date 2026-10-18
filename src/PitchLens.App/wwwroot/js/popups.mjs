export function popupPosition(point, size, viewport) {
    const maxLeft = Math.max(16, viewport.width - size.width - 16);
    const maxTop = Math.max(16, viewport.height - size.height - 16);
    const preferred = point.x + 24 + size.width <= viewport.width - 16
        ? point.x + 24 : point.x - size.width - 24;
    return { left: Math.max(16, Math.min(preferred, maxLeft)), top: Math.max(16, Math.min(point.y, maxTop)) };
}

export class PopupHost {
    constructor(doc, win) {
        this.doc = doc; this.win = win; this.handlers = new Map(); this.openers = new Map(); this.adjacent = null;
        this.resize = () => { if (this.adjacent?.open) this.position(this.adjacent, this.point); };
        this.click = event => {
            const button = event.target.closest?.("[data-close-popup]");
            if (button) this.close(button.closest("dialog")?.id);
        };
        doc.addEventListener("click", this.click);
        win.addEventListener("resize", this.resize);
    }
    position(dialog, point) {
        const box = dialog.getBoundingClientRect();
        const position = popupPosition(point, box, { width: this.win.innerWidth, height: this.win.innerHeight });
        dialog.style.left = `${position.left}px`; dialog.style.top = `${position.top}px`;
    }
    open(id, point = null) {
        const dialog = this.doc.getElementById(id);
        if (!dialog || dialog.tagName !== "DIALOG") throw new Error("Requested popup is missing.");
        if (!this.handlers.has(dialog)) {
            const close = () => {
                if (dialog.open || this.doc.querySelector("dialog[open]")) return;
                const opener = this.openers.get(dialog);
                if (opener?.isConnected) opener.focus({ preventScroll: true });
                if (this.adjacent === dialog) this.adjacent = null;
            };
            const backdrop = event => {
                if (event.target !== dialog) return;
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
        for (const open of this.doc.querySelectorAll("dialog[open]")) if (open !== dialog) open.close();
        this.openers.set(dialog, opener);
        if (!dialog.open) dialog.showModal();
        if (point) {
            this.adjacent = dialog; this.point = point; this.position(dialog, point);
        }
    }
    close(id) {
        const dialog = id ? this.doc.getElementById(id) : null;
        if (dialog?.open) dialog.close();
    }
    dispose() {
        this.doc.removeEventListener("click", this.click); this.win.removeEventListener("resize", this.resize);
        for (const [dialog, handlers] of this.handlers) {
            dialog.removeEventListener("close", handlers.close); dialog.removeEventListener("click", handlers.backdrop);
            if (dialog.open) dialog.close();
        }
        this.handlers.clear(); this.openers.clear(); this.adjacent = null; this.point = null;
    }
}
