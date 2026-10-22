import { ARENA } from "./viewport.mjs";

export const DEFAULT_VIEW = Object.freeze({ version:1,style:0,markerScale:1,showNumbers:true,smartDetail:false });
export function validateView(value) {
    if(!value||Object.keys(value).sort().join(",")!=="markerScale,showNumbers,smartDetail,style,version"||
        value.version!==1||!Number.isInteger(value.style)||value.style<0||value.style>5||
        !Number.isFinite(value.markerScale)||value.markerScale<.75||value.markerScale>1.5||
        typeof value.showNumbers!=="boolean"||typeof value.smartDetail!=="boolean")
        throw new RangeError("Invalid player view settings.");
    return Object.freeze({...value});
}
export function smartView(selected,ball) {
    if(!selected)return ARENA;
    const width=Math.min(ARENA.width,Math.max(52,Math.abs(selected.x-ball.x)+22));
    const height=Math.min(ARENA.height,Math.max(36,Math.abs(selected.y-ball.y)+22));
    return {x:Math.max(ARENA.x,Math.min((selected.x+ball.x-width)/2,ARENA.x+ARENA.width-width)),
        y:Math.max(ARENA.y,Math.min((selected.y+ball.y-height)/2,ARENA.y+ARENA.height-height)),width,height};
}
export function pickTarget(targets,point,width,height) {
    if(point.x<0||point.y<0||point.x>width||point.y>height)return null;
    return targets.filter(t=>t.round?
        ((point.x-t.x)/(t.width/2))**2+((point.y-t.y)/(t.height/2))**2<=1:
        Math.abs(point.x-t.x)<=t.width/2&&Math.abs(point.y-t.y)<=t.height/2)
        .sort((a,b)=>Math.hypot(point.x-a.x,point.y-a.y)-Math.hypot(point.x-b.x,point.y-b.y)||
            (a.id<b.id?-1:a.id>b.id?1:0))[0]?.id??null;
}
function circle(c,x,y,r,fill,stroke) {
    c.beginPath();c.arc(x,y,r,0,Math.PI*2);
    if(fill){c.fillStyle=fill;c.fill();}if(stroke){c.strokeStyle=stroke;c.stroke();}
}
function box(c,x,y,w,h,r) {c.beginPath();c.roundRect(x,y,w,h,r);}

export class PlayerView {
    constructor(invalidate,report=()=>{},createImage=()=>new Image()) {
        this.invalidate=invalidate;this.report=report;this.createImage=createImage;
        this.assets=new Map();this.bindings=new Map();this.disposed=false;this.constructed=0;this.failures=0;
    }
    prepare(scenario) {
        this.bindings.clear();
        for(const p of scenario.players) {
            const v=p.portrait;
            if(!v)continue;
            if(v.playerId!==p.id||v.teamId!==p.teamId||v.name!==p.name||
                !/^[a-z0-9-]+$/.test(p.id)||v.imagePath!==`images/players/${p.id}.png`) {
                this.report(`Photo identity unavailable for ${p.name}; numbered marker retained.`);continue;
            }
            let asset=this.assets.get(v.imagePath);
            if(!asset) {
                asset={state:"loading",image:this.createImage()};this.constructed++;
                this.assets.set(v.imagePath,asset);
                const fail=()=>{
                    if(this.disposed||asset.state==="failed")return;
                    asset.state="failed";this.failures++;
                    if([...this.bindings.values()].some(b=>b.asset===asset)) {
                        this.report(`Photo unavailable for ${p.name}; numbered marker retained.`);
                        this.invalidate();
                    }
                };
                asset.image.onerror=fail;
                asset.image.onload=async()=>{
                    if(this.disposed||asset.state!=="loading")return;
                    asset.state="decoding";
                    try {
                        if(asset.image.decode)await asset.image.decode();
                        if(!(asset.image.naturalWidth>0&&asset.image.naturalHeight>0))throw new Error("Empty local portrait.");
                    } catch(error) {console.error("Local player portrait could not be decoded.",error);fail();return;}
                    if(this.disposed||asset.state!=="decoding")return;
                    asset.state="ready";
                    if([...this.bindings.values()].some(b=>b.asset===asset))this.invalidate();
                };
                asset.image.src=v.imagePath;
            }
            this.bindings.set(p.id,{team:p.teamId,name:p.name,asset});
            if(asset.state==="failed")this.report(`Photo unavailable for ${p.name}; numbered marker retained.`);
        }
    }
    image(p) {
        const b=this.bindings.get(p.id);
        return b?.team===p.teamId&&b.name===p.name&&b.asset.state==="ready"?b.asset.image:null;
    }
    geometry(p,point,ball,selected,settings,scale,detail) {
        const wanted=settings.style!==0&&(settings.style!==4||p.id===selected||Math.hypot(point.x-ball.x,point.y-ball.y)<=9)&&
            (settings.style!==5||detail);
        const image=wanted?this.image(p):null;
        const style=image?(settings.style===4||settings.style===5?1:settings.style):0;
        const r=style?Math.min(2.6*settings.markerScale,28*settings.markerScale/scale):2*settings.markerScale;
        const w=style===3?r*1.75:r*2,h=style===2?r*2.6:style===3?r*2.25:r*2;
        return {style,image,r,width:w,height:h,number:!image||settings.showNumbers};
    }
    draw(c,p,point,color,away,geometry,selected,goal) {
        const g=geometry,x=point.x,y=point.y;
        c.lineWidth=.18;
        if(g.style===0) {
            if(goal){c.lineWidth=.45;circle(c,x,y,g.r+1,null,"#05f0ff");}
            if(selected){c.lineWidth=.6;circle(c,x,y,g.r+.8,null,"#fff");}
            circle(c,x+.15,y+.3,g.r+.2,"#23382b");
            c.lineWidth=.18;circle(c,x,y,g.r,color,"#f4f4ef");
            if(away){c.strokeStyle="#fff";c.lineWidth=.25;c.beginPath();c.moveTo(x-g.r*.75,y+g.r*.55);c.lineTo(x+g.r*.75,y+g.r*.55);c.stroke();}
            c.fillStyle="#37003c";c.font=`700 ${g.r*1.25}px Barlow, sans-serif`;c.textAlign="center";c.textBaseline="middle";
            c.fillText(String(p.number),x,y);return;
        }
        const left=x-g.width/2,top=y-g.height/2,u=g.r/2.6;
        c.save();
        if(g.style===1) {
            circle(c,x,y,g.r,color);
            c.save();c.beginPath();c.arc(x,y,g.r-.22*u,0,Math.PI*2);c.clip();
            const image=g.image,sw=image.naturalWidth*.68,sh=image.naturalWidth*.68;
            c.drawImage(image,image.naturalWidth*.16,0,sw,sh,left+.2*u,top+.2*u,g.width-.4*u,g.height-.4*u);c.restore();
        } else if(g.style===2) {
            c.fillStyle=color;c.beginPath();c.ellipse(x,y+g.height/2-.25*u,g.width*.46,.5*u,0,0,Math.PI*2);c.fill();
            const fit=Math.min(g.width/g.image.naturalWidth,(g.height-.35*u)/g.image.naturalHeight);
            const w=g.image.naturalWidth*fit,h=g.image.naturalHeight*fit;
            c.drawImage(g.image,x-w/2,top+g.height-.35*u-h,w,h);
        } else {
            box(c,left,top,g.width,g.height,.35*u);c.fillStyle=color;c.fill();
            const fit=Math.min((g.width-.35*u)/g.image.naturalWidth,(g.height-.3*u)/g.image.naturalHeight);
            const w=g.image.naturalWidth*fit,h=g.image.naturalHeight*fit;
            c.drawImage(g.image,x-w/2,top+.15*u,w,h);
        }
        if(selected||goal) {
            c.strokeStyle=selected?"#fff":"#05f0ff";c.lineWidth=.4*u;
            if(g.style===1)circle(c,x,y,g.r+.55*u,null,c.strokeStyle);
            else{box(c,left-.5*u,top-.5*u,g.width+u,g.height+u,.5*u);c.stroke();}
        }
        if(g.number) {
            const badge=g.style===3?{x,y:y+g.height/2-.4*u,w:g.width,h:.95*u}:
                {x:x+g.width*.3,y:y+g.height*.38,w:1.7*u,h:1.3*u};
            box(c,badge.x-badge.w/2,badge.y-badge.h/2,badge.w,badge.h,.22*u);c.fillStyle=color;c.fill();
            c.lineWidth=.12*u;c.strokeStyle="#fff";c.stroke();
            c.fillStyle="#37003c";c.font=`700 ${1.15*u}px Barlow, sans-serif`;c.textAlign="center";c.textBaseline="middle";
            c.fillText(String(p.number),badge.x,badge.y);
        }
        c.restore();
    }
    dispose() {
        this.disposed=true;
        for(const asset of this.assets.values()){asset.image.onload=null;asset.image.onerror=null;}
        this.assets.clear();this.bindings.clear();
    }
}
