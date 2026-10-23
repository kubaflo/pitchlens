(() => {
    "use strict";
    const $ = selector => document.querySelector(selector);
    const $$ = selector => [...document.querySelectorAll(selector)];
    const design = document.body.dataset.design;
    const pitch = $("[data-pitch]");
    const ns = "http://www.w3.org/2000/svg";
    const svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "-3 -3 111 74");
    svg.setAttribute("role", "img");
    svg.setAttribute("aria-label", "Illustrative synthetic pitch with 22 numbered players. Mock animation, not a generated match.");
    svg.innerHTML = '<defs><pattern id="grass" width="21" height="68" patternUnits="userSpaceOnUse"><rect width="10.5" height="68" fill="#30674d"/><rect x="10.5" width="10.5" height="68" fill="#346f53"/></pattern></defs><rect width="105" height="68" fill="url(#grass)"/><g stroke="#c2d7c7" stroke-width=".22" fill="none"><rect x=".2" y=".2" width="104.6" height="67.6"/><path d="M52.5 0V68M0 13.84H16.5V54.16H0M105 13.84H88.5V54.16H105M0 24.84H5.5V43.16H0M105 24.84H99.5V43.16H105"/><circle cx="52.5" cy="34" r="9.15"/></g><circle cx="52.5" cy="34" r=".4" fill="#c2d7c7"/>';
    const points = [[5,34],[25,12],[21,28],[21,43],[25,58],[43,18],[39,34],[43,51],[66,12],[77,35],[66,56],[100,34],[85,12],[86,28],[86,44],[85,58],[69,20],[66,34],[69,51],[51,10],[50,57],[42,36]];
    points.forEach(([x,y], index) => {
        const group = document.createElementNS(ns, "g");
        group.setAttribute("transform", `translate(${x} ${y})`);
        const circle = document.createElementNS(ns, "circle");
        circle.setAttribute("r", "1.9"); circle.setAttribute("fill", index < 11 ? "#db4457" : "#f1ede4");
        circle.setAttribute("stroke", "#172d21"); circle.setAttribute("stroke-width", ".2");
        const text = document.createElementNS(ns, "text");
        text.setAttribute("fill", index < 11 ? "white" : "#18261e");
        text.setAttribute("style", "font-size:2px");
        text.textContent = String(index % 11 + 1);
        group.append(circle,text); svg.append(group);
    });
    const ball = document.createElementNS(ns, "circle");
    ball.setAttribute("r", ".8"); ball.setAttribute("fill", "white"); ball.setAttribute("class", "ball"); svg.append(ball); pitch.append(svg);
    const normalControls = '<div class="transport"><button class="play" data-play aria-pressed="false">Play</button><input data-seek type="range" min="0" max="300" value="236" aria-label="Mock replay time in seconds"><output data-time aria-label="Replay position">03:56</output>' +
        (design === "two-views" ? '' : '<button class="primary" data-explain title="Scripted preview only; no AI calls">Explain this moment</button>') + '</div>';
    $("[data-transport]").innerHTML = design === "moments"
        ? '<div class="transport"><div class="moment-nav"><button class="quiet" data-previous>← Previous</button><button class="play" data-play aria-pressed="false">Play</button><button class="quiet" data-next>Next →</button></div><button class="primary" data-explain>Why this moment?</button></div>'
        : normalControls;
    document.body.insertAdjacentHTML("beforeend", '<dialog class="settings" aria-labelledby="settings-title"><header><h2 id="settings-title">Keep it comfortable.</h2><button class="quiet" data-close-settings>Close</button></header><div class="settings-body"><label>Text size<select data-size><option value="100">100% · Standard</option><option value="125">125%</option><option value="150">150%</option><option value="175">175%</option><option value="200">200%</option></select></label><p class="caption">Changes this HTML preview only. Nothing is saved to the app. Pitch markers stay the same size.</p><details><summary>Where the other features go</summary><ul><li>Players &amp; camera</li><li>Match analysis &amp; statistics</li><li>Director’s Cut</li><li>New match &amp; source settings</li></ul><p class="caption">Proposed menu grouping, not connected features. The main screen keeps only replay and explanation.</p></details><details><summary>How production explanation would work</summary><p>Three specialists, in order. Up to 3 responses, 12 evidence reads and a 270-second whole-run deadline. The real app keeps its existing acceptance and cancellation guards. This mock sends no requests.</p></details><a href="index.html">Compare all five designs</a></div></dialog>');
    const dialog = $(".settings");
    let settingsOpener;
    const answer = $("[data-answer]");
    let time = 236, playing = false, frame = null, last = 0, moment = 2;
    const moments = [
        {time:180,title:"The move begins.",copy:"Start with the first pass, without a wall of statistics.",name:"The build-up"},
        {time:210,title:"The next pass opens it up.",copy:"Follow the connection that takes the move forward.",name:"The connection"},
        {time:236,title:"An opening. A finish.",copy:"Follow one short passage of play, then find out why it mattered.",name:"The finish"},
        {time:260,title:"A new passage of play.",copy:"Move on when you are ready, with one clear next step.",name:"The restart"},
        {time:300,title:"The final moment.",copy:"A quiet end to the replay, with the same simple controls.",name:"Full time"}
    ];
    const fan = "The ball reaches the scorer in space. A short final touch completes the move. The useful story is the sequence—not a dashboard full of numbers.";
    const analyst = "Look at the final pass and the receiver’s position before the finish. An explanation should connect its interpretation to those observed events, not invent pressure or expected-goal values.";
    function paint() {
        $$("[data-time]").forEach(node => { node.textContent = `${Math.floor(time / 60).toString().padStart(2,"0")}:${Math.floor(time % 60).toString().padStart(2,"0")}`; });
        $$("[data-seek]").forEach(node => { node.value = String(time); });
        const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        ball.setAttribute("cx", String(reduced ? 72 : 72 + Math.sin(time / 4) * 5));
        ball.setAttribute("cy", String(reduced ? 36 : 36 + Math.cos(time / 5) * 3));
    }
    function setPlaying(value) {
        playing = value; if (frame !== null) cancelAnimationFrame(frame); frame = null;
        $$("[data-play]").forEach(button => { button.textContent = playing ? "Pause" : "Play"; button.setAttribute("aria-pressed",String(playing)); });
        if (playing) { last = performance.now(); frame = requestAnimationFrame(tick); }
    }
    function tick(now) {
        if (!playing) return;
        time = Math.min(300,time + (now-last)/1000); last = now; paint();
        if (time === 300) setPlaying(false); else frame = requestAnimationFrame(tick);
    }
    function hideAnswer() {
        answer.hidden = true;
        if ($("[data-placeholder]")) $("[data-placeholder]").hidden = false;
        if ($("[data-watch-view]")) $("[data-watch-view]").hidden = false;
        $$("[data-watch]").forEach(button => { button.classList.add("selected"); button.setAttribute("aria-pressed","true"); });
        if (design === "two-views") $$("[data-explain]").forEach(button => { button.classList.remove("selected"); button.setAttribute("aria-pressed","false"); });
    }
    function explain() {
        setPlaying(false);
        answer.innerHTML = '<header><div><span class="kicker">Scripted design example · Not an agent result</span><h2>One move. One clear explanation.</h2></div><button class="back" data-hide>Back to match</button></header><p class="lead" data-copy></p><div class="view-buttons" aria-label="Explanation style"><button class="selected" data-copy-mode="fan" aria-pressed="true">Fan</button><button data-copy-mode="analyst" aria-pressed="false">Analyst</button></div><details><summary>How it works · 3 agents, in order</summary><ol class="team-list"><li><strong>Find the evidence</strong><span>The analyst reads what happened in this captured moment.</span></li><li><strong>Write the explanation</strong><span>The editor receives those findings and writes a short answer.</span></li><li><strong>Check the evidence</strong><span>The reviewer receives the final wording, re-reads every cited event and can reject it.</span></li></ol><p class="caption">Scripted preview only. No agents have run, and no review approval is being claimed. In the app, technical audit information stays out of the main reading flow.</p></details>';
        $("[data-copy]").textContent = fan;
        answer.hidden = false;
        if ($("[data-placeholder]")) $("[data-placeholder]").hidden = true;
        if ($("[data-watch-view]")) $("[data-watch-view]").hidden = true;
        $$("[data-watch]").forEach(button => { button.classList.remove("selected"); button.setAttribute("aria-pressed","false"); });
        if (design === "two-views") $$("[data-explain]").forEach(button => { button.classList.add("selected"); button.setAttribute("aria-pressed","true"); });
        answer.focus({preventScroll:true});
        answer.scrollIntoView({block:"nearest",behavior:"auto"});
    }
    function moveMoment(delta) {
        moment = Math.max(0,Math.min(moments.length-1,moment+delta));
        const value = moments[moment]; setPlaying(false); time = value.time; hideAnswer(); paint();
        $("[data-moment-count]").textContent = `${String(moment+1).padStart(2,"0")} / 05 · ${value.name}`;
        $("[data-moment-title]").textContent = value.title; $("[data-moment-copy]").textContent = value.copy;
        $("[data-previous]").disabled = moment === 0; $("[data-next]").disabled = moment === moments.length-1;
    }
    document.addEventListener("click",event => {
        const button = event.target.closest("button"); if (!button) return;
        if (button.hasAttribute("data-play")) { if (time === 300) time=0; setPlaying(!playing); }
        if (button.hasAttribute("data-explain")) explain();
        if (button.hasAttribute("data-hide") || button.hasAttribute("data-watch")) hideAnswer();
        if (button.hasAttribute("data-settings")) { settingsOpener=button; dialog.showModal(); }
        if (button.hasAttribute("data-close-settings")) dialog.close();
        if (button.hasAttribute("data-previous")) moveMoment(-1);
        if (button.hasAttribute("data-next")) moveMoment(1);
        if (button.dataset.copyMode) {
            $("[data-copy]").textContent = button.dataset.copyMode === "fan" ? fan : analyst;
            $$("[data-copy-mode]").forEach(node => { const selected=node===button; node.classList.toggle("selected",selected); node.setAttribute("aria-pressed",String(selected)); });
        }
    });
    document.addEventListener("input",event => {
        if (event.target.hasAttribute("data-seek")) { time=Number(event.target.value); paint(); }
    });
    document.addEventListener("change",event => {
        if (event.target.hasAttribute("data-size")) document.documentElement.style.setProperty("--reading",String(Number(event.target.value)/100));
    });
    dialog.addEventListener("close",() => settingsOpener?.focus());
    dialog.addEventListener("click",event => {
        if (event.target !== dialog) return;
        const box=dialog.getBoundingClientRect();
        if (event.clientX<box.left || event.clientX>box.right || event.clientY<box.top || event.clientY>box.bottom) dialog.close();
    });
    document.addEventListener("visibilitychange",() => { if (document.hidden) setPlaying(false); });
    window.addEventListener("pagehide",() => setPlaying(false));
    paint();
})();
