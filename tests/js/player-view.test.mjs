import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {PlayerView,DEFAULT_VIEW,validateView,smartView,pickTarget} from "../../src/PitchLens.App/wwwroot/js/player-view.mjs";
import {ARENA} from "../../src/PitchLens.App/wwwroot/js/viewport.mjs";

const player={id:"arsenal-saka",teamId:"arsenal",name:"Bukayo Saka",number:7,
    portrait:{playerId:"arsenal-saka",teamId:"arsenal",name:"Bukayo Saka",imagePath:"images/players/arsenal-saka.png"}};
const inkFixture=JSON.parse(readFileSync(new URL("./fixtures/barlow-number-ink.json",import.meta.url),"utf8")).numbers;
function cacheHarness() {
    const images=[],messages=[];let invalidations=0;
    const view=new PlayerView(()=>invalidations++,m=>messages.push(m),()=>{
        const image={naturalWidth:220,naturalHeight:280,decode:async()=>{}};
        images.push(image);return image;
    });
    return {view,images,messages,get invalidations(){return invalidations;}};
}
test("validated view settings reject corrupt persisted/native packets without arbitrary new styles",()=>{
    assert.equal(validateView({...DEFAULT_VIEW,style:5,markerScale:1.5}).style,5);
    for(const value of [null,{...DEFAULT_VIEW,version:2},{...DEFAULT_VIEW,style:6},{...DEFAULT_VIEW,markerScale:NaN},
        {...DEFAULT_VIEW,markerScale:0},{...DEFAULT_VIEW,showNumbers:"false"},{...DEFAULT_VIEW,url:"https://invalid"}])
        assert.throws(()=>validateView(value),/Invalid/);
});
test("verified local images construct/decode once; async completion invalidates paused frame, failures do not reload",async()=>{
    const h=cacheHarness();h.view.prepare({players:[player]});
    assert.equal(h.images.length,1);assert.equal(h.view.image(player),null);
    await h.images[0].onload();assert.equal(h.invalidations,1);assert.equal(h.view.image(player),h.images[0]);
    h.view.prepare({players:[player]});assert.equal(h.images.length,1);
    const changed={...player,name:"Another person"};
    h.view.prepare({players:[changed]});assert.equal(h.view.image(changed),null);assert.equal(h.images.length,1);
    h.view.prepare({players:[{...player,portrait:{...player.portrait,imagePath:"https://invalid/face.png"}}]});
    assert.equal(h.images.length,1);assert.ok(h.messages.length>0);
    const failed=cacheHarness();failed.view.prepare({players:[player]});
    failed.images[0].decode=async()=>{throw new Error("Controlled decode failure");};
    await failed.images[0].onload();assert.equal(failed.view.image(player),null);assert.equal(failed.invalidations,1);
    failed.view.prepare({players:[player]});assert.equal(failed.images.length,1);assert.equal(failed.view.failures,1);
    const stale=cacheHarness();stale.view.prepare({players:[player]});const callback=stale.images[0].onload;
    stale.view.dispose();await callback();assert.equal(stale.invalidations,0);
});
test("six actual marker policies retain numbered fallbacks and implement focus distance/detail rather than mock imports",async()=>{
    const h=cacheHarness();h.view.prepare({players:[player]});await h.images[0].onload();
    for(let style=0;style<6;style++){
        const settings={...DEFAULT_VIEW,style,showNumbers:false};
        const g=h.view.geometry(player,{x:50,y:34},{x:50,y:34},"arsenal-rice",settings,8,true);
        assert.equal(!!g.image,style!==0);assert.equal(g.number,style===0);
        assert.ok(g.width>0&&g.height>0);
        const fallback=h.view.geometry({...player,id:"unknown"},{x:50,y:34},{x:50,y:34},"unknown",settings,8,true);
        assert.equal(fallback.style,0);assert.equal(fallback.number,true);
    }
    assert.equal(h.view.geometry(player,{x:20,y:10},{x:50,y:34},"other",{...DEFAULT_VIEW,style:4},8,false).style,0);
    assert.equal(h.view.geometry(player,{x:20,y:10},{x:50,y:34},player.id,{...DEFAULT_VIEW,style:4},8,false).style,1);
    assert.equal(h.view.geometry(player,{x:50,y:34},{x:50,y:34},player.id,{...DEFAULT_VIEW,style:5},8,false).style,0);
    const g=h.view.geometry(player,{x:50,y:34},{x:50,y:34},player.id,{...DEFAULT_VIEW,style:1,markerScale:1.5},100,true);
    assert.equal(g.width*100,84,"detail portraits have a bounded pixel size scaled by explicit user preference");
});
test("smart camera is genuinely tighter and contains selected player/ball; hit targets match drawn circles/boxes deterministically",()=>{
    const view=smartView({x:50,y:30},{x:60,y:36});
    assert.ok(view.width<ARENA.width&&view.height<ARENA.height);
    for(const p of [{x:50,y:30},{x:60,y:36}]){
        assert.ok(p.x>=view.x&&p.x<=view.x+view.width);assert.ok(p.y>=view.y&&p.y<=view.y+view.height);
    }
    const targets=[{id:"b",x:30,y:30,width:20,height:20,round:true},{id:"a",x:30,y:30,width:20,height:20,round:true}];
    assert.equal(pickTarget(targets,{x:30,y:30},100,100),"a");
    assert.equal(pickTarget(targets,{x:39,y:39},100,100),null);
    assert.equal(pickTarget([{...targets[0],round:false}],{x:39,y:39},100,100),"b");
    assert.equal(pickTarget(targets,{x:-1,y:30},100,100),null);
});
test("production cut-outs and tiles preserve actual image aspect and number badges scale with bounded portraits",async()=>{
    const h=cacheHarness();h.view.prepare({players:[player]});await h.images[0].onload();
    let textScale=1;const scales=[];
    const images=[],fonts=[],c={drawImage:(...args)=>images.push(args),fillText:()=>fonts.push(parseFloat(c.font.split(" ")[1])*textScale),
        measureText:text=>inkFixture[text],translate(){},scale(x){textScale*=x;},
        save(){scales.push(textScale);},restore(){textScale=scales.pop();}};
    for(const name of ["beginPath","arc","fill","stroke","clip","ellipse","roundRect"])c[name]=()=>{};
    for(const style of [1,2,3])for(const scale of [8,100]){
        const g=h.view.geometry(player,{x:50,y:34},{x:50,y:34},player.id,{...DEFAULT_VIEW,style},scale,true);
        h.view.draw(c,player,{x:50,y:34},"mint",false,g,true,false);
        const image=images.at(-1),width=image.at(-2),height=image.at(-1);
        assert.ok(Math.abs(width/height-(style===1?1:220/280))<1e-12,"no squashed headshots");
        assert.ok(fonts.at(-1)*scale<=12.4,"badge remains proportional at detail pixel cap");
    }
});
