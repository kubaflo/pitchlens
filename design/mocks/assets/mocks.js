(function () {
    "use strict";
    const Core = window.PitchLensMockCore;
    const model = Core.prepare(window.PitchLensDemo);
    const design = document.body.dataset.design;
    const clock = new Core.Clock(model.duration, Number(document.body.dataset.start || 0));
    const ns = "http://www.w3.org/2000/svg";
    let selected = "manchester-city-doku", raf = null, lastInfo = -Infinity, lastPaint = -Infinity;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    const club = team => team === "arsenal" ? "Arsenal" : "Manchester City";
    const crest = team => "assets/images/" + (team === "arsenal" ? "arsenal.png" : "manchester-city.png");
    const safe = value => String(value).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
    const text = (selector, value) => $$(selector).forEach(node => { if (node.textContent !== value) node.textContent = value; });
    const now = () => performance.now();

    const pitch = $("[data-pitch]");
    const svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "-24 -24 1098 728");
    svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
    svg.setAttribute("aria-label", "Interactive synthetic football pitch. Each shirt is a player button.");
    svg.innerHTML = `<defs><pattern id="grass" width="210" height="680" patternUnits="userSpaceOnUse"><rect width="105" height="680" fill="#2d7552"/><rect x="105" width="105" height="680" fill="#317c57"/></pattern></defs>
      <rect width="1050" height="680" rx="4" fill="url(#grass)"/>
      <g fill="none" stroke="#d6eadd" stroke-width="3"><rect x="2" y="2" width="1046" height="676"/><path d="M525 2V678"/><circle cx="525" cy="340" r="91.5"/><path d="M2 138.4H165V541.6H2M1048 138.4H885V541.6H1048M2 248.4H55V431.6H2M1048 248.4H995V431.6H1048"/><path d="M0 303.4H-15V376.6H0M1050 303.4H1065V376.6H1050"/></g>
      <g fill="#d6eadd"><circle cx="525" cy="340" r="4"/><circle cx="110" cy="340" r="4"/><circle cx="940" cy="340" r="4"/></g>`;
    const trail = document.createElementNS(ns, "path");
    for (const [name, value] of Object.entries({ fill: "none", stroke: "#d7b8ff", "stroke-width": "5", opacity: ".8", "pointer-events": "none" })) trail.setAttribute(name, value);
    svg.append(trail);
    const sprites = new Map();
    for (const player of [...model.players].sort((a, b) => a.id < b.id ? 1 : -1)) {
        const group = document.createElementNS(ns, "g");
        group.dataset.player = player.id;
        group.setAttribute("role", "button"); group.setAttribute("tabindex", "0");
        group.setAttribute("aria-label", `${player.name}, ${club(player.team)}, shirt ${player.number}. Open player popup.`);
        group.innerHTML = `<circle class="shirt-shadow" cx="2" cy="4" r="23"/><circle class="selection-ring" r="29"/><circle class="shirt-disc" r="21" fill="${player.team === "arsenal" ? "#00ff87" : "#ff83b8"}"/><text class="player-number">${player.number}</text>`;
        sprites.set(player.id, group); svg.append(group);
    }
    const ball = document.createElementNS(ns, "circle");
    for (const [name, value] of Object.entries({ r: "9", fill: "white", stroke: "#172c22", "stroke-width": "2", "pointer-events": "none" })) ball.setAttribute(name, value);
    svg.append(ball); pitch.append(svg);

    const rosterHTML = model.players.map(p => `<button data-choose="${safe(p.id)}"><img src="${crest(p.team)}" alt=""><span>${safe(p.name)}<small>${club(p.team)} · #${p.number}</small></span></button>`).join("");
    const studioTabs = design === "studio" ? `<div class="popup-tabs" role="tablist" aria-label="Studio views"><button role="tab" id="match-tab" aria-selected="false" aria-controls="match-panel" data-popup-tab="match">Match</button><button role="tab" id="story-tab" aria-selected="true" aria-controls="story-panel" data-popup-tab="story">Story</button></div>` : "";
    document.body.insertAdjacentHTML("beforeend", `
      <dialog id="player-dialog" class="player-dialog" aria-labelledby="player-title">
        <header class="dialog-head"><div><span class="eyebrow">Player lens</span><span class="caption">Synthetic match facts at the playhead</span></div><button class="dialog-close" data-close aria-label="Close player popup">Close</button></header>
        <div class="dialog-body"><div class="player-identity"><img data-player-crest alt=""><div><h3 id="player-title" data-player-name></h3><p data-player-club></p></div></div><dl class="player-stats"><div><dt>Passes completed</dt><dd data-player-passes></dd></div><div><dt>Distance</dt><dd data-player-distance></dd></div><div><dt>Pass accuracy</dt><dd data-player-accuracy></dd></div><div><dt>Goals</dt><dd data-player-goals></dd></div></dl><span class="caption">Illustrative 6v6 data. Not real athlete performance.</span></div>
      </dialog>
      <dialog id="roster-dialog" aria-labelledby="roster-title"><header class="dialog-head"><div><h2 id="roster-title">Choose a player</h2><span class="caption">Real 2026/27 names · Synthetic match</span></div><button class="dialog-close" data-close>Close</button></header><div class="dialog-body"><div class="roster-list">${rosterHTML}</div></div></dialog>
      <dialog id="story-dialog" aria-labelledby="story-title"><header class="dialog-head"><div><span class="eyebrow">Scripted match story</span><h2 id="story-title">A game of transitions</h2><span class="caption">Full illustrative arc. Choose a moment to preview.</span></div><button class="dialog-close" data-close>Close</button></header>${studioTabs}<div class="dialog-body"><section id="match-panel" role="tabpanel" aria-labelledby="match-tab" hidden><h3>Arsenal vs Manchester City</h3><p>Pick a real squad name to inspect synthetic replay facts.</p><div class="roster-list">${design === "studio" ? rosterHTML : ""}</div></section><section id="story-panel"${design === "studio" ? ' role="tabpanel" aria-labelledby="story-tab"' : ""}><ol class="story-list">${model.story.beats.map(b => `<li class="story-row"><button data-jump="${b.endTime}" aria-label="Preview ${safe(b.kind)} at ${b.endTime} seconds">${b.endTime.toFixed(1)}s</button><div><h3>${safe(({ "build-up": "Build-up under pressure", turnover: "The turnover", counterattack: "City break forward", "support-run": "Doku finds space", finish: "Haaland finishes", response: "Arsenal respond" })[b.kind])}</h3><p>${safe(({ "build-up": "Arsenal work through Saliba and Rice as Dias closes the space.", turnover: "Doku intercepts Rice's pass and the direction changes.", counterattack: "Doku and Cherki carry City into the attacking half.", "support-run": "Doku advances to receive Cherki's pass, then carries toward goal.", finish: "Doku finds Haaland. The scripted finish comes at 45.5 seconds.", response: "Odegaard restarts; Saka, Gyokeres and Rice push back." })[b.kind])}</p><small>Demo evidence: ${b.eventIds.map(safe).join(", ")}</small></div></li>`).join("")}</ol></section></div></dialog>
      <dialog id="composer-dialog" aria-labelledby="composer-title"><header class="dialog-head"><div><span class="eyebrow">Mock composer</span><h2 id="composer-title">Set the scene.</h2><span class="caption">Scripted preview only. No Copilot, authentication or API calls.</span></div><button class="dialog-close" data-close>Close</button></header><div class="dialog-body"><form class="composer-form"><div class="composer-controls"><label>Story focus<select name="focus"><option value="0">Full match story</option><option value="18.5">The turnover & counterattack</option><option value="48.5">Arsenal's response</option></select></label><label>Replay pace<select name="pace"><option value="1">Real time · 1×</option><option value="0.75">Take it in · 0.75×</option><option value="1.25">Quick look · 1.25×</option></select></label></div><button type="submit" class="button purple">Preview scripted scene</button><div class="preview-result" hidden role="status"><h3>Demo preview ready</h3><p data-preview-summary></p><button type="button" class="button mint" data-use-preview>Use this preview</button></div></form></div></dialog>
      <dialog id="settings-dialog" aria-labelledby="settings-title"><header class="dialog-head"><div><h2 id="settings-title">Replay settings</h2><span class="caption">Local design preview preferences</span></div><button class="dialog-close" data-close>Close</button></header><div class="dialog-body"><label class="settings-row">Show shirt numbers<input type="checkbox" checked data-numbers></label><p>Space plays or pauses outside controls. Arrow keys seek one second. Player shirts also work with Enter or Space.</p><p class="caption">Reduced-motion preferences are respected. No settings are sent or stored remotely.</p><a href="index.html">Back to all five designs</a></div></dialog>
      <dialog id="controls-dialog" aria-labelledby="controls-title"><header class="dialog-head"><h2 id="controls-title">Match controls</h2><button class="dialog-close" data-close>Close</button></header><div class="dialog-body"><div class="actions"><button class="button purple" data-play>Play</button><button class="button" data-restart>Restart</button><button class="button" data-seek="-5">−5 seconds</button><button class="button" data-seek="5">+5 seconds</button></div><div class="actions"><button class="button" data-open="roster">Players</button><button class="button" data-open="story">Match story</button><button class="button" data-open="composer">New match</button><button class="button" data-open="settings">Settings</button></div></div></dialog>
      <span class="visually-hidden" role="status" data-announcement></span>`);

    const playerDialog = $("#player-dialog");
    const dialogs = $$("dialog");
    const openers = new WeakMap();
    let preview = { time: 0, rate: 1 };
    function announce(message) { text("[data-announcement]", message); }
    function closeDialog(dialog) { if (dialog.open) dialog.close(); }
    function closeAll() { dialogs.forEach(closeDialog); }
    for (const dialog of dialogs) {
        dialog.addEventListener("close", () => {
            if (dialog.open || dialogs.some(other => other.open)) return;
            const opener = openers.get(dialog);
            if (opener?.isConnected) opener.focus({ preventScroll: true });
        });
        dialog.addEventListener("click", event => {
            if (event.target !== dialog) return;
            const box = dialog.getBoundingClientRect();
            if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
        });
    }
    function openDialog(name, opener) {
        if (name === "roster" && design === "studio") { name = "story"; popupTab("match"); }
        else if (name === "story") popupTab("story");
        const dialog = $("#" + name + "-dialog");
        closeAll(); pause();
        openers.set(dialog, opener || document.activeElement);
        dialog.showModal();
    }
    function popupTab(name) {
        if (design !== "studio") return;
        $$("[data-popup-tab]").forEach(tab => {
            const active = tab.dataset.popupTab === name;
            tab.setAttribute("aria-selected", String(active)); tab.tabIndex = active ? 0 : -1;
        });
        $("#match-panel").hidden = name !== "match"; $("#story-panel").hidden = name !== "story";
    }
    function profile(time) {
        const p = model.player(selected), values = Core.stats(model, selected, time);
        text("[data-player-name]", p.name);
        text("[data-player-club]", `${club(p.team)} · #${p.number}`);
        const image = $("[data-player-crest]"); image.src = crest(p.team); image.alt = club(p.team) + " crest";
        text("[data-player-passes]", `${values.completed} / ${values.passes}`);
        text("[data-player-distance]", `${values.distance.toFixed(1)} m`);
        text("[data-player-accuracy]", values.accuracy === null ? "Unavailable" : `${Math.round(values.accuracy * 100)}%`);
        $("[data-player-accuracy]").classList.toggle("no-attempts", values.accuracy === null);
        text("[data-player-goals]", String(values.goals));
    }
    function openPlayer(id, opener, event) {
        selected = id;
        sprites.forEach((group, playerId) => { group.classList.toggle("selected", playerId === id); group.setAttribute("aria-pressed", String(playerId === id)); });
        if (design !== "focus") { closeAll(); pause(); }
        openers.set(playerDialog, opener?.isConnected ? opener : $("[data-open=roster]"));
        profile(clock.time(now()));
        if (!playerDialog.open) {
            if (design === "focus") playerDialog.show(); else playerDialog.showModal();
        }
        if (design === "matchday") {
            const anchor = opener?.getBoundingClientRect() || pitch.getBoundingClientRect();
            const box = playerDialog.getBoundingClientRect();
            const x = event?.clientX || anchor.right, y = event?.clientY || anchor.top;
            const side = x + box.width + 24 <= innerWidth - 16 ? x + 24 : x - box.width - 24;
            playerDialog.style.setProperty("--popup-x", `${Math.max(16, Math.min(side, innerWidth - box.width - 16))}px`);
            playerDialog.style.setProperty("--popup-y", `${Math.max(16, Math.min(y, innerHeight - box.height - 16))}px`);
        }
        paint(true);
    }
    function info(time) {
        const value = Core.score(model, time);
        text("[data-score]", `${value.arsenal} : ${value["manchester-city"]}`);
        const beat = model.story.beats.filter(b => b.endTime <= time).at(-1);
        text("[data-current-beat]", beat ? ({ "build-up": "Build-up under pressure", turnover: "Possession turns", counterattack: "The counterattack", "support-run": "Doku finds space", finish: "The finish", response: "Arsenal respond" })[beat.kind] : "The opening shape");
        $$("[data-play]").forEach(button => { button.textContent = clock.playing ? "Pause" : time >= 72 ? "Replay" : "Play"; button.setAttribute("aria-pressed", String(clock.playing)); });
        const latest = $$("[data-jump]").filter(b => !b.closest("dialog") && Number(b.dataset.jump) <= time).at(-1);
        $$("[data-jump]").filter(b => !b.closest("dialog")).forEach(b => b.classList.toggle("active", b === latest));
        if (playerDialog.open) profile(time);
    }
    function paint(force) {
        const stamp = now(), time = clock.time(stamp);
        if (!force && clock.playing && reduced.matches && stamp - lastPaint < 250) return;
        lastPaint = stamp;
        for (const player of model.players) {
            const p = Core.at(player.frames, time);
            sprites.get(player.id).setAttribute("transform", `translate(${p.x * 10} ${p.y * 10})`);
        }
        const pos = Core.ball(model, time); ball.setAttribute("cx", pos.x * 10); ball.setAttribute("cy", pos.y * 10);
        const path = model.player(selected).frames.filter(f => f.time < time).concat(Core.at(model.player(selected).frames, time));
        trail.setAttribute("d", path.map((p, i) => `${i ? "L" : "M"}${p.x * 10},${p.y * 10}`).join(" "));
        text("[data-clock]", `${time.toFixed(1)} / 72.0 s`);
        $$("[data-scrub]").forEach(input => { if (document.activeElement !== input) input.value = time; });
        if (force || stamp - lastInfo >= 250 || !clock.playing) { info(time); lastInfo = stamp; }
    }
    function loop() { raf = null; paint(false); if (clock.playing && !document.hidden) raf = requestAnimationFrame(loop); }
    function play() { clock.play(now()); paint(true); if (raf === null) raf = requestAnimationFrame(loop); }
    function pause() { clock.pause(now()); if (raf !== null) cancelAnimationFrame(raf); raf = null; paint(true); }
    function seek(time) { clock.seek(time, now()); paint(true); if (clock.playing && raf === null) raf = requestAnimationFrame(loop); }
    document.addEventListener("click", event => {
        const target = event.target.closest("button,[data-player]");
        if (!target) return;
        if (target.dataset.player) return openPlayer(target.dataset.player, target, event);
        if (target.dataset.choose) { const opener = $("[data-open=roster]"); closeAll(); return openPlayer(target.dataset.choose, opener); }
        if (target.dataset.open) return openDialog(target.dataset.open, target);
        if (target.hasAttribute("data-close")) return closeDialog(target.closest("dialog"));
        if (target.hasAttribute("data-play")) return clock.playing ? pause() : play();
        if (target.hasAttribute("data-restart")) { pause(); return seek(0); }
        if (target.dataset.seek) return seek(clock.time(now()) + Number(target.dataset.seek));
        if (target.dataset.jump) { closeAll(); seek(Number(target.dataset.jump)); announce("Scripted replay moved to " + target.dataset.jump + " seconds."); }
        if (target.dataset.popupTab) popupTab(target.dataset.popupTab);
        if (target.hasAttribute("data-use-preview")) {
            closeAll(); pause(); clock.speed(preview.rate, now()); seek(preview.time);
            announce("Scripted preview applied. No AI or backend call was made.");
        }
    });
    document.addEventListener("input", event => {
        if (event.target.matches("[data-scrub]")) seek(Number(event.target.value));
    });
    document.addEventListener("change", event => {
        if (event.target.matches("[data-numbers]")) document.body.classList.toggle("hide-numbers", !event.target.checked);
    });
    $(".composer-form").addEventListener("submit", event => {
        event.preventDefault();
        const form = event.currentTarget;
        preview = { time: Number(form.elements.focus.value), rate: Number(form.elements.pace.value) };
        $("[data-preview-summary]").textContent = `Local scripted scene from ${preview.time.toFixed(1)} seconds at ${preview.rate}×. Arsenal vs Manchester City; no new match data was generated.`;
        $(".preview-result").hidden = false;
    });
    document.addEventListener("pointerdown", event => {
        if (design === "focus" && playerDialog.open && !playerDialog.contains(event.target) && !event.target.closest("[data-player]"))
            playerDialog.close();
    });
    document.addEventListener("keydown", event => {
        const player = event.target.closest?.("[data-player]");
        if (player && ["Enter", " "].includes(event.key)) { event.preventDefault(); openPlayer(player.dataset.player, player); return; }
        if (design === "focus" && playerDialog.open && event.key === "Escape") { playerDialog.close(); return; }
        const tab = event.target.closest?.("[data-popup-tab]");
        if (tab && ["ArrowLeft", "ArrowRight"].includes(event.key)) { event.preventDefault(); const name = tab.dataset.popupTab === "match" ? "story" : "match"; popupTab(name); $(`[data-popup-tab="${name}"]`).focus(); return; }
        if (dialogs.some(d => d.open) || event.repeat || event.metaKey || event.ctrlKey || event.altKey ||
            event.target.closest?.("input,select,textarea,button,a,[role=button],[contenteditable=true]")) return;
        if (event.key === " ") { event.preventDefault(); clock.playing ? pause() : play(); }
        if (event.key === "ArrowLeft" || event.key === "ArrowRight") { event.preventDefault(); seek(clock.time(now()) + (event.key === "ArrowLeft" ? -1 : 1)); }
    });
    document.addEventListener("visibilitychange", () => { if (document.hidden) pause(); });
    window.addEventListener("resize", () => {
        if (design !== "matchday" || !playerDialog.open) return;
        const box = playerDialog.getBoundingClientRect();
        playerDialog.style.setProperty("--popup-x", `${Math.max(16, Math.min(box.left, innerWidth - box.width - 16))}px`);
        playerDialog.style.setProperty("--popup-y", `${Math.max(16, Math.min(box.top, innerHeight - box.height - 16))}px`);
    });
    window.addEventListener("pagehide", pause);
    sprites.forEach((group, id) => {
        group.classList.toggle("selected", id === selected);
        group.setAttribute("aria-pressed", String(id === selected));
    });
    paint(true);
}());
