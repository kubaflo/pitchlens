import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,writeFileSync} from "node:fs";

test("every production CSS font size resolves through text-only root scaling, including media and important overrides",()=>{
    const css=readFileSync(new URL("../../src/PitchLens.App/wwwroot/app.css",import.meta.url),"utf8");
    const declarations=[...css.matchAll(/font-size\s*:\s*([^;}]+)/g)].map(m=>m[1].replace(/\s*!important\s*$/,"").trim());
    assert.ok(declarations.length>120,"inventory includes the whole stylesheet, not only the new widget");
    const root="calc(112.5% * var(--reading-scale, 1))";
    assert.equal(declarations.filter(v=>v===root).length,1);
    const relative=declarations.filter(v=>v!==root);
    for(const value of relative)assert.match(value,/^\d*\.?\d+rem$/,"no absolute, nested-em, viewport or unscaled override: "+value);
    assert.doesNotMatch(css,/\bfont\s*:[^;{}]*\d(?:px|pt)/);
    assert.doesNotMatch(css,/\bzoom\s*:/);
    assert.doesNotMatch(css,/-webkit-line-clamp:\s*[1-9]/);
    const resolved=[100,125,150,175,200].map(percent=>({
        percent,rootPixels:18*percent/100,fontPixels:relative.map(value=>Number.parseFloat(value)*18*percent/100)
    }));
    for(const sizes of resolved){
        assert.ok(Math.min(...sizes.fontPixels)>=14*sizes.percent/100-0.00001);
        for(let i=0;i<relative.length;i++)assert.ok(Math.abs(sizes.fontPixels[i]/resolved[0].fontPixels[i]-sizes.percent/100)<1e-9);
    }
    const marker=readFileSync(new URL("../../src/PitchLens.App/wwwroot/js/player-view.mjs",import.meta.url),"utf8");
    assert.match(marker,/100px Barlow/,"canonical measured shirt-number geometry deliberately remains independent");
    if(process.env.PITCHLENS_READING_CSS_WITNESS)writeFileSync(process.env.PITCHLENS_READING_CSS_WITNESS,JSON.stringify({method:"Production declaration resolution, not browser layout or native pixels",declarations:declarations.length,resolved},null,2));
});
