import test from "node:test";
import assert from "node:assert/strict";
import { filmAt, filmDuration, FilmProjection } from "../../src/PitchLens.App/wwwroot/js/director.mjs";
import { readFileSync } from "node:fs";
const plan={scenes:[
    {sourceStart:75,sourceEnd:86,rate:1,holdSeconds:3},
    {sourceStart:115,sourceEnd:125,rate:1,holdSeconds:3},
    {sourceStart:166,sourceEnd:176,rate:1,holdSeconds:4},
    {sourceStart:189,sourceEnd:199,rate:1,holdSeconds:3}
]};
test("film projection matches native cut/hold boundaries and independent wall clock at all base speeds",()=>{
    assert.equal(filmDuration(plan),54);
    for(const base of [1,3,5]){
        const clock=new FilmProjection(plan); clock.apply(0,true,0);
        assert.equal(clock.frame(11000).sourceTime,86);
        assert.ok(clock.frame(11000).holding);
        assert.equal(clock.frame(14000).sourceTime,115);
        assert.equal(clock.frame(54000).sourceTime,199);
        clock.apply(14,false,20000);assert.equal(clock.frame(90000).filmTime,14);
        clock.apply(0,true,100000);assert.equal(clock.frame(101000).sourceTime,76);
    }
    assert.equal(filmAt({scenes:[{...plan.scenes[0],rate:.5}]},2).sourceTime,76);
});
test("actual recorded Sol edit has exact58second mapping without editorial rewrites",()=>{
    const edit=JSON.parse(readFileSync(new URL("../../src/PitchLens.Generation/Resources/recorded_director.json",import.meta.url)));
    assert.equal(filmDuration(edit),58);
    for(const [time,source,holding] of [[0,69,false],[9,78,true],[12,79,false],[14,80,false],[26,86,true],[30,152,false],[43,165,false],[54,176,true],[58,176,true]]){
        const frame=filmAt(edit,time);
        assert.equal(frame.sourceTime,source);assert.equal(frame.holding,holding);
    }
});
