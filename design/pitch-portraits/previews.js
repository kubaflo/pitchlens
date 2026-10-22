/* Design-only snapshots. No playback, model calls, imports, fetch or production renderer changes. */
(function () {
    "use strict";
    const ASSETS = "../../src/PitchLens.App/wwwroot/";
    const concepts = [
        { id: "01", slug: "photo-tokens", name: "Photo tokens", line: "Every player. A familiar face.",
            note: "Immediate recognition, but close duels can make the circles overlap." },
        { id: "02", slug: "cut-out-players", name: "Cut-out players", line: "More football character. Less card.",
            note: "The transparent bust feels more like a player; tall silhouettes need more space." },
        { id: "03", slug: "portrait-tiles", name: "Portrait tiles", line: "A compact, structured alternative.",
            note: "Numbers and team rails stay clear. The rectangular silhouettes feel more diagrammatic." },
        { id: "04", slug: "focus-first", name: "Focus first", line: "Faces where your attention already is.",
            note: "Recommended: keep the wide pitch quiet; show faces for the selected and ball-near players." },
        { id: "05", slug: "smart-zoom", name: "Smart zoom", line: "Numbers wide. People up close.",
            note: "Best for dense play: portraits appear in detail view. The extra camera state needs a clear cue." }
    ];
    // Same curated layout in every concept, based on the user's mint/red reference. Not generated match evidence.
    const players = [
        ["arsenal-raya", "David Raya", 1, 11.5, 31.5],
        ["arsenal-timber", "Jurrien Timber", 12, 41.2, 7.2],
        ["arsenal-saliba", "William Saliba", 2, 35.8, 25.6],
        ["arsenal-gabriel", "Gabriel", 6, 36.8, 40.9],
        ["arsenal-calafiori", "Riccardo Calafiori", 33, 47.4, 58.3],
        ["arsenal-zubimendi", "Martin Zubimendi", 36, 49.1, 32.5],
        ["arsenal-rice", "Declan Rice", 41, 56.4, 39.6],
        ["arsenal-odegaard", "Martin Odegaard", 8, 57.1, 19.9],
        ["arsenal-saka", "Bukayo Saka", 7, 60.3, 5.6],
        ["arsenal-gyokeres", "Viktor Gyokeres", 14, 66.7, 31.7],
        ["arsenal-martinelli", "Gabriel Martinelli", 11, 67.2, 59.5],
        ["liverpool-alisson", "Alisson Becker", 1, 99.1, 34.6],
        ["liverpool-frimpong", "Jeremie Frimpong", 30, 80.2, 57.8],
        ["liverpool-van-dijk", "Virgil van Dijk", 4, 83.5, 41.9],
        ["liverpool-gomez", "Joe Gomez", 2, 82.1, 24.5],
        ["liverpool-kerkez", "Milos Kerkez", 6, 79.2, 8.2],
        ["liverpool-gravenberch", "Ryan Gravenberch", 38, 70.4, 33.8],
        ["liverpool-mac-allister", "Alexis Mac Allister", 10, 67.1, 46.1],
        ["liverpool-szoboszlai", "Dominik Szoboszlai", 8, 66, 19.8],
        ["liverpool-chiesa", "Federico Chiesa", 14, 56.4, 58.9],
        ["liverpool-isak", "Alexander Isak", 9, 55.4, 31.8],
        ["liverpool-gakpo", "Cody Gakpo", 18, 57.3, 7.7]
    ].map(([id, name, number, x, y]) => Object.freeze({
        id, name, number, x, y, team: id.startsWith("arsenal-") ? "arsenal" : "liverpool"
    }));
    const ball = Object.freeze({ x: 56.5, y: 39.6 });
    const wide = Object.freeze({ x: -4, y: -4, width: 113, height: 76 });
    const initialSelected = "liverpool-alisson";
    const filename = c => `${c.id}-${c.slug}.html`;
    const photo = p => `${ASSETS}images/players/${p.id}.png`;
    const teamName = p => p.team === "arsenal" ? "Arsenal" : "Liverpool";
    const clamp = (n, min, max) => Math.max(min, Math.min(max, n));
    function camera(selected) {
        const width = 56, height = width * 76 / 113;
        return { x: clamp(selected.x - width / 2, -4, 109 - width),
            y: clamp(selected.y - height / 2, -4, 72 - height), width, height };
    }
    function coordinates(p, bounds) {
        return { x: (p.x - bounds.x) / bounds.width * 100, y: (p.y - bounds.y) / bounds.height * 100,
            visible: p.x >= bounds.x && p.x <= bounds.x + bounds.width &&
                p.y >= bounds.y && p.y <= bounds.y + bounds.height };
    }
    function showsPhoto(concept, p, selected, zoom) {
        if (concept === "04") return p.id === selected || Math.hypot(p.x - ball.x, p.y - ball.y) <= 9;
        if (concept === "05") return zoom;
        return true;
    }
    function pitchSvg(id) {
        return `<svg id="${id}" class="pitch-art" viewBox="-4 -4 113 76" aria-hidden="true">
            <defs><clipPath id="field-${id}"><rect width="105" height="68"/></clipPath></defs>
            <rect x="-30" y="-30" width="170" height="130" fill="#284d3b"/>
            <rect width="105" height="68" fill="#4d7658"/>
            <g fill="#527c5d" clip-path="url(#field-${id})">${[0, 21, 42, 63, 84].map(x => `<rect x="${x}" width="10.5" height="68"/>`).join("")}</g>
            <g fill="none" stroke="#d6e4d2" stroke-width=".24" opacity=".92">
                <rect width="105" height="68"/><path d="M52.5 0V68"/><circle cx="52.5" cy="34" r="9.15"/>
                <path d="M0 13.84H16.5V54.16H0M105 13.84H88.5V54.16H105M0 24.84H5.5V43.16H0M105 24.84H99.5V43.16H105"/>
                <path d="M16.5 26.7A9.15 9.15 0 0 1 16.5 41.3M88.5 26.7A9.15 9.15 0 0 0 88.5 41.3"/>
                <path d="M0 30.34H-2.8V37.66H0M105 30.34H107.8V37.66H105"/>
            </g><g fill="#e8f0e1"><circle cx="52.5" cy="34" r=".35"/><circle cx="11" cy="34" r=".25"/><circle cx="94" cy="34" r=".25"/></g>
        </svg>`;
    }
    function gallery(doc) {
        doc.body.innerHTML = `<header class="topbar"><a class="brand" href="index.html">PitchLens <span>Design lab</span></a><span class="preview-pill">Offline / Design only</span></header>
            <main class="gallery"><div class="eyebrow">On-pitch portraits / Five directions</div><h1>Put a face<br>to the play.</h1>
            <p class="intro">Same 22 players. Same positions. Same selection.<br>Five ways to add real faces without losing the football.</p>
            <div class="recommend"><span>Our pick</span><strong>04 / Focus first</strong><p>Keep the wide view readable. Bring in faces when they help.</p></div>
            <div class="gallery-grid">${concepts.map(c => `<article class="gallery-card"><a href="${filename(c)}" class="thumbnail" aria-label="Open ${c.id} ${c.name}">
                <iframe src="${filename(c)}?embed=1" title="${c.name} actual on-pitch preview" tabindex="-1" aria-hidden="true" loading="lazy"></iframe></a>
                <div class="card-copy"><span class="eyebrow">${c.id} / ${c.id === "04" ? "Recommended" : "Portrait study"}</span><h2><a href="${filename(c)}">${c.name}</a></h2><p>${c.line}</p></div></article>`).join("")}</div>
            <footer class="disclosure">Synthetic layout, not a real match or fresh AI result. Official portraits already bundled in this project; third-party rights retained under user-confirmed non-commercial permission. No network, generation or app changes.</footer></main>`;
    }
    function detail(doc, win, id) {
        const concept = concepts.find(c => c.id === id);
        if (!concept) throw new Error("Unknown design concept.");
        const params = new URLSearchParams(win.location.search);
        const embedded = params.get("embed") === "1";
        doc.body.className = embedded ? "detail embedded" : "detail";
        doc.body.dataset.treatment = id;
        const chosen = players.find(p => p.id === params.get("selected"))?.id || initialSelected;
        const state = { selected: chosen, zoom: false, compact: false };
        doc.body.innerHTML = `<header class="topbar"><a class="brand" href="index.html">PitchLens <span>On-pitch portraits</span></a>
            <nav class="switcher" aria-label="Five concepts">${concepts.map(c => `<a id="switch-${c.id}" href="${filename(c)}" ${c.id === id ? 'aria-current="page"' : ""}>${c.id}</a>`).join("")}</nav><a class="back-link" href="index.html">All five →</a></header>
            <main class="study"><div class="study-title"><div><span class="eyebrow">Direction ${id} / 05</span><h1>${concept.name}</h1></div><span class="preview-pill">Synthetic / Design only</span></div>
            <div class="fixture"><div><img src="${ASSETS}images/arsenal_crest.png" alt=""><strong>Arsenal</strong><span>v</span><strong>Liverpool</strong><img src="${ASSETS}images/liverpool_crest.svg" alt=""></div>
                <div class="team-key"><span class="arsenal-key">Arsenal</span><span class="liverpool-key">Liverpool</span></div></div>
            <section id="pitch-shell" class="pitch-shell" aria-label="Synthetic pitch portrait study">
                <div id="pitch" class="pitch">${pitchSvg("pitch-art")}<div id="player-layer" class="player-layer"></div><span id="ball" class="ball" role="img" aria-label="Ball"></span>
                    <div class="pitch-note">SYNTHETIC PREVIEW <span>Same positions in all five</span></div>
                    <div id="zoom-glimpse" class="zoom-glimpse" hidden aria-hidden="true"><span>Photo detail</span><div class="glimpse-pitch">${pitchSvg("glimpse-art")}<div id="glimpse-players"></div></div></div>
                </div></section>
            <div class="study-toolbar"><div class="segmented" aria-label="Preview width"><button id="wide-layout" aria-pressed="true">Wide layout</button><button id="compact-layout" aria-pressed="false">Compact layout</button></div>
                ${id === "05" ? '<div class="segmented" aria-label="Camera detail"><button id="wide-camera" aria-pressed="true">Wide · numbers</button><button id="detail-camera" aria-pressed="false">Detail · portraits</button></div>' : ""}
                <label class="player-picker">Select player<select id="player-picker">${players.map(p => `<option value="${p.id}">${p.name} · ${teamName(p)} #${p.number}</option>`).join("")}</select></label></div>
            <div class="study-bottom"><div class="selection" aria-live="polite"><img id="selected-image" alt="" width="68" height="86"><div><span class="eyebrow">Selected player</span><strong id="selected-name"></strong><span id="selected-club"></span></div><span class="selection-cue">Click a player or use Tab + Enter</span></div>
                <div class="tradeoff"><span class="eyebrow">${id === "04" ? "Why we'd start here" : "The trade-off"}</span><p>${concept.note}</p></div></div>
            <p id="view-status" class="view-status" aria-live="polite"></p><footer class="disclosure">Static design study only. Original synthetic layout; official roster identities and existing local portraits. No playback, AI call, live metric or production app change. Press 1–5 to compare.</footer></main>`;
        const layer = doc.getElementById("player-layer");
        function marker(p, bounds, mini) {
            const node = doc.createElement(mini ? "span" : "button");
            const at = coordinates(p, bounds);
            const hasPhoto = mini || showsPhoto(id, p, state.selected, state.zoom);
            node.className = `marker team-${p.team} treatment-${mini ? "01" : id}${hasPhoto ? " has-photo" : ""}${p.id === state.selected ? " selected" : ""}`;
            node.dataset.playerId = p.id;
            node.hidden = !at.visible;
            node.style.setProperty("--x", `${at.x}%`); node.style.setProperty("--y", `${at.y}%`);
            if (!mini) {
                node.type = "button"; node.setAttribute("aria-label", `Select ${p.name}, ${teamName(p)}, shirt ${p.number}`);
                node.setAttribute("aria-pressed", p.id === state.selected ? "true" : "false");
                node.title = `${p.name} · ${teamName(p)} #${p.number}`;
                node.addEventListener("click", () => select(p.id, true));
            }
            const ground = doc.createElement("span"); ground.className = "ground"; node.appendChild(ground);
            const frame = doc.createElement("span"); frame.className = "portrait-frame";
            if (hasPhoto) {
                const img = doc.createElement("img"); img.src = photo(p); img.alt = ""; img.draggable = false;
                img.addEventListener("error", () => { img.hidden = true; node.classList.add("no-photo"); });
                frame.appendChild(img);
            }
            node.appendChild(frame);
            const number = doc.createElement("span"); number.className = "shirt-number"; number.textContent = String(p.number); node.appendChild(number);
            return node;
        }
        function paint() {
            const selected = players.find(p => p.id === state.selected);
            const bounds = id === "05" && state.zoom ? camera(selected) : wide;
            const viewBox = b => `${b.x} ${b.y} ${b.width} ${b.height}`;
            doc.getElementById("pitch-art").setAttribute("viewBox", viewBox(bounds));
            layer.replaceChildren(...players.map(p => marker(p, bounds, false)));
            const point = coordinates(ball, bounds), ballNode = doc.getElementById("ball");
            ballNode.style.setProperty("--x", `${point.x}%`); ballNode.style.setProperty("--y", `${point.y}%`); ballNode.hidden = !point.visible;
            doc.getElementById("selected-image").src = photo(selected);
            doc.getElementById("selected-name").textContent = selected.name;
            doc.getElementById("selected-club").textContent = `${teamName(selected)} · #${selected.number}`;
            doc.getElementById("player-picker").value = state.selected;
            for (const c of concepts) doc.getElementById(`switch-${c.id}`).href = `${filename(c)}?selected=${state.selected}`;
            doc.getElementById("pitch-shell").classList.toggle("compact", state.compact);
            doc.getElementById("pitch").classList.toggle("is-zoomed", state.zoom);
            doc.getElementById("wide-layout").setAttribute("aria-pressed", String(!state.compact));
            doc.getElementById("compact-layout").setAttribute("aria-pressed", String(state.compact));
            const glimpse = doc.getElementById("zoom-glimpse");
            glimpse.hidden = id !== "05" || state.zoom;
            if (id === "05") {
                const close = camera(selected);
                doc.getElementById("glimpse-art").setAttribute("viewBox", viewBox(close));
                doc.getElementById("glimpse-players").replaceChildren(...players.map(p => marker(p, close, true)));
                doc.getElementById("wide-camera").setAttribute("aria-pressed", String(!state.zoom));
                doc.getElementById("detail-camera").setAttribute("aria-pressed", String(state.zoom));
            }
            const faces = players.filter(p => coordinates(p, bounds).visible && showsPhoto(id, p, state.selected, state.zoom)).length;
            doc.getElementById("view-status").textContent = id === "05" ?
                state.zoom ? `Detail camera · ${faces} visible portraits at unchanged field positions. Return wide to see all 22.` :
                    "Wide camera · all 22 numbered markers. The small inset previews the same selected area with portraits." :
                `${faces} on-pitch portraits · 22 selectable players · identical ball and positions across concepts.`;
        }
        function select(pid, focus) {
            if (!players.some(p => p.id === pid)) throw new Error("Unknown preview identity.");
            state.selected = pid; paint();
            if (focus) [...layer.children].find(n => n.dataset.playerId === pid)?.focus({ preventScroll: true });
        }
        doc.getElementById("wide-layout").addEventListener("click", () => { state.compact = false; paint(); });
        doc.getElementById("compact-layout").addEventListener("click", () => { state.compact = true; paint(); });
        doc.getElementById("player-picker").addEventListener("change", e => select(e.target.value, false));
        if (id === "05") {
            doc.getElementById("wide-camera").addEventListener("click", () => { state.zoom = false; paint(); });
            doc.getElementById("detail-camera").addEventListener("click", () => { state.zoom = true; paint(); });
        }
        doc.addEventListener("keydown", e => {
            if (e.ctrlKey || e.metaKey || e.altKey || ["INPUT", "SELECT", "TEXTAREA"].includes(e.target.tagName)) return;
            if (/^[1-5]$/.test(e.key)) {
                e.preventDefault(); win.location.href = `${filename(concepts[Number(e.key) - 1])}?selected=${state.selected}`;
            }
        });
        paint();
        return { state, select, paint, players, ball, concept };
    }
    const api = { concepts, players, ball, wide, camera, coordinates, showsPhoto, filename, photo, gallery, detail, initialSelected };
    if (typeof module !== "undefined") module.exports = api;
    if (typeof document !== "undefined") {
        if (document.body.dataset.concept) window.PitchPortraitPreview = detail(document, window, document.body.dataset.concept);
        else gallery(document);
    }
}());
