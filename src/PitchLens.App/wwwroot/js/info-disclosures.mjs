const toggles = "[data-info-toggle]";

export function infoPosition(anchor, size, viewport) {
    const margin = 12, gap = 8;
    const left = Math.max(margin, Math.min(anchor.left + (anchor.width - size.width) / 2, viewport.width - size.width - margin));
    const below = anchor.bottom + gap, above = anchor.top - size.height - gap;
    const preferredTop = below + size.height <= viewport.height - margin ? below :
        above >= margin ? above : Math.max(margin, viewport.height - size.height - margin);
    const top = Math.max(margin, Math.min(preferredTop, viewport.height - size.height - margin));
    return { left, top };
}

function positionInfo(button, content) {
    const win = button.ownerDocument.defaultView;
    const position = infoPosition(button.getBoundingClientRect(), content.getBoundingClientRect(),
        { width: win.innerWidth, height: win.innerHeight });
    content.style.left = `${position.left}px`;
    content.style.top = `${position.top}px`;
}

export function closeInfo(scope, restoreFocus = false) {
    for (const button of scope.querySelectorAll(`${toggles}[aria-expanded='true']`)) {
        const content = button.ownerDocument.getElementById(button.getAttribute("aria-controls"));
        if (content) {
            if (content.matches(":popover-open")) content.hidePopover();
            content.hidden = true;
        }
        button.setAttribute("aria-expanded", "false");
        if (restoreFocus && button.isConnected) button.focus({ preventScroll: true });
    }
}

// Native top-layer popovers overlay the parent; they never enter PopupHost's dialog stack.
export function installInfoDisclosures(doc) {
    const win = doc.defaultView;
    const click = event => {
        const close = event.target.closest?.("[data-info-close]");
        if (close) { closeInfo(close.closest(".info-disclosure"), true); return; }
        const button = event.target.closest?.(toggles);
        if (!button) {
            if (!event.target.closest?.(".info-disclosure")) closeInfo(doc);
            return;
        }
        const content = doc.getElementById(button.getAttribute("aria-controls"));
        if (!content) throw new Error("Contextual explanation is missing.");
        if (typeof content.showPopover !== "function") throw new Error("Contextual explanations require native popover support.");
        const opening = button.getAttribute("aria-expanded") !== "true";
        closeInfo(button.closest("dialog, .setup-card, .matchday") ?? doc);
        if (opening) {
            content.hidden = false;
            content.showPopover();
            button.setAttribute("aria-expanded", "true");
            positionInfo(button, content);
            button.focus({ preventScroll: true });
        }
    };
    const keydown = event => {
        if (event.key !== "Escape") return;
        const scope = event.target.closest?.("dialog, .setup-card, .matchday") ?? doc;
        if (!scope.querySelector(`${toggles}[aria-expanded='true']`)) return;
        event.preventDefault();
        event.stopPropagation();
        closeInfo(scope, true);
    };
    const close = event => { if (event.target.tagName === "DIALOG") closeInfo(event.target); };
    const scroll = event => { if (!event.target.closest?.(".info-content")) closeInfo(doc); };
    const reposition = () => {
        for (const button of doc.querySelectorAll(`${toggles}[aria-expanded='true']`)) {
            const content = doc.getElementById(button.getAttribute("aria-controls"));
            if (content?.matches(":popover-open")) positionInfo(button, content);
        }
    };
    doc.addEventListener("click", click);
    doc.addEventListener("keydown", keydown, true);
    doc.addEventListener("close", close, true);
    doc.addEventListener("scroll", scroll, true);
    win?.addEventListener("resize", reposition);
    doc.fonts?.addEventListener("loadingdone", reposition);
    return () => {
        closeInfo(doc);
        doc.removeEventListener("click", click);
        doc.removeEventListener("keydown", keydown, true);
        doc.removeEventListener("close", close, true);
        doc.removeEventListener("scroll", scroll, true);
        win?.removeEventListener("resize", reposition);
        doc.fonts?.removeEventListener("loadingdone", reposition);
    };
}
