import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,writeFileSync} from "node:fs";
import {createHash} from "node:crypto";
const {PlayerView,DEFAULT_VIEW,drawShirtNumber}=await import(process.env.PITCH_TEST_PLAYER_VIEW||
    new URL("../../src/PitchLens.App/wwwroot/js/player-view.mjs",import.meta.url));
import {viewportMap,ARENA} from "../../src/PitchLens.App/wwwroot/js/viewport.mjs";

const fixture=JSON.parse(readFileSync(new URL("./fixtures/barlow-number-ink.json",import.meta.url),"utf8"));
const point=(m,x,y)=>({x:m[0]*x+m[2]*y+m[4],y:m[1]*x+m[3]*y+m[5]});
class InkCanvas {
    constructor(matrix){this.matrix=[...matrix];this.stack=[];this.labels=[];this.boxes=[];this.lines=[];this.colors=[];this.strokes=[];}
    save(){this.stack.push({matrix:[...this.matrix],font:this.font,textAlign:this.textAlign,textBaseline:this.textBaseline});}
    restore(){Object.assign(this,this.stack.pop());}
    translate(x,y){const m=this.matrix;m[4]+=m[0]*x+m[2]*y;m[5]+=m[1]*x+m[3]*y;}
    scale(x,y){this.matrix[0]*=x;this.matrix[1]*=x;this.matrix[2]*=y;this.matrix[3]*=y;}
    beginPath(){this.path=null;}arc(x,y,r){this.path={kind:"circle",x,y,r};}
    roundRect(x,y,w,h){this.path={kind:"box",x,y,w,h};}
    ellipse(){this.path={kind:"ground"};}clip(){}drawImage(){}
    moveTo(x,y){this.lines.push({x,y});}lineTo(x,y){this.lines.push({x,y});}
    fill(){this.colors.push(this.fillStyle);if(this.path?.kind==="box")this.boxes.push({...this.path,matrix:[...this.matrix]});}
    stroke(){this.strokes.push({color:this.strokeStyle,path:this.path});}
    measureText(text){assert.equal(this.font,"700 100px Barlow, sans-serif");assert.equal(this.textBaseline,"alphabetic");return fixture.numbers[text];}
    fillText(text,x,y){
        const b=fixture.numbers[text],m=this.matrix;
        const topLeft=point(m,x-b.actualBoundingBoxLeft,y-b.actualBoundingBoxAscent);
        const bottomRight=point(m,x+b.actualBoundingBoxRight,y+b.actualBoundingBoxDescent);
        this.labels.push({text,topLeft,bottomRight,center:{x:(topLeft.x+bottomRight.x)/2,y:(topLeft.y+bottomRight.y)/2}});
    }
}
const player={id:"arsenal-saka",teamId:"arsenal",name:"Bukayo Saka",number:7,
    portrait:{playerId:"arsenal-saka",teamId:"arsenal",name:"Bukayo Saka",imagePath:"images/players/arsenal-saka.png"}};

test("all jersey ink centers use measured Barlow bounds across every production style/size/camera/DPR; no away stripes",async()=>{
    const font=readFileSync(new URL("../../src/PitchLens.App/wwwroot/fonts/Barlow-Bold.ttf",import.meta.url));
    assert.equal(createHash("sha256").update(font).digest("hex"),fixture.fontSHA256);
    let image;const view=new PlayerView(()=>{},()=>{},()=>image={naturalWidth:220,naturalHeight:280,decode:async()=>{}});
    view.prepare({players:[player]});await image.onload();
    let samples=0,maxError=0;const examples=[];
    for(const number of Object.keys(fixture.numbers).map(Number))
    for(const markerScale of [.75,1,1.5])
    for(const style of [0,1,2,3,4,5])
    for(const [width,height,dpr,camera] of [[1144,595,1,ARENA],[700,800,2,ARENA],[1144,595,2,{x:24,y:16,width:52,height:36}]])
    for(const away of [false,true]){
        const map=viewportMap(width,height,width*dpr,height*dpr,camera),where={x:50,y:34};
        const p={...player,number},g=view.geometry(p,where,where,p.id,{...DEFAULT_VIEW,style,markerScale},map.field.scale,true);
        const c=new InkCanvas(map.matrix),color=away?"#ff7d80":"#c8ffd4";
        view.draw(c,p,where,color,away,g,true,true);
        assert.equal(c.labels.length,1);assert.equal(c.lines.length,0,"marker painter must not draw decorative line segments");
        assert.ok(c.colors.includes(color));
        assert.ok(c.strokes.some(s=>s.color==="#fff"&&(g.style<2?s.path?.kind==="circle"&&s.path.r>g.r:
            s.path?.kind==="box"&&s.path.w>g.width)),"selection ring retained outside the marker");
        const target=g.style===0?point(map.matrix,where.x,where.y):
            point(c.boxes.at(-1).matrix,c.boxes.at(-1).x+c.boxes.at(-1).w/2,c.boxes.at(-1).y+c.boxes.at(-1).h/2);
        const ink=c.labels[0],error=Math.hypot(ink.center.x-target.x,ink.center.y-target.y);
        assert.ok(error<1e-8,`${number}/style${style}/scale${markerScale}/DPR${dpr}: ink center drift ${error}`);
        if(g.style!==0){
            const b=c.boxes.at(-1),left=point(b.matrix,b.x,b.y),right=point(b.matrix,b.x+b.w,b.y+b.h);
            assert.ok(ink.topLeft.x>=left.x&&ink.bottomRight.x<=right.x,"two-digit ink fits badge width");
            assert.ok(ink.topLeft.y>=left.y&&ink.bottomRight.y<=right.y,"ink fits badge height");
        }
        samples++;maxError=Math.max(maxError,error);
        if([1,4,7,10,14,36,88].includes(number)&&markerScale===1&&away&&width===1144&&dpr===1)
            examples.push({number,style,inkCenter:ink.center,target,error});
    }
    if(process.env.PITCHLENS_MARKER_WITNESS)writeFileSync(process.env.PITCHLENS_MARKER_WITNESS,JSON.stringify({
        evidence:"Production PlayerView with actual bundled Barlow CoreText glyph bounds and controlled Canvas matrices; not browser/native pixels",
        fontSHA256:fixture.fontSHA256,samples,maxCenterErrorPixels:maxError,examples},null,2));
    view.dispose();
});
test("badge-off suppresses text while missing portraits and Smart wide keep centered numbered fallbacks",async()=>{
    let image;const view=new PlayerView(()=>{},()=>{},()=>image={naturalWidth:220,naturalHeight:280,decode:async()=>{}});
    view.prepare({players:[player]});await image.onload();
    for(const style of [1,2,3,4,5]){
        const c=new InkCanvas([8,0,0,8,10,20]),p={...player,number:14},where={x:50,y:34};
        const hidden=view.geometry(p,where,where,p.id,{...DEFAULT_VIEW,style,showNumbers:false},8,true);
        view.draw(c,p,where,"mint",false,hidden,false,false);assert.equal(c.labels.length,0);
        const missing={...p,id:"missing"},fallback=view.geometry(missing,where,where,missing.id,{...DEFAULT_VIEW,style,showNumbers:false},8,true);
        view.draw(c,missing,where,"red",true,fallback,false,false);assert.equal(c.labels.length,1);
        const target=point(c.matrix,where.x,where.y);
        assert.ok(Math.hypot(c.labels[0].center.x-target.x,c.labels[0].center.y-target.y)<1e-8);
    }
    const wide=new InkCanvas([8,0,0,8,10,20]),where={x:50,y:34};
    const wideGeometry=view.geometry(player,where,where,player.id,{...DEFAULT_VIEW,style:5,showNumbers:false},8,false);
    view.draw(wide,player,where,"mint",false,wideGeometry,false,false);
    const wideCenter=point(wide.matrix,where.x,where.y);
    assert.ok(Math.hypot(wide.labels[0].center.x-wideCenter.x,wide.labels[0].center.y-wideCenter.y)<1e-8);
    const c=new InkCanvas([1,0,0,1,0,0]);c.measureText=()=>({actualBoundingBoxLeft:0,actualBoundingBoxRight:0,actualBoundingBoxAscent:0,actualBoundingBoxDescent:0});
    assert.throws(()=>drawShirtNumber(c,14,0,0,10,20,20,new Map()),/metrics are unavailable/);
    assert.equal(c.stack.length,0);assert.equal(c.labels.length,0);
});
