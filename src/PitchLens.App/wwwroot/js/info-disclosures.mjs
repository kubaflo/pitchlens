const toggles = "[data-info-toggle]";

export function closeInfo(scope, restoreFocus = false) {
    for (const button of scope.querySelectorAll(`${toggles}[aria-expanded='true']`)) {
        const content = button.ownerDocument.getElementById(button.getAttribute("aria-controls"));
        if (content) content.hidden = true;
        button.setAttribute("aria-expanded", "false");
        if (restoreFocus && button.isConnected) button.focus({ preventScroll: true });
    }
}

// Inline help never participates in PopupHost's dialog stack or replay commands.
export function installInfoDisclosures(doc) {
    const click = event => {
        const button = event.target.closest?.(toggles);
        if (!button) {
            if (!event.target.closest?.(".info-disclosure")) closeInfo(doc);
            return;
        }
        const content = doc.getElementById(button.getAttribute("aria-controls"));
        if (!content) throw new Error("Contextual explanation is missing.");
        const opening = button.getAttribute("aria-expanded") !== "true";
        closeInfo(button.closest("dialog, .setup-card, .matchday") ?? doc);
        content.hidden = !opening;
        button.setAttribute("aria-expanded", String(opening));
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
    doc.addEventListener("click", click);
    doc.addEventListener("keydown", keydown, true);
    doc.addEventListener("close", close, true);
    return () => {
        closeInfo(doc);
        doc.removeEventListener("click", click);
        doc.removeEventListener("keydown", keydown, true);
        doc.removeEventListener("close", close, true);
    };
}
