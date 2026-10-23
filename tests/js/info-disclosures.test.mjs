import test from "node:test";
import assert from "node:assert/strict";
import { installInfoDisclosures } from "../../src/PitchLens.App/wwwroot/js/info-disclosures.mjs";
import { PopupHost } from "../../src/PitchLens.App/wwwroot/js/popups.mjs";
import { keyboardAction } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";

// Minimal DOM/event model for the production delegated handlers, not browser/AX pixel certification.
function harness() {
    const listeners = [], dialogs = [], buttons = [], content = new Map();
    const isInfo = selector => selector?.includes("data-info-toggle");
    const query = (selector, scope) => isInfo(selector)
        ? buttons.filter(b => (!scope || b.parent === scope) && b.getAttribute("aria-expanded") === "true")
        : dialogs.filter(d => d.open);
    const doc = {
        activeElement: null,
        getElementById: id => content.get(id) ?? dialogs.find(d => d.id === id),
        querySelectorAll: selector => query(selector), querySelector: selector => query(selector)[0],
        addEventListener: (type, handler, capture = false) => listeners.push({ type, handler, capture }),
        removeEventListener(type, handler, capture = false) {
            const index = listeners.findIndex(l => l.type === type && l.handler === handler && l.capture === capture);
            if (index >= 0) listeners.splice(index, 1);
        }
    };
    const win = { innerWidth: 390, innerHeight: 500, addEventListener() {}, removeEventListener() {} };
    function dispatch(type, target, key) {
        const event = { target, key, defaultPrevented: false, stopped: false,
            preventDefault() { this.defaultPrevented = true; }, stopPropagation() { this.stopped = true; } };
        for (const capture of [true, false])
            for (const entry of [...listeners].filter(l => l.type === type && l.capture === capture)) {
                if (event.stopped) break;
                entry.handler(event);
            }
        return event;
    }
    function dialog(id) {
        const handlers = new Map();
        const value = {
            id, tagName: "DIALOG", open: false, attributes: {}, handlers, focusCount: 0,
            setAttribute(name, value) { this.attributes[name] = value; },
            addEventListener: (name, fn) => handlers.set(name, fn), removeEventListener: name => handlers.delete(name),
            querySelectorAll: selector => query(selector, value), querySelector: selector => query(selector, value)[0],
            contains: target => target === value || target.parent === value,
            show() { this.open = true; }, showModal() { this.open = true; },
            close() { this.open = false; dispatch("close", this); handlers.get("close")?.(); },
            getBoundingClientRect: () => ({ width: 358, height: 450 })
        };
        dialogs.push(value);
        return value;
    }
    function help(parent, id) {
        const attributes = { "aria-controls": id, "aria-expanded": "false" };
        content.set(id, { hidden: true });
        const button = {
            tagName: "BUTTON", parent, ownerDocument: doc, isConnected: true, focused: 0,
            getAttribute: name => attributes[name], setAttribute: (name, value) => { attributes[name] = value; },
            focus() { this.focused++; doc.activeElement = this; },
            closest(selector) {
                if (selector === "[data-info-toggle]" || selector === "button, input, select, a, [tabindex]") return this;
                if (selector === ".info-disclosure") return this;
                if (selector === "dialog" || selector === "dialog, .setup-card, .matchday") return parent;
                return null;
            }
        };
        buttons.push(button);
        return button;
    }
    const uninstall = installInfoDisclosures(doc);
    const host = new PopupHost(doc, win);
    const opener = { isConnected: true, focus() { doc.activeElement = this; }, closest: () => null };
    doc.activeElement = opener;
    const click = button => { button.focus(); return dispatch("click", button); };
    const press = (button, key) => {
        const event = dispatch("keydown", button, key);
        // The browser supplies button activation; our handler must leave it alone.
        if (!event.defaultPrevented && button.tagName === "BUTTON" && ["Enter", " "].includes(key)) dispatch("click", button);
        if (!event.defaultPrevented && key === "Escape" && button.parent?.open && button.parent.attributes["aria-modal"] === "true") button.parent.close();
        return event;
    };
    return { doc, host, dialog, help, content, click, press, dispatch, uninstall, listeners };
}

for (const id of ["reading-dialog", "settings-dialog"]) {
    test(`${id}: click/Enter/Space open inline help; first Escape restores opener without dismissing its parent`, () => {
        const h = harness(), parent = h.dialog(id), first = h.help(parent, "first"), second = h.help(parent, "second");
        h.host.open(id);
        const floating = h.host.floating;
        for (const activation of ["click", "Enter", " "]) {
            if (activation === "click") h.click(first); else h.press(first, activation);
            assert.equal(first.getAttribute("aria-expanded"), "true");
            assert.equal(h.content.get("first").hidden, false);
            assert.equal(parent.open, true);
            assert.equal(h.host.floating, floating, "help never replaces the popup owner");
            assert.equal(keyboardAction({ target: first, key: " " }), null, "help activation cannot toggle replay");
            const before = first.focused;
            const escape = h.press(first, "Escape");
            assert.equal(escape.defaultPrevented, true);
            assert.equal(escape.stopped, true, "PopupHost's bubbling Escape never receives the first key");
            assert.equal(parent.open, true);
            assert.equal(first.getAttribute("aria-expanded"), "false");
            assert.equal(h.content.get("first").hidden, true);
            assert.equal(first.focused, before + 1);
            assert.equal(h.doc.activeElement, first);
        }
        h.click(first); h.click(second);
        assert.equal(h.content.get("first").hidden, true, "only one explanation per parent");
        assert.equal(h.content.get("second").hidden, false);
        h.click(second);
        assert.equal(second.getAttribute("aria-expanded"), "false", "the same button toggles closed");
        const icon = { parent, closest: selector => first.closest(selector) };
        h.dispatch("click", icon);
        assert.equal(first.getAttribute("aria-expanded"), "true", "click/tap on the icon reaches its native button");
        h.press(first, "Escape");
        h.press(second, "Escape");
        assert.equal(parent.open, false, "the next Escape still closes the actual parent normally");
        h.host.dispose(); h.uninstall();
        assert.equal(h.listeners.length, 0);
    });
}

test("native close, popup transition, outside click and disposal reset help without stale state or focus theft", () => {
    const h = harness(), reading = h.dialog("reading-dialog"), settings = h.dialog("settings-dialog");
    const help = h.help(reading, "reading-help");
    h.host.open("reading-dialog"); h.click(help);
    const before = help.focused;
    h.host.open("settings-dialog");
    assert.equal(h.content.get("reading-help").hidden, true);
    assert.equal(help.getAttribute("aria-expanded"), "false");
    assert.equal(help.focused, before);
    h.host.open("reading-dialog");
    assert.equal(help.getAttribute("aria-expanded"), "false");
    h.click(help); reading.close();
    assert.equal(h.content.get("reading-help").hidden, true);
    h.host.open("reading-dialog"); h.click(help);
    const inside = { parent: reading, closest: () => null };
    h.dispatch("click", inside);
    assert.equal(reading.open, true);
    assert.equal(h.content.get("reading-help").hidden, true);
    assert.equal(help.focused, before + 2);
    h.click(help); h.host.close("reading-dialog");
    assert.equal(help.getAttribute("aria-expanded"), "false");
    assert.equal(settings.open, false);
    h.host.dispose(); h.uninstall();
});

test("missing contextual content is an explicit error rather than false expanded state", () => {
    const h = harness(), parent = h.dialog("reading-dialog"), help = h.help(parent, "missing");
    h.content.delete("missing");
    assert.throws(() => h.click(help), /explanation is missing/);
    assert.equal(help.getAttribute("aria-expanded"), "false");
    h.host.dispose(); h.uninstall();
});
