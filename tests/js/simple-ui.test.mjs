import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,existsSync} from "node:fs";
import {resolve,dirname} from "node:path";
import vm from "node:vm";

const folder=resolve("design/simple-ui");
const pages=["01-essential","02-companion","03-cinema","04-two-views","05-moments"];
const code=readFileSync(resolve(folder,"assets/simple.js"),"utf8");

// Controlled DOM, not browser layout/pixel acceptance. Execute the shipped mock interaction code.
function harness(html){
    class Element{
        children=[];hidden=false;value="";textContent="";events={};focused=0;open=false;
        constructor(tag,attributes={}){
            this.tagName=tag;this.attributes=attributes;
            this.dataset=Object.fromEntries(Object.entries(attributes).filter(([k])=>k.startsWith("data-")).map(([k,v])=>[k.slice(5).replace(/-([a-z])/g,(_,c)=>c.toUpperCase()),v]));
            const classes=new Set((attributes.class??"").split(" "));
            this.classList={add:k=>classes.add(k),remove:k=>classes.delete(k),toggle:(k,on)=>on?classes.add(k):classes.delete(k)};
            this.classes=classes;this.style={values:{},setProperty(k,v){this.values[k]=v;}};
        }
        set innerHTML(value){this.children=parse(value);}
        get innerHTML(){return "";}
        append(...children){this.children.push(...children);}
        setAttribute(k,v){this.attributes[k]=v;}
        hasAttribute(k){return Object.hasOwn(this.attributes,k);}
        addEventListener(name,callback){this.events[name]=callback;}
        closest(selector){return selector==="button"&&this.tagName==="button"?this:null;}
        focus(){this.focused++;}
        scrollIntoView(){}
        showModal(){this.open=true;}
        close(){this.open=false;this.events.close?.();}
        getBoundingClientRect(){return {left:0,top:0,right:500,bottom:500};}
    }
    function parse(value){
        return [...value.matchAll(/<([a-z][\w-]*)\b([^>]*)>/g)].map(([,tag,source])=>{
            const attributes=Object.fromEntries([...source.matchAll(/([:\w-]+)(?:="([^"]*)")?/g)].map(([,k,v])=>[k,v??""]));
            return new Element(tag,attributes);
        });
    }
    const roots=parse(html),body=roots.find(n=>n.tagName==="body"),documentElement=roots.find(n=>n.tagName==="html");
    function all(){const walk=nodes=>nodes.flatMap(node=>[node,...walk(node.children)]);return walk(roots);}
    function matches(node,selector){return selector.startsWith(".")?node.classes.has(selector.slice(1)):node.hasAttribute(selector.slice(1,-1));}
    const events={},windowEvents={};let frame=null;
    const document={body,documentElement,hidden:false,querySelector:selector=>all().find(n=>matches(n,selector)),
        querySelectorAll:selector=>all().filter(n=>matches(n,selector)),createElementNS:(_,tag)=>new Element(tag),
        addEventListener:(name,fn)=>events[name]=fn};
    body.insertAdjacentHTML=(_,text)=>roots.push(...parse(text));
    vm.runInNewContext(code,{document,window:{matchMedia:()=>({matches:false}),addEventListener:(name,fn)=>windowEvents[name]=fn},
        performance:{now:()=>0},requestAnimationFrame:fn=>{frame=fn;return 1;},cancelAnimationFrame:()=>frame=null});
    const get=selector=>document.querySelector(selector),click=selector=>{const button=get(selector);assert.ok(button,selector);events.click({target:button});return button;};
    return {get,click,document,events,windowEvents,tick:now=>frame?.(now),frame:()=>frame,all};
}

test("five complete local HTML choices have no remote runtime assets or missing links",()=>{
    for(const name of ["index",...pages]){
        const file=resolve(folder,name+".html"),html=readFileSync(file,"utf8");
        assert.match(html,/<h1\b/);assert.match(html,/<meta name="viewport"/);
        for(const [,url] of html.matchAll(/(?:src|href)="([^"#]+)"/g)){
            assert.doesNotMatch(url,/^https?:/);assert.ok(existsSync(resolve(dirname(file),url)),`${name}: ${url}`);
        }
    }
    const css=readFileSync(resolve(folder,"assets/simple.css"),"utf8");
    for(const [,url] of css.matchAll(/url\("([^"]+)"\)/g))assert.ok(existsSync(resolve(folder,"assets",url)));
    assert.doesNotMatch(code,/\b(?:fetch|XMLHttpRequest|localStorage|sessionStorage)\b/);
});
for(const page of pages)test(page+": shipped interactions expose simple explanation, ordered roles and preview-only text sizing",()=>{
    const h=harness(readFileSync(resolve(folder,page+".html"),"utf8"));
    const circles=h.all().filter(n=>n.tagName==="circle");assert.ok(circles.length>=24);
    assert.equal(h.frame(),null);h.click("[data-play]");assert.equal(h.get("[data-play]").textContent,"Pause");
    h.tick(1000);h.click("[data-play]");assert.equal(h.frame(),null);
    h.click("[data-explain]");assert.equal(h.get("[data-answer]").hidden,false);
    assert.ok(h.get("[data-copy]").textContent.includes("scorer"));
    h.click("[data-copy-mode]"); // Fan remains the default.
    const analyst=h.document.querySelectorAll("[data-copy-mode]")[1];h.events.click({target:analyst});
    assert.ok(h.get("[data-copy]").textContent.includes("expected-goal"));
    assert.equal(h.frame(),null,"explanation pauses only this mock animation");
    const settings=h.click("[data-settings]");assert.equal(h.get(".settings").open,true);
    const size=h.get("[data-size]");
    for(const percent of [100,125,150,175,200]){size.value=String(percent);h.events.change({target:size});assert.equal(h.document.documentElement.style.values["--reading"],String(percent/100));}
    h.click("[data-close-settings]");assert.equal(h.get(".settings").open,false);assert.equal(settings.focused,1);
    h.click("[data-hide]");assert.equal(h.get("[data-answer]").hidden,true);
    if(page==="04-two-views")assert.equal(h.get("[data-watch-view]").hidden,false);
    if(page==="05-moments"){h.click("[data-next]");assert.match(h.get("[data-moment-count]").textContent,/04 \/ 05/);}
});
