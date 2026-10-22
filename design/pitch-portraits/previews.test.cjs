const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const preview = require("./previews.js");

function documentStub() {
    const doc = { nodes: new Map(), handlers: new Map(), activeElement: null,
        getElementById(id) { const n=this.nodes.get(id);assert.ok(n,`Real scaffold must declare ${id}`);return n; },
        createElement(tag) { return new Element(tag); },
        addEventListener(name, handler) { this.handlers.set(name,handler); }
    };
    class Element {
        constructor(tag) {
            this.tagName=tag.toUpperCase();this.children=[];this.handlers=new Map();this.attributes={};this.dataset={};this.hidden=false;this.className="";
            this.style={setProperty:(name,value)=>{this.style[name]=value;}};
            this.classList={
                add:name=>{this.className+=" "+name;},
                contains:name=>this.className.split(/\s+/).includes(name),
                toggle:(name,on)=>{const names=new Set(this.className.split(/\s+/).filter(Boolean));on?names.add(name):names.delete(name);this.className=[...names].join(" ");}
            };
        }
        set innerHTML(value) {
            this.html=value;
            for(const match of value.matchAll(/id="([^"]+)"/g))doc.nodes.set(match[1],new Element("div"));
        }
        get innerHTML(){return this.html;}
        appendChild(child){this.children.push(child);return child;}
        replaceChildren(...children){this.children=children;}
        setAttribute(name,value){this.attributes[name]=String(value);}
        addEventListener(name,handler){this.handlers.set(name,handler);}
        focus(){doc.activeElement=this;}
        click(){this.handlers.get("click")?.({target:this});}
    }
    doc.body=new Element("body");doc.body.dataset={};
    return doc;
}
const descendants = node => node.children.flatMap(child=>[child,...descendants(child)]);
const pngHash = file => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

test("all five local pages and gallery reference only existing reviewed assets; no fetch/module/server dependency",()=>{
    const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,"../../src/PitchLens.Generation/Resources/player-portraits.json"),"utf8"));
    assert.equal(preview.players.length,22);
    assert.equal(new Set(preview.players.map(p=>p.id)).size,22);
    for(const team of ["arsenal","liverpool"])assert.equal(preview.players.filter(p=>p.team===team).length,11);
    for(const p of preview.players) {
        const entry=manifest.entries.find(e=>e.playerId===p.id);
        assert.equal(entry.name,p.name);assert.equal(entry.teamId,p.team);
        const file=path.resolve(__dirname,preview.photo(p));
        assert.ok(fs.existsSync(file));assert.equal(pngHash(file),entry.sha256);
        assert.equal(fs.readFileSync(file).readUInt32BE(16),entry.width);
    }
    for(const c of preview.concepts) {
        const html=fs.readFileSync(path.join(__dirname,preview.filename(c)),"utf8");
        assert.ok(html.includes(`data-concept="${c.id}"`));
        assert.ok(html.includes('src="previews.js"'));assert.ok(html.includes('href="previews.css"'));
        assert.ok(!html.includes('type="module"'));assert.ok(!html.includes("https://"));
    }
    const css=fs.readFileSync(path.join(__dirname,"previews.css"),"utf8");
    for(const m of css.matchAll(/url\("([^"]+)"\)/g))assert.ok(fs.existsSync(path.resolve(__dirname,m[1])));
    assert.ok(css.includes("prefers-reduced-motion"));
    const script=fs.readFileSync(path.join(__dirname,"previews.js"),"utf8");
    assert.ok(!/\bfetch\s*\(|\bXMLHttpRequest\b|\bimport\s*\(/.test(script));
    const doc=documentStub();preview.gallery(doc);
    assert.equal([...doc.body.innerHTML.matchAll(/<iframe /g)].length,5);
    for(const c of preview.concepts)assert.ok(doc.body.innerHTML.includes(`src="${preview.filename(c)}?embed=1"`));
    const detail=documentStub();preview.detail(detail,{location:{search:""}},"01");
    for(const match of detail.body.innerHTML.matchAll(/(?:src|href)="([^"]+)"/g)) {
        const file=match[1].split("?")[0];
        assert.ok(fs.existsSync(path.resolve(__dirname,file)),`Local preview reference exists: ${file}`);
    }
});

for(const c of preview.concepts)test(`${c.id} renders 22 exact markers/ball/initial selection; click, keyboard and compact state work`,()=>{
    const doc=documentStub(),win={location:{search:"",href:""}},api=preview.detail(doc,win,c.id);
    const layer=doc.getElementById("player-layer");
    assert.equal(layer.children.length,22);assert.equal(layer.children.filter(n=>!n.hidden).length,22);
    assert.equal(api.state.selected,"liverpool-alisson");
    assert.equal(layer.children.filter(n=>n.classList.contains("selected")).length,1);
    for(const [i,n] of layer.children.entries()) {
        const p=preview.players[i],at=preview.coordinates(p,preview.wide);
        assert.equal(n.dataset.playerId,p.id);assert.equal(n.style["--x"],`${at.x}%`);assert.equal(n.style["--y"],`${at.y}%`);
        assert.equal(n.tagName,"BUTTON");assert.ok(n.attributes["aria-label"].includes(p.name));
        assert.equal(n.children.at(-1).textContent,String(p.number));
    }
    const faces=layer.children.filter(n=>n.classList.contains("has-photo"));
    assert.equal(faces.length,["01","02","03"].includes(c.id)?22:c.id==="04"?3:0);
    assert.equal(descendants(layer).filter(n=>n.tagName==="IMG").length,faces.length);
    assert.equal(doc.getElementById("ball").hidden,false);
    assert.equal(doc.getElementById("selected-name").textContent,"Alisson Becker");
    layer.children.find(n=>n.dataset.playerId==="arsenal-saka").click();
    assert.equal(api.state.selected,"arsenal-saka");assert.equal(doc.getElementById("selected-name").textContent,"Bukayo Saka");
    assert.equal(doc.activeElement.dataset.playerId,"arsenal-saka");
    assert.equal(doc.getElementById("switch-02").href,"02-cut-out-players.html?selected=arsenal-saka");
    const positions=layer.children.map(n=>[n.style["--x"],n.style["--y"]]);
    doc.getElementById("compact-layout").click();assert.equal(api.state.compact,true);
    assert.deepEqual(layer.children.map(n=>[n.style["--x"],n.style["--y"]]),positions);
    assert.equal(doc.getElementById("compact-layout").attributes["aria-pressed"],"true");
    let prevented=false;doc.handlers.get("keydown")({key:"5",target:{tagName:"BODY"},preventDefault(){prevented=true;}});
    assert.equal(prevented,true);assert.equal(win.location.href,"05-smart-zoom.html?selected=arsenal-saka");
    const image=descendants(layer).find(n=>n.tagName==="IMG");
    if(image){image.handlers.get("error")();assert.equal(image.hidden,true);}
});

test("smart zoom demonstrates real portrait detail and a wide numbered state without changing source positions",()=>{
    const doc=documentStub(),api=preview.detail(doc,{location:{search:""}},"05");
    assert.equal(doc.getElementById("zoom-glimpse").hidden,false);
    assert.equal(doc.getElementById("glimpse-players").children.length,22);
    assert.ok(descendants(doc.getElementById("glimpse-players")).filter(n=>n.tagName==="IMG").length>0);
    doc.getElementById("detail-camera").click();
    assert.equal(api.state.zoom,true);assert.equal(doc.getElementById("zoom-glimpse").hidden,true);
    const layer=doc.getElementById("player-layer"),visible=layer.children.filter(n=>!n.hidden);
    assert.ok(visible.length>1&&visible.length<22);
    assert.ok(visible.every(n=>n.classList.contains("has-photo")));
    assert.equal(doc.getElementById("ball").hidden,false);
    const bounds=preview.camera(preview.players.find(p=>p.id===api.state.selected));
    assert.equal(doc.getElementById("pitch-art").attributes.viewBox,`${bounds.x} ${bounds.y} ${bounds.width} ${bounds.height}`);
    assert.equal(layer.children.find(n=>n.dataset.playerId==="liverpool-alisson").hidden,false);
    doc.getElementById("wide-camera").click();assert.equal(layer.children.filter(n=>!n.hidden).length,22);
    assert.equal(api.state.selected,preview.initialSelected);
});

test("every local camera uses the same aspect ratio and an unknown selection cannot load arbitrary image paths",()=>{
    for(const player of preview.players) {
        const camera=preview.camera(player);
        assert.ok(Math.abs(camera.width/camera.height-113/76)<1e-10);
        assert.equal(preview.coordinates(player,camera).visible,true);
    }
    const doc=documentStub(),api=preview.detail(doc,{location:{search:"?selected=https://example.invalid/fake.png"}},"01");
    assert.equal(api.state.selected,preview.initialSelected);
    assert.throws(()=>api.select("not-a-player"),/Unknown/);
});
