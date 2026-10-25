import test from "node:test";
import assert from "node:assert/strict";
import { PopupHost, popupPosition, popupHost, releasePopupHost } from "../../src/PitchLens.App/wwwroot/js/popups.mjs";

function harness() {
    const windowHandlers = new Map(), documentHandlers = new Map(), captureHandlers = new Map(), tasks = [], observers = [];
    const win = {
        innerWidth: 1280, innerHeight: 800,
        addEventListener: (key, fn) => windowHandlers.set(key, fn), removeEventListener: key => windowHandlers.delete(key),
        ResizeObserver: class {
            constructor(callback) { this.callback = callback; observers.push(this); }
            observe(target) { this.target = target; this.disconnected = false; } disconnect() { this.disconnected = true; }
        }
    };
    const opener = { isConnected: true, focused: 0, focus() { this.focused++; doc.activeElement = this; }, closest: () => null };
    const makeDialog = id => ({
        id, tagName: "DIALOG", open: false, attributes: {}, height: 460, handlers: new Map(), modal: 0, nonmodal: 0,
        style: { setProperty(key, value) { this[key] = value; }, getPropertyValue(key) { return this[key] ?? ""; } },
        setAttribute(key, value) { this.attributes[key] = value; },
        addEventListener(key, fn) { this.handlers.set(key, fn); }, removeEventListener(key) { this.handlers.delete(key); },
        showModal() { this.open = true; this.modal++; }, show() { this.open = true; this.nonmodal++; },
        close() { this.open = false; tasks.push(() => this.handlers.get("close")?.()); },
        contains(target) { return target === this || target.parent === this; },
        querySelectorAll: () => [],
        getBoundingClientRect() {
            const height = Math.min(this.height, parseFloat(this.style["--player-lens-height"] ?? "460"));
            return { left: 100, top: 100, right: 476, bottom: 100 + height, width: 376, height };
        }
    });
    const player = makeDialog("player-dialog"), roster = makeDialog("roster-dialog"), story = makeDialog("story-dialog"), analysis=makeDialog("analysis-dialog"),more=makeDialog("more-dialog");
    const view=makeDialog("view-dialog"),settings=makeDialog("settings-dialog"),fixture=makeDialog("fixture-dialog");
    const reading=makeDialog("reading-dialog");
    const all = [player, roster, story, analysis,more,view,settings,fixture,reading];
    const doc = {
        activeElement: opener, getElementById: id => all.find(d => d.id === id),
        querySelector: () => all.find(d => d.open), querySelectorAll: selector => selector?.includes("data-info-toggle") ? [] : all.filter(d => d.open),
        addEventListener: (key, fn, capture) => (capture ? captureHandlers : documentHandlers).set(key, fn),
        removeEventListener: (key, fn, capture) => (capture ? captureHandlers : documentHandlers).delete(key)
    };
    const flush = () => { while (tasks.length) tasks.shift()(); };
    return { win, doc, opener, player, roster, story, analysis,more,view,settings,fixture,reading, observers, documentHandlers, windowHandlers, flush };
}

test("startup Text size and replay popups share one nonmodal host with Escape, outside, Close and focus return",async()=>{
    const h=harness(),oldDoc=globalThis.document,oldWin=globalThis.window;
    const listeners=[];const add=h.doc.addEventListener;
    h.doc.addEventListener=(type,fn,capture)=>{if(type==="click")listeners.push(fn);add(type,fn,capture);};
    globalThis.document=h.doc;globalThis.window=h.win;
    try{
        await import("../../src/PitchLens.App/wwwroot/js/app-interface.mjs");
        const trigger={...h.opener,dataset:{openPopup:"reading-dialog"},closest(selector){return selector==="[data-open-popup]"||selector==="[data-open-popup='reading-dialog']"?this:null;}};
        h.doc.activeElement=trigger;for(const listener of [...listeners])listener({target:trigger});
        const shared=popupHost(h.doc,h.win);assert.equal(shared,popupHost(h.doc,h.win));
        assert.equal(h.reading.open,true);assert.equal(h.reading.modal,0);assert.equal(h.reading.nonmodal,1);
        const inside={parent:h.reading,closest:()=>null};h.documentHandlers.get("click")({target:inside});assert.equal(h.reading.open,true);
        h.documentHandlers.get("click")({target:trigger});assert.equal(h.reading.open,true);
        h.documentHandlers.get("keydown")({key:"Escape",preventDefault(){}});h.flush();
        assert.equal(h.reading.open,false);assert.equal(trigger.focused,1);
        shared.open("settings-dialog");h.doc.activeElement={closest:()=>h.settings};
        shared.open("reading-dialog");h.flush();assert.equal(h.settings.open,false);assert.equal(h.reading.open,true);
        shared.open("player-dialog");h.flush();assert.equal(h.reading.open,false);assert.equal(h.player.open,true);
        shared.open("reading-dialog");h.flush();assert.equal(h.player.open,false);
        shared.follow({x:500,y:300},{left:0,top:0,right:1000,bottom:700,height:700},40);
        assert.equal(h.reading.style.transform,undefined,"text preferences never follow a player");
        h.documentHandlers.get("click")({target:{closest:()=>null}});h.flush();assert.equal(h.reading.open,false);
        shared.open("reading-dialog");
        const close={closest:selector=>selector==="[data-close-popup]"?close:selector==="dialog"?h.reading:null};
        h.documentHandlers.get("click")({target:close});h.flush();assert.equal(h.reading.open,false);
        releasePopupHost(shared);assert.equal(h.documentHandlers.size,0);
    }finally{globalThis.document=oldDoc;globalThis.window=oldWin;}
});

test("lens placement flips/clamps, prefers pitch space and avoids the selected shirt when possible", () => {
    for (const viewport of [{ width: 1280, height: 800 }, { width: 1024, height: 768 }, { width: 390, height: 700 }]) {
        const size = { width: Math.min(376, viewport.width - 32), height: Math.min(300, viewport.height - 32) };
        for (const point of [{ x: 10, y: 10 }, { x: viewport.width - 5, y: viewport.height - 5 }, { x: viewport.width / 2, y: 80 }]) {
            const p = popupPosition(point, size, viewport);
            assert.ok(p.left >= 16 && p.top >= 16);
            assert.ok(p.left + size.width <= viewport.width - 16);
            assert.ok(p.top + size.height <= viewport.height - 16);
            assert.ok(!(point.x > p.left && point.x < p.left + size.width && point.y > p.top && point.y < p.top + size.height));
        }
    }
    const bounds = { left: 40, right: 1240, top: 250, bottom: 720, height: 470 };
    const p = popupPosition({ x: 1200, y: 650 }, { width: 376, height: 400 }, { width: 1280, height: 800 }, bounds, 40);
    assert.ok(p.left + 376 < 1200);
    assert.ok(p.top >= bounds.top && p.top + 400 <= bounds.bottom);
});

test("story remains modal with native backdrop close and focus return", () => {
    const h = harness(), host = new PopupHost(h.doc, h.win);
    host.open("story-dialog");
    assert.equal(h.story.modal, 1);
    assert.equal(h.story.attributes["aria-modal"], "true");
    h.story.handlers.get("click")({ target: h.story, clientX: 800, clientY: 740 });
    h.flush();
    assert.equal(h.story.open, false);
    assert.equal(h.opener.focused, 1);
    host.dispose();
    assert.equal(h.windowHandlers.size, 0); assert.equal(h.documentHandlers.size, 0);
    assert.throws(() => host.open("missing"), /missing/);
});

test("phone player sheet uses CSS viewport bounds and drops desktop tracking when resized", () => {
    const h = harness(), host = new PopupHost(h.doc, h.win);
    host.open("player-dialog");
    const bounds = { left: 16, top: 200, right: 1240, bottom: 720, height: 520 };
    host.follow({ x: 200, y: 400 }, bounds, 40);
    assert.match(h.player.style.transform, /^translate/);
    h.win.innerWidth = 375;
    h.windowHandlers.get("resize")();
    assert.equal(h.player.style.transform, "");
    host.follow({ x: 80, y: 320 }, { left: 12, top: 200, right: 363, bottom: 430, height: 230 }, 24);
    assert.equal(h.player.style.transform, "");
    h.win.innerWidth = 1280;
    h.windowHandlers.get("resize")();
    assert.match(h.player.style.transform, /^translate/);
    host.dispose();
});

test("roster and player are nonmodal; follow, switching, Escape/outside and asynchronous focus restoration are safe", () => {
    const h = harness(), host = new PopupHost(h.doc, h.win);
    host.open("roster-dialog");
    assert.equal(h.roster.nonmodal, 1); assert.equal(h.roster.modal, 0);
    h.doc.activeElement = { parent: h.roster, closest: () => h.roster };
    host.open("player-dialog"); h.flush();
    assert.equal(h.roster.open, false); assert.equal(h.player.nonmodal, 1);
    assert.equal(h.player.attributes["aria-modal"], "false");
    const bounds = { left: 40, top: 200, right: 1240, bottom: 720, width: 1200, height: 520 };
    host.follow({ x: 200, y: 400 }, bounds, 40);
    const first = h.player.style.transform;
    host.follow({ x: 1000, y: 600 }, bounds, 40);
    assert.notEqual(h.player.style.transform, first);
    h.player.height = 330; h.observers[0].callback();
    assert.equal(host.size.height, 330);
    host.hidePlayer(true); assert.equal(h.player.style.visibility, "hidden");
    host.hidePlayer(false); assert.equal(h.player.style.visibility, "");
    host.open("roster-dialog"); h.observers[0].callback(); h.flush();
    assert.equal(h.roster.style.transform, undefined, "roster never inherits a previous player's tracking transform");
    host.open("player-dialog"); h.flush();

    const second = { isConnected: true, focused: 0, focus() { this.focused++; h.doc.activeElement = this; }, closest: () => null };
    h.doc.activeElement = second;
    const opens = h.player.nonmodal;
    host.open("player-dialog"); // Already-selected/second shirt does not add another card or modal.
    assert.equal(h.player.nonmodal, opens);
    let prevented = false;
    h.documentHandlers.get("keydown")({ key: "Escape", preventDefault() { prevented = true; } }); h.flush();
    assert.equal(prevented, true); assert.equal(second.focused, 1);

    host.open("player-dialog");
    const transport = { closest: () => null };
    h.doc.activeElement = transport;
    h.documentHandlers.get("click")({ target: transport }); h.flush();
    assert.equal(h.player.open, false); assert.equal(h.doc.activeElement, transport);
    assert.equal(second.focused, 1, "asynchronous outside close must not steal focus");

    h.doc.activeElement = second; host.open("player-dialog");
    host.close("player-dialog", false); host.open("player-dialog"); h.flush();
    assert.equal(host.floating, h.player, "delayed old close event must not clear a reopened lens");
    host.open("story-dialog"); h.flush();
    assert.equal(h.player.open, false); assert.equal(h.story.modal, 1);
    host.dispose(); h.flush();
    assert.ok(h.observers.every(o => o.disconnected));
    assert.equal(h.player.handlers.size, 0);
    assert.equal(h.documentHandlers.size, 0); assert.equal(h.windowHandlers.size, 0);
});

test("Analysis is a nonmodal independent popup: no player following, Escape/outside/Close and safe focus return",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    host.open("analysis-dialog");
    assert.equal(h.analysis.nonmodal,1);assert.equal(h.analysis.modal,0);
    assert.equal(h.analysis.attributes["aria-modal"],"false");
    host.follow({x:700,y:500},{left:0,top:0,right:1000,bottom:700,height:700},40);
    h.windowHandlers.get("resize")();
    assert.equal(h.analysis.style.transform,undefined,"Analysis never follows or inherits player coordinates");
    const inside={parent:h.analysis,closest:()=>null};
    h.documentHandlers.get("click")({target:inside});assert.equal(h.analysis.open,true);
    const trigger={closest:selector=>selector==="#analysis-button"?trigger:null};
    h.documentHandlers.get("click")({target:trigger});assert.equal(h.analysis.open,true,"opening click does not immediately dismiss popup");
    let prevented=false;
    h.documentHandlers.get("keydown")({key:"Escape",preventDefault(){prevented=true;}});
    h.flush();assert.equal(prevented,true);assert.equal(h.analysis.open,false);assert.equal(h.opener.focused,1);
    host.open("analysis-dialog");
    const outside={focused:0,focus(){this.focused++;h.doc.activeElement=this;},closest:selector=>selector==="button, input, select, a, [tabindex]"?outside:null};
    h.documentHandlers.get("click")({target:outside});h.flush();
    assert.equal(h.analysis.open,false);assert.equal(outside.focused,1);assert.equal(h.opener.focused,1,"outside close must not steal focus");
    h.doc.activeElement=h.opener;host.open("analysis-dialog");
    const close={closest:selector=>selector==="[data-close-popup]"?close:selector==="dialog"?h.analysis:null};
    h.documentHandlers.get("click")({target:close});h.flush();
    assert.equal(h.analysis.open,false);assert.equal(h.opener.focused,2);
    host.open("analysis-dialog");h.doc.activeElement=inside;
    inside.closest=selector=>selector==="dialog"?h.analysis:null;
    host.open("player-dialog");h.flush();
    assert.equal(h.analysis.open,false);assert.equal(h.player.open,true);
    host.open("analysis-dialog");h.flush();assert.equal(h.player.open,false);
    host.dispose();h.flush();assert.equal(h.analysis.open,false);
    assert.equal(h.analysis.handlers.size,0);assert.equal(h.documentHandlers.size,0);
});

test("Analysis close falls back to pitch focus if its opener becomes disabled during a film transition",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    const pitch={focused:0,focus(){this.focused++;}};
    const find=h.doc.getElementById;
    h.doc.getElementById=id=>id==="pitch"?pitch:find(id);
    host.open("analysis-dialog");h.opener.disabled=true;
    host.close("analysis-dialog");h.flush();
    assert.equal(h.opener.focused,0);assert.equal(pitch.focused,1);
    host.dispose();
});

test("More is a plain nonmodal action dialog; Escape/outside and transitions retain one-popup focus origin",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    host.open("more-dialog");assert.equal(h.more.nonmodal,1);assert.equal(h.more.modal,0);
    assert.equal(h.more.attributes["aria-modal"],"false");
    host.follow({x:300,y:500},{left:0,top:0,right:800,bottom:700,height:700},40);
    assert.equal(h.more.style.transform,undefined);
    const trigger={closest:selector=>selector==="#more-button"?trigger:null};
    h.documentHandlers.get("click")({target:trigger});assert.equal(h.more.open,true);
    h.doc.activeElement={parent:h.more,closest:selector=>selector==="dialog"?h.more:null};
    host.open("roster-dialog");h.flush();
    assert.equal(h.more.open,false);assert.equal(h.roster.open,true);
    host.close("roster-dialog");h.flush();assert.equal(h.opener.focused,1);
    host.open("more-dialog");h.documentHandlers.get("keydown")({key:"Escape",preventDefault(){}});h.flush();
    assert.equal(h.more.open,false);assert.equal(h.opener.focused,2);
    host.open("more-dialog");
    const outside={closest:()=>null};
    h.documentHandlers.get("click")({target:outside});h.flush();
    assert.equal(h.more.open,false);assert.equal(h.opener.focused,2);
    host.dispose();
});
test("View settings is nonmodal, never follows a player, and retains one-popup/keyboard/outside focus behavior",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    host.open("view-dialog");assert.equal(h.view.nonmodal,1);assert.equal(h.view.modal,0);
    assert.equal(h.view.attributes["aria-modal"],"false");
    host.follow({x:900,y:500},{left:0,top:0,right:1200,bottom:700,height:700},40);
    assert.equal(h.view.style.transform,undefined);
    const opener={closest:selector=>selector==="#view-button, #settings-camera-button"?opener:null};
    h.documentHandlers.get("click")({target:opener});assert.equal(h.view.open,true);
    h.documentHandlers.get("keydown")({key:"Escape",preventDefault(){}});h.flush();
    assert.equal(h.view.open,false);assert.equal(h.opener.focused,1);
    host.open("view-dialog");host.open("player-dialog");h.flush();assert.equal(h.view.open,false);
    host.open("view-dialog");h.flush();assert.equal(h.player.open,false);
    h.documentHandlers.get("click")({target:{closest:()=>null}});h.flush();assert.equal(h.view.open,false);
    host.dispose();
});
test("Replay settings camera entry closes the modal before opening the shared nonmodal camera popup",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    host.open("settings-dialog");assert.equal(h.settings.modal,1);assert.equal(h.settings.open,true);
    const button={parent:h.settings,closest:selector=>selector==="dialog"?h.settings:
        selector==="#view-button, #settings-camera-button"?button:null};
    h.doc.activeElement=button;
    host.open("view-dialog");h.flush();
    assert.equal(h.settings.open,false);assert.equal(h.view.open,true);
    assert.equal(h.view.nonmodal,1);assert.equal(h.view.modal,0);
    assert.equal(h.doc.querySelectorAll().length,1);
    h.documentHandlers.get("click")({target:button});
    assert.equal(h.view.open,true,"the original Settings camera click must not dismiss the new popup");
    host.close("view-dialog");h.flush();
    assert.equal(h.opener.focused,1,"focus returns to the original settings opener, not the closed modal");
    host.dispose();
});
test("fixture picker is nonmodal and never follows a player; cancel/Escape restores focus without opening another popup",()=>{
    const h=harness(),host=new PopupHost(h.doc,h.win);
    host.open("fixture-dialog");assert.equal(h.fixture.nonmodal,1);assert.equal(h.fixture.modal,0);
    const trigger={closest:selector=>selector==="#generate-match"?trigger:null};
    h.documentHandlers.get("click")({target:trigger});assert.equal(h.fixture.open,true);
    host.follow({x:300,y:400},{left:0,top:0,right:800,bottom:700,height:700},40);
    assert.equal(h.fixture.style.transform,undefined);
    h.documentHandlers.get("keydown")({key:"Escape",preventDefault(){}});h.flush();
    assert.equal(h.fixture.open,false);assert.equal(h.opener.focused,1);
    host.dispose();
});
