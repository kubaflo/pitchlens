import test from "node:test";
import assert from "node:assert/strict";
import { ProjectionClock } from "../../src/PitchLens.App/wwwroot/js/replay.mjs";
import { FilmProjection } from "../../src/PitchLens.App/wwwroot/js/director.mjs";

test("typed voice-paced projection uses1x and clamps at observed cue boundaries despite base3/5 and huge time steps",()=>{
    for(const rate of [1,3,5]){
        const clock=new ProjectionClock();clock.reset(1,300,0,[{start:76,end:96}]);
        clock.apply({revision:1,sequence:1,time:0,playing:true,rate,voicePaced:true,voiceBoundary:3},0);
        assert.equal(clock.time(1e6),3);assert.equal(clock.effectiveRate(1e6),1);
        clock.apply({revision:1,sequence:2,time:3,playing:true,rate,voicePaced:true,voiceBoundary:4.999999999999999},1e6);
        assert.equal(clock.time(2e6),4.999999999999999);
        clock.apply({revision:1,sequence:3,time:4.999999999999999,playing:true,rate,voicePaced:true,voiceBoundary:7},2e6);
        assert.equal(clock.time(3e6),7);
        clock.apply({revision:1,sequence:4,time:7,playing:true,rate,voicePaced:false,voiceBoundary:null},3e6);
        assert.equal(clock.time(3e6+1000),7+rate);
        assert.equal(clock.apply({revision:1,sequence:2,time:0,playing:true,rate,voicePaced:true,voiceBoundary:3},3e6),false);
    }
});
test("film projection holds the full sentence until native release, leaving original58s plan unchanged",()=>{
    const plan={scenes:[{sourceStart:69,sourceEnd:78,rate:1,holdSeconds:3},
        {sourceStart:79,sourceEnd:86,rate:.5,holdSeconds:4},
        {sourceStart:152,sourceEnd:162,rate:1,holdSeconds:3},
        {sourceStart:165,sourceEnd:176,rate:1,holdSeconds:4}]};
    const film=new FilmProjection(plan);
    film.apply(9,true,0,11.999999999999998);
    assert.equal(film.frame(1e6).sourceTime,78);assert.ok(film.frame(1e6).holding);
    film.apply(11.999999999999998,true,1e6,26);
    assert.equal(film.frame(2e6).sourceTime,86);
    film.apply(54,true,2e6,57.99999999999999);
    assert.ok(film.frame(3e6).filmTime<58);assert.equal(film.frame(3e6).sourceTime,176);
    film.apply(57.99999999999999,true,3e6,null);
    assert.equal(film.frame(4e6).filmTime,58);
});
