import test from "node:test";
import assert from "node:assert/strict";
import {validateLens,drawLens} from "../../src/PitchLens.App/wwwroot/js/lenses.mjs";

const scenario={revision:2,duration:300,teams:[{id:"liverpool"},{id:"chelsea"}],
    players:[{id:"l1",teamId:"liverpool",number:10},{id:"l2",teamId:"liverpool",number:8}]};
const view={window:{revision:2,fixture:"liverpool/chelsea",start:10,cutoff:70},mode:"connections",subjectId:"liverpool",
    nodes:[{playerId:"l1",x:30,y:20},{playerId:"l2",x:70,y:40}],edges:[{id:"edge",from:"l1",to:"l2",count:4}],cells:[]};
test("lens packets enforce revision/fixture/time/count/participant identity and reject unavailable heat",()=>{
    assert.equal(validateLens(view,scenario),view);
    for(const window of [{...view.window,revision:1},{...view.window,fixture:"arsenal/manchester-city"},{...view.window,cutoff:NaN},
        {...view.window,start:71},{...view.window,cutoff:301}])
        assert.throws(()=>validateLens({...view,window},scenario));
    assert.throws(()=>validateLens({...view,edges:[{...view.edges[0],count:0}]},scenario));
    assert.throws(()=>validateLens({...view,edges:[{...view.edges[0],to:"unknown"}]},scenario));
    const cells=Array.from({length:28},(_,i)=>({index:i,x:i%7*15,y:Math.floor(i/7)*17,width:15,height:17,seconds:i===0?60:0}));
    const heat={...view,mode:"movement",subjectId:"l1",nodes:[],edges:[],cells};
    assert.equal(validateLens(heat,scenario),heat);
    assert.throws(()=>validateLens({...heat,cells:cells.map(c=>({...c,seconds:0}))},scenario));
    assert.throws(()=>validateLens({...heat,cells:cells.map(c=>({...c,seconds:NaN}))},scenario));
});
test("original field-coordinate network uses directed arrowheads/count weights and explicit historical node labels",()=>{
    const lines=[],labels=[],widths=[];
    const context={beginPath(){},moveTo(x,y){lines.push(["move",x,y]);},lineTo(x,y){lines.push(["line",x,y]);},
        stroke(){widths.push(this.lineWidth);},arc(){},fillText(text,x,y){labels.push({text,x,y});}};
    drawLens(context,view,scenario.players);
    assert.equal(lines.length,5,"connection plus two directional arrowhead segments");
    assert.ok(widths.includes(1.05),"four completed passes set exact count-based edge width");
    assert.deepEqual(labels.filter(l=>l.text.startsWith("H")),[{text:"H10",x:30,y:20},{text:"H8",x:70,y:40}]);
    assert.equal(labels[0].text,"4");
});
test("reading size scales canvas interface labels with a14px floor, never historical positions or edge geometry",()=>{
    for(const fieldScale of [2,5,10]){
        const renders=[];
        for(const reading of [1,1.25,1.5,1.75,2]){
            const labels=[],geometry=[];
            const c={beginPath(){},moveTo(x,y){geometry.push([x,y]);},lineTo(x,y){geometry.push([x,y]);},stroke(){},
                arc(...args){geometry.push(args);},fillText(text,x,y){labels.push({text,x,y,font:Number(this.font.match(/([\d.]+)px/)[1])*fieldScale});}};
            drawLens(c,view,scenario.players,reading,14/fieldScale);
            assert.ok(labels.every(l=>l.font>=14*reading-1e-9));
            renders.push({labels,geometry});
        }
        for(let i=0;i<renders.length;i++){
            assert.deepEqual(renders[i].geometry,renders[0].geometry);
            assert.deepEqual(renders[i].labels.map(({text,x,y})=>({text,x,y})),renders[0].labels.map(({text,x,y})=>({text,x,y})));
            for(let j=0;j<renders[0].labels.length;j++)assert.ok(Math.abs(renders[i].labels[j].font/renders[0].labels[j].font-[1,1.25,1.5,1.75,2][i])<1e-9);
        }
    }
});
