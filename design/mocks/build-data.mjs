// Package the reviewed synthetic recording as a classic script for file:// use.
import fs from "node:fs";
const scenario = JSON.parse(fs.readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_match.json", import.meta.url), "utf8"));
const roster = [
    ["arsenal-raya", "David Raya", 1], ["arsenal-saliba", "William Saliba", 2],
    ["arsenal-odegaard", "Martin Odegaard", 8], ["arsenal-rice", "Declan Rice", 41],
    ["arsenal-saka", "Bukayo Saka", 7], ["arsenal-gyokeres", "Viktor Gyokeres", 14],
    ["manchester-city-donnarumma", "Gianluigi Donnarumma", 1], ["manchester-city-dias", "Rúben Dias", 3],
    ["manchester-city-foden", "Phil Foden", 47], ["manchester-city-cherki", "Rayan Cherki", 10],
    ["manchester-city-haaland", "Erling Haaland", 9], ["manchester-city-doku", "Jérémy Doku", 11]
].map(([id, name, number]) => ({ id, name, number, team: id.startsWith("arsenal-") ? "arsenal" : "manchester-city" }));
fs.writeFileSync(new URL("./assets/demo-data.js", import.meta.url),
    "// Scripted synthetic design data. No SDK or network calls.\nwindow.PitchLensDemo = " + JSON.stringify({ scenario, roster }) + ";\n");
