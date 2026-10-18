import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
import test from "node:test";

const directory = path.dirname(fileURLToPath(import.meta.url));
const read = relative => fs.readFileSync(path.join(directory, relative), "utf8");
const context = { window: {} };
vm.createContext(context);
vm.runInContext(read("assets/core.js"), context);
vm.runInContext(read("assets/demo-data.js"), context);
const Core = context.PitchLensMockCore;
const model = Core.prepare(context.window.PitchLensDemo);
const entries = ["01-matchday.html", "02-broadcast.html", "03-touchline.html", "04-focus.html", "05-studio.html"];

test("exactly five distinct entry pages plus the review gallery", () => {
    assert.deepEqual(fs.readdirSync(directory).filter(p => p.endsWith(".html")).sort(), [...entries, "index.html"]);
    const links = [...read("index.html").matchAll(/href="([^"]+\.html)"/g)].map(m => m[1]);
    assert.deepEqual(links, entries);
    const designs = new Set();
    for (const entry of entries) {
        const html = read(entry);
        designs.add(html.match(/data-design="([^"]+)"/)[1]);
        assert.match(html, /<title>[^<]+<\/title>/);
        assert.match(html, /<h1\b/);
        assert.match(html, /Design prototype/);
        assert.match(html, /Synthetic|synthetic/);
        assert.match(html, /data-pitch/);
        for (const action of ["roster", "story", "composer"])
            assert.ok(html.includes(`data-open="${action}"`), `${entry} exposes ${action}`);
        assert.doesNotMatch(html, /type=["']module["']|https?:\/\/[^"']+\.(?:js|css)/i);
    }
    assert.equal(designs.size, 5);
});

test("all local HTML and CSS assets resolve, including fonts and actual crests", () => {
    for (const entry of [...entries, "index.html"]) {
        for (const match of read(entry).matchAll(/(?:href|src)="([^"]+)"/g)) {
            if (/^(?:https?:|#)/.test(match[1])) continue;
            assert.ok(fs.existsSync(path.join(directory, match[1])), `${entry}: ${match[1]}`);
        }
    }
    for (const match of read("assets/mocks.css").matchAll(/url\(["']?([^"')]+)["']?\)/g))
        assert.ok(fs.existsSync(path.join(directory, "assets", match[1])), match[1]);
    for (const name of ["arsenal", "manchester-city"]) {
        const bytes = fs.readFileSync(path.join(directory, `assets/images/${name}.png`));
        assert.equal(bytes.subarray(1, 4).toString(), "PNG");
        assert.equal(bytes.readUInt32BE(16), 200);
        assert.equal(bytes.readUInt32BE(20), 200);
        const original = fs.readFileSync(path.join(directory, `../../src/PitchLens.App/wwwroot/images/${name === "arsenal" ? "arsenal_crest" : "manchester_city_crest"}.png`));
        assert.ok(bytes.equals(original));
    }
    assert.match(read("assets/fonts/OFL.txt"), /SIL OPEN FONT LICENSE/);
    for (const weight of ["Regular", "SemiBold", "Bold"]) {
        const font = fs.readFileSync(path.join(directory, `assets/fonts/Barlow-${weight}.ttf`));
        assert.equal(font.readUInt32BE(0), 0x00010000);
        assert.ok(font.length > 20000);
    }
});

test("classic scripts parse without module/fetch/server dependencies", () => {
    for (const file of ["assets/core.js", "assets/demo-data.js", "assets/mocks.js"])
        assert.doesNotThrow(() => new vm.Script(read(file), { filename: file }));
    assert.doesNotMatch(read("assets/mocks.js"), /\bfetch\s*\(|XMLHttpRequest|WebSocket|import\s*\(/);
    assert.doesNotMatch(read("assets/core.js"), /\bfetch\s*\(|XMLHttpRequest|WebSocket/);
});

test("illustrative match has real roster text, coherent possession and no future score/stat leakage", () => {
    assert.equal(model.players.length, 12);
    assert.equal(model.player("arsenal-rice").number, 41);
    assert.equal(model.player("manchester-city-doku").name, "Jérémy Doku");
    assert.equal(Core.score(model, 45.49)["manchester-city"], 0);
    assert.equal(Core.score(model, 45.5)["manchester-city"], 1);
    assert.equal(Core.score(model, 30)["manchester-city"], 0);
    assert.equal(Core.stats(model, "manchester-city-haaland", 44).goals, 0);
    assert.equal(Core.stats(model, "manchester-city-haaland", 72).goals, 1);
    assert.equal(Core.stats(model, "arsenal-raya", 0).accuracy, null);
    for (let i = 0; i <= 720; i++) {
        const time = i / 10, owner = Core.owner(model, time);
        if (!owner) continue;
        const player = Core.at(model.player(owner).frames, time), ball = Core.ball(model, time);
        assert.ok(Math.hypot(player.x - ball.x, player.y - ball.y) < 1e-8);
    }
});

test("play/pause, scrub, replay end and composer pace preserve one mock clock", () => {
    const clock = new Core.Clock(72, 28);
    clock.play(0);
    assert.equal(clock.time(2000), 30);
    clock.pause(2000);
    assert.equal(clock.time(50000), 30);
    clock.seek(48.5, 50000);
    assert.equal(clock.time(80000), 48.5);
    clock.speed(.75, 80000);
    clock.play(80000);
    assert.equal(clock.time(82000), 50);
    clock.seek(100, 82000);
    assert.equal(clock.time(90000), 72);
    assert.equal(clock.playing, false);
    clock.play(90000);
    assert.equal(clock.time(91000), .75);
    assert.throws(() => clock.seek(NaN, 0));
});

test("trajectory-derived stats include partial segments and seeking never double counts", () => {
    const frames = [{ time: 0, x: 0, y: 0 }, { time: 2, x: 3, y: 4 }];
    assert.equal(Core.measure(frames, 1).distance, 2.5);
    assert.equal(Core.measure(frames, 1).maxSpeed, 2.5);
    const attempts = time => model.players.reduce((sum, p) => sum + Core.stats(model, p.id, time).passes, 0);
    assert.equal(attempts(72), 9);
    assert.equal(attempts(0), 0);
    assert.equal(attempts(72), 9);
});
