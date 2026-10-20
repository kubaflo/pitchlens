const concepts = [
    { id:"01", slug:"the-lion", name:"The Lion", line:"The most direct league connection.", copy:"Official Premier League mark, uninterrupted on a crisp white app tile. A confident, immediately recognisable direction with no extra symbol competing for attention.", tags:["Official mark","Clean white","Recognition first"] },
    { id:"02", slug:"matchday", name:"Matchday", line:"A compact broadcast identity.", copy:"A mint matchday header sits above the official mark. The tiny PLENS label makes this feel like the companion app, not a replacement for the league's own app.", tags:["Official mark","Broadcast stripe","App signature"] },
    { id:"03", slug:"the-lens", name:"The Lens", line:"Premier League, through PitchLens.", copy:"The official lion sits inside an original mint lens. The magnifier is the differentiator: this is about seeing and explaining the football, not just another scores app.", tags:["Official mark","Original lens","Analysis identity"] },
    { id:"04", slug:"under-the-lights", name:"Under the Lights", line:"The energy of an evening fixture.", copy:"A midnight tile with sharp mint and lilac light cuts. The official mark remains clean and intact in its own white field while the composition supplies the match-night energy.", tags:["Official mark","Night palette","Dynamic framing"] },
    { id:"05", slug:"pitchlens-p", name:"PitchLens P", line:"Our app first. League connection second.", copy:"A bold Barlow P is the primary app signature. The official lion becomes a secondary badge, so the identity stays recognisable as PitchLens even among other football apps.", tags:["Original monogram","Official badge","Mint-led"] },
    { id:"06", slug:"replay-ring", name:"Replay Ring", line:"Built around the moment worth watching.", copy:"An original replay ring surrounds the official mark. A mint play cue gives the icon a clear relationship to the app's replay and moment-explanation experience.", tags:["Official mark","Replay cue","White tile"] },
    { id:"07", slug:"on-the-pitch", name:"On the Pitch", line:"The football stays central.", copy:"A deep-green pitch and fine field markings put the official lion on the centre spot. This keeps the match-viewing character without the rejected crown-and-ball symbol.", tags:["Official mark","Pitch geometry","Football green"] },
    { id:"08", slug:"clubhouse", name:"Clubhouse", line:"A sporting badge, not another app square.", copy:"An original mint-edged shield frames the unmodified official mark. More traditional football character, with the restrained purple and white combination doing most of the work.", tags:["Official mark","Original shield","Sporting badge"] },
    { id:"09", slug:"the-read", name:"The Read", line:"From football action to useful insight.", copy:"The official mark shares the tile with an original four-bar analysis motif. It signals the evidence, comparisons and visual explanation side of PitchLens at a glance.", tags:["Official mark","Analysis bars","Purple-led"] },
    { id:"10", slug:"split-signal", name:"Split Signal", line:"A bolder mark for the dock.", copy:"A hard purple-and-mint split gives the app its own silhouette. A white card holding the official mark bridges the two sides, with a slight angle to make the composition feel alive.", tags:["Official mark","Graphic split","Strong silhouette"] }
];
const file = c => `${c.id}-${c.slug}.html`;
const mark = '<span class="mark"><img src="assets/premier-league-official.png" alt="Official Premier League lion mark"></span>';
function icon(c, size) {
    let detail = "";
    if(c.id==="02") detail='<span class="detail">PLENS</span>';
    if(c.id==="05") detail='<span class="letter" aria-hidden="true">P</span>';
    if(c.id==="06") detail='<span class="detail"></span>';
    if(c.id==="07") detail='<svg viewBox="0 0 100 100" aria-hidden="true"><g fill="none" stroke="#8bc3a5" stroke-width="1"><rect x="10" y="10" width="80" height="80" rx="1"/><path d="M10 50H90M28 10V27H72V10M28 90V73H72V90"/><circle cx="50" cy="50" r="12"/></g></svg>';
    if(c.id==="09") detail='<span class="bars" aria-hidden="true"><span></span><span></span><span></span><span></span></span>';
    return `<div class="icon c${c.id}"${size ? ` style="--size:${size}px"`:""}>${mark}${detail}</div>`;
}
const header='<header class="top"><a class="brand" href="index.html">PitchLens <span>Identity review / 10 directions</span></a><span class="status">Selected: 10 / Split Signal</span></header>';
const footer='<footer>Official Premier League mark: <a href="https://www.premierleague.com/resources/v1.55.23/i/favicon/favicon-196x196.png">premierleague.com official asset</a>. Used in these prototype mockups at your request; rights remain with the Premier League. These are independent PitchLens design explorations, not an endorsement or a claim of official-app status. Original layouts and frames; freely licensed local Barlow type. No site scripts, proprietary fonts or analytics.</footer>';
const id=document.body.dataset.concept;
let content;
if(!id) {
    content=`<main><div class="eyebrow">Logo / App icon study</div><h1>Ten ways to look like matchday.</h1><p class="intro">Actual Premier League assets. Ten distinct PitchLens directions. Open any mock to compare the large mark, small sizes and dock context.</p><div class="grid">${concepts.map(c=>`<a class="card" href="${file(c)}"><div class="card-stage">${icon(c)}</div><div class="card-copy"><span class="number">${c.id} / ${c.id==="10"?"SELECTED":"APP IDENTITY"}</span><h2>${c.name}</h2><p>${c.line}</p></div></a>`).join("")}</div><p class="legend"><strong>Selected: 10 / Split Signal.</strong> This direction is exported to the app icon and splash. The other nine directions remain review-only explorations.</p></main>`;
} else {
    const c=concepts.find(c=>c.id===id);
    if(!c) throw new Error("Unknown logo mock.");
    document.title=`${c.id} ${c.name} / PitchLens logo review`;
    const sizes=[24,40,64,96];
    content=`<main><section class="hero"><div><div class="eyebrow">Direction ${c.id} / 10</div><h1>${c.name}.</h1><p class="intro">${c.copy}</p><div class="tags">${c.tags.map(t=>`<span>${t}</span>`).join("")}</div></div><div class="hero-icon">${icon(c)}</div></section><section class="preview-row"><div class="preview"><h3>At actual small sizes</h3><div class="sizes">${sizes.map(s=>`<div>${icon(c,s)}<div class="size-label">${s} px</div></div>`).join("")}</div></div><div class="preview dark"><h3>Independent desktop app / Dock study</h3><div class="dock"><span class="neutral"></span>${icon(c)}<span class="neutral"></span></div><div class="dock-caption">PitchLens</div></div></section><nav class="page-actions"><a href="index.html">← All 10 directions</a><a href="${file(concepts[(Number(id))%10])}">Next direction →</a></nav></main>`;
}
document.body.innerHTML=header+content+footer;
