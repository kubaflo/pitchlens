import {popupHost} from "./popups.mjs";

// Available before the replay canvas exists; pitch popups acquire this same one-stack host.
document.addEventListener("click",event=>{
    const button=event.target.closest?.("[data-open-popup]");
    if(button?.dataset.openPopup==="reading-dialog")popupHost(document,window).open("reading-dialog");
});
