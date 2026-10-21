export function validateLens(view, scenario) {
    const w = view?.window;
    const fixture = scenario.teams.map(t => t.id).join("/");
    if (!w || w.revision !== scenario.revision || w.fixture !== fixture ||
        !Number.isFinite(w.start) || !Number.isFinite(w.cutoff) || w.start < 0 || w.cutoff < w.start || w.cutoff > scenario.duration)
        throw new Error("Lens window does not match this observed fixture.");
    if (!["connections", "movement"].includes(view.mode) || !Array.isArray(view.nodes) || view.nodes.length > 22 ||
        !Array.isArray(view.edges) || view.edges.length > 90 || !Array.isArray(view.cells) || view.cells.length > 28)
        throw new Error("Invalid pitch lens data.");
    const point = p => Number.isFinite(p.x) && Number.isFinite(p.y) && p.x >= 0 && p.x <= 105 && p.y >= 0 && p.y <= 68;
    if (view.mode === "connections") {
        if (!scenario.teams.some(t => t.id === view.subjectId) || view.nodes.some(n => !point(n) ||
            !scenario.players.some(p => p.id === n.playerId && p.teamId === view.subjectId)) ||
            view.edges.some(e => !Number.isSafeInteger(e.count) || e.count < 1 ||
                !view.nodes.some(n => n.playerId === e.from) || !view.nodes.some(n => n.playerId === e.to)))
            throw new Error("Connection nodes or counts are invalid.");
    } else if (!scenario.players.some(p => p.id === view.subjectId) ||
        view.cells.length !== 28 || view.cells.some((c, i) => c.index !== i || !point(c) ||
            c.width !== 15 || c.height !== 17 || !Number.isFinite(c.seconds) || c.seconds < 0) ||
        Math.abs(view.cells.reduce((sum,c) => sum+c.seconds,0)-(w.cutoff-w.start)) > 1e-6)
        throw new Error("Movement cells do not conserve observed player-time.");
    return view;
}

// Field coordinates only: the host applies the same camera/density transform as the players.
export function drawLens(c, view, players) {
    if (view.mode === "movement") {
        const max = Math.max(0,...view.cells.map(cell => cell.seconds));
        for (const cell of view.cells) {
            if (cell.seconds > 0 && max > 0) {
                c.fillStyle = `rgba(255,207,84,${.08+.4*cell.seconds/max})`;
                c.fillRect(cell.x,cell.y,cell.width,cell.height);
            }
            if (cell.index === view.selectedCell) {
                c.strokeStyle = "#05f0ff"; c.lineWidth = .6;
                c.strokeRect(cell.x+.3,cell.y+.3,cell.width-.6,cell.height-.6);
            }
        }
        return;
    }
    const nodes = new Map(view.nodes.map(n => [n.playerId,n]));
    for (const edge of view.edges) {
        const a=nodes.get(edge.from),b=nodes.get(edge.to);
        const dx=b.x-a.x,dy=b.y-a.y,length=Math.hypot(dx,dy);
        if(length<.1) {
            c.strokeStyle=edge.id===view.selectedEdgeId?"#05f0ff":"rgba(237,233,255,.65)";
            c.lineWidth=Math.min(1.5,.25+edge.count*.2);
            c.beginPath();c.arc(a.x+3,a.y-3,3,0,Math.PI*1.8);c.stroke();
            c.fillStyle="#fff";c.font="600 2px Barlow, sans-serif";c.fillText(String(edge.count),a.x+6,a.y-6);
            continue;
        }
        const ux=dx/length,uy=dy/length,offset=1;
        const inset=Math.min(2.5,length/4);
        const x1=a.x+ux*inset-uy*offset,y1=a.y+uy*inset+ux*offset;
        const x2=b.x-ux*inset-uy*offset,y2=b.y-uy*inset+ux*offset;
        c.strokeStyle=edge.id===view.selectedEdgeId?"#05f0ff":"rgba(237,233,255,.65)";
        c.lineWidth=Math.min(1.5,.25+edge.count*.2);
        c.beginPath();c.moveTo(x1,y1);c.lineTo(x2,y2);
        c.moveTo(x2-ux*2-uy,y2-uy*2+ux);c.lineTo(x2,y2);c.lineTo(x2-ux*2+uy,y2-uy*2-ux);c.stroke();
        c.fillStyle="#fff";c.font="600 2px Barlow, sans-serif";c.textAlign="center";c.textBaseline="middle";
        c.fillText(String(edge.count),(x1+x2)/2-uy*1.5,(y1+y2)/2+ux*1.5);
    }
    for(const node of view.nodes) {
        c.strokeStyle="#ded1f1";c.lineWidth=.4;c.beginPath();c.arc(node.x,node.y,2.3,0,Math.PI*2);c.stroke();
        c.fillStyle="#eee3ff";c.font="600 1.6px Barlow, sans-serif";c.textAlign="center";c.textBaseline="middle";
        c.fillText(`H${players.find(p=>p.id===node.playerId).number}`,node.x,node.y);
    }
}
