import test from "node:test";
import assert from "node:assert/strict";
import { PopupHost, popupPosition } from "../../src/PitchLens.App/wwwroot/js/popups.mjs";

test("adjacent popups flip and clamp within desktop and small viewport bounds", () => {
    for (const viewport of [{ width: 1280, height: 800 }, { width: 1024, height: 768 }, { width: 390, height: 700 }]) {
        const size = { width: Math.min(376, viewport.width - 32), height: Math.min(460, viewport.height - 32) };
        for (const point of [{ x: 10, y: 10 }, { x: viewport.width - 5, y: viewport.height - 5 }]) {
            const position = popupPosition(point, size, viewport);
            assert.ok(position.left >= 16 && position.top >= 16);
            assert.ok(position.left + size.width <= viewport.width - 16);
            assert.ok(position.top + size.height <= viewport.height - 16);
        }
    }
});

test("native modal transitions preserve root focus return, backdrop close and teardown", () => {
    const windowHandlers = new Map(), documentHandlers = new Map();
    const win = { innerWidth: 1280, innerHeight: 800,
        addEventListener: (key, value) => windowHandlers.set(key, value), removeEventListener: key => windowHandlers.delete(key) };
    let focused = 0;
    const opener = { isConnected: true, focus: () => focused++, closest: () => null };
    const makeDialog = id => ({
        id, tagName: "DIALOG", open: false, style: {}, handlers: new Map(),
        addEventListener(key, value) { this.handlers.set(key, value); },
        removeEventListener(key) { this.handlers.delete(key); },
        showModal() { this.open = true; },
        close() { this.open = false; this.handlers.get("close")?.(); },
        getBoundingClientRect: () => ({ left: 100, top: 100, right: 476, bottom: 560, width: 376, height: 460 })
    });
    const roster = makeDialog("roster"), player = makeDialog("player"), all = [roster, player];
    const doc = {
        activeElement: opener,
        getElementById: id => all.find(d => d.id === id),
        querySelector: () => all.find(d => d.open),
        querySelectorAll: () => all.filter(d => d.open),
        addEventListener: (key, value) => documentHandlers.set(key, value),
        removeEventListener: key => documentHandlers.delete(key)
    };
    const host = new PopupHost(doc, win);
    host.open("roster");
    assert.equal(roster.open, true);
    doc.activeElement = { closest: () => roster };
    host.open("player", { x: 1200, y: 740 });
    assert.equal(roster.open, false);
    assert.equal(player.open, true);
    assert.equal(player.style.left, "800px");
    const beforeClose = focused;
    player.handlers.get("click")({ target: player, clientX: 800, clientY: 740 });
    assert.equal(player.open, false);
    assert.equal(focused, beforeClose + 1);
    doc.activeElement = opener;
    host.open("player");
    host.dispose();
    assert.equal(player.open, false);
    assert.equal(windowHandlers.size, 0);
    assert.equal(documentHandlers.size, 0);
    assert.equal(player.handlers.size, 0);
    assert.throws(() => host.open("missing"), /missing/);
});
