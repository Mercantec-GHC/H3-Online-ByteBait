// Drive the same float and visible-time clock as the game using keyboard-accessible controls.
import { createFloatRun } from "../../ByteBait.Client/wwwroot/js/float-run.js";
import { drawFloatScene } from "../../ByteBait.Client/wwwroot/js/float-scene.js";
import { drawNordborgScene } from "../../ByteBait.Client/wwwroot/js/nordborg-scene.js";

const canvas = document.getElementById("scene");
const castButton = document.getElementById("cast");
const hookButton = document.getElementById("hook");
const status = document.getElementById("status");
const outcomes = { scared: "Fish scared away", hooked: "Fish hooked", escaped: "Fish escaped" };
let run = null;

castButton.addEventListener("click", () => {
    if (document.hidden) return;
    run?.dispose();
    run = createFloatRun(canvas, outcome => {
        run = null;
        hookButton.disabled = true;
        status.textContent = outcomes[outcome];
    }, Infinity, drawNordborgScene);
    hookButton.disabled = false;
    status.textContent = "Cast in progress.";
    run.start();
});
hookButton.addEventListener("click", () => run?.hook(performance.now()));

drawFloatScene(canvas, null, drawNordborgScene);
canvas.setAttribute("aria-label", "Open water");
castButton.disabled = false;
status.textContent = "Ready to cast.";
