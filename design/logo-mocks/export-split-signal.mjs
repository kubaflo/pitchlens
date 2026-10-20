import { readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";

const image = readFileSync(new URL("assets/premier-league-official.png", import.meta.url));
const provenance = JSON.parse(readFileSync(new URL("assets/provenance.json", import.meta.url)));
if (createHash("sha256").update(image).digest("hex") !== provenance.sha256)
    throw new Error("Official mark does not match its recorded source hash.");

const svg = `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="512" height="512" viewBox="0 0 512 512">
  <defs><clipPath id="tile"><rect width="512" height="512" rx="117.76"/></clipPath></defs>
  <g clip-path="url(#tile)">
    <rect width="512" height="512" fill="#37003C"/>
    <path d="M344 0H512V512H158Z" fill="#00FF87"/>
    <g transform="rotate(-8 256 256)">
      <rect x="102.4" y="112.16" width="307.2" height="327.68" rx="30.72" ry="32.768" fill="#210025" opacity=".33"/>
      <rect x="102.4" y="92.16" width="307.2" height="327.68" rx="30.72" ry="32.768" fill="#FFFFFF"/>
    </g>
    <image x="117.76" y="117.76" width="276.48" height="276.48" xlink:href="data:image/png;base64,${image.toString("base64")}"/>
  </g>
</svg>
`;
for (const name of ["AppIcon/appicon.svg", "Splash/splash.svg"])
    writeFileSync(new URL(`../../src/PitchLens.App/Resources/${name}`, import.meta.url), svg);
console.log("Exported selected10 Split Signal to app icon/splash with unchanged official PNG.");
