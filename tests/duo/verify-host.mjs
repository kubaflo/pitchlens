import assert from "node:assert/strict";
import {execFile} from "node:child_process";
import {promisify} from "node:util";

const run = promisify(execFile);
const cli = process.env.MAUI_DEVFLOW;
const port = process.env.DEVFLOW_PORT;
const simulator = process.env.SIMULATOR_ID;
const expectedMode = process.env.DUO_EXPECTED_MODE;
assert.ok(cli && port && simulator, "Set MAUI_DEVFLOW, DEVFLOW_PORT and SIMULATOR_ID.");
assert.ok(["SinglePane", "Wide", "Tall"].includes(expectedMode), "Set DUO_EXPECTED_MODE to the native mode being checked.");
assert.ok(process.env.DEVELOPER_DIR, "Select Xcode 27.1 with DEVELOPER_DIR, not global xcode-select.");

async function command(...args) {
    const result = await run(cli, ["devflow", ...args, "--agent-port", port, "--json"]);
    return JSON.parse(result.stdout);
}

const listing = JSON.parse((await run("xcrun", ["simctl", "list", "devices", "-j"])).stdout);
const devices = Object.entries(listing.devices).flatMap(([runtime, values]) =>
    values.map(device => ({...device, runtime})));
const device = devices.find(value => value.udid === simulator);
assert.equal(device?.deviceTypeIdentifier, "com.apple.CoreSimulator.SimDeviceType.iPhone-Duo");
assert.equal(device.state, "Booted");
assert.equal(device.runtime, "com.apple.CoreSimulator.SimRuntime.iOS-27-1");

const tree = await command("ui", "tree");
function find(nodes, predicate) {
    for (const node of nodes) {
        if (predicate(node)) return node;
        const child = find(node.children ?? [], predicate);
        if (child) return child;
    }
}
const host = find(tree, node => node.automationId === "duo-host");
assert.equal(host?.type, "TwoPaneView");
const mode = (await command("ui", "property", "duo-host", "Mode")).value;
assert.equal(mode, expectedMode);
const panes = host.children.filter(node => node.isVisible).map(node => node.bounds);
assert.equal(panes.length, mode === "SinglePane" ? 1 : 2);
for (const pane of panes) {
    assert.ok(pane.width > 0 && pane.height > 0);
    assert.ok(pane.x >= -1 && pane.y >= -1);
    assert.ok(pane.x + pane.width <= host.bounds.width + 1);
    assert.ok(pane.y + pane.height <= host.bounds.height + 1);
}
const web = find([host], node => node.type === "BlazorWebView");
assert.ok(web?.isVisible);
assert.ok(Math.abs(web.bounds.width - panes[0].width) < 1);
assert.ok(Math.abs(web.bounds.height - panes[0].height) < 1);
if (mode === "SinglePane") {
    assert.ok(Math.abs(panes[0].width - host.bounds.width) < 1);
    assert.ok(Math.abs(panes[0].height - host.bounds.height) < 1);
} else if (mode === "Wide") {
    assert.ok(panes[1].x > panes[0].x + panes[0].width, "Native division must separate the panes.");
} else {
    assert.ok(panes[1].y > panes[0].y + panes[0].height, "Native division must separate the panes.");
}
console.log(JSON.stringify({
    simulator, runtime: device.runtime, mode, host: host.bounds, panes,
    scope: "Actual native host geometry only; not a posture or physical-touch certification."
}, null, 2));
