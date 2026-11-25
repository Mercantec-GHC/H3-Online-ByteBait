/**
 * Coordinates the casting meter, HTTP receipt and one float on the game canvas.
 * The USB reader claims a scored throw synchronously before it notifies Blazor.
 */
import * as meter from "./timing-meter.js";
import { createFloatRun } from "./float-run.js";

// The proposed Oplà motion firmware accepts hooks for 60 seconds after a throw.
const motionWindowMs = 60000;

export function createFishingGame(canvas, receiver) {
    meter.initialize(canvas);
    let phase = "ready";
    let outcome = null;
    let attempt = null;
    let nextId = 0;
    let disposed = false;

    function report(nextPhase, nextOutcome = outcome, hookedCast = null) {
        phase = nextPhase;
        outcome = nextOutcome;
        void receiver.invokeMethodAsync("OnGameState", phase, outcome, hookedCast).catch(error => {
            if (!disposed) console.error("Unable to update the game status.", error);
        });
    }

    function rearm(result) {
        attempt?.run.dispose();
        attempt = null;
        canvas.setAttribute("aria-label", "Casting meter");
        meter.restart(canvas);
        report("running", result);
    }

    function resolve(result) {
        if (result === "hooked") report("hooked", result, attempt.cast);
        else rearm(result);
    }

    return {
        readMeterAt: targetMs => meter.readMeterAt(canvas, targetMs),
        subscribeZone: listener => meter.subscribeZone(canvas, listener),
        hasCast: () => attempt !== null,
        beginCast(receivedAtMs) {
            meter.stop(canvas); // Replaces queued zone colors with OFF before starting the POST.
            const id = ++nextId;
            attempt = { id, cast: null, run: createFloatRun(canvas, resolve, receivedAtMs + motionWindowMs) };
            report("saving", null);
            return id;
        },
        finishSave(id, startFloat, cast = null) {
            if (disposed || !canvas.isConnected || phase !== "saving" || attempt?.id !== id) return false;
            if (startFloat) {
                attempt.cast = cast;
                report("fishing");
                attempt.run.start();
            } else {
                rearm(null);
            }
            return true;
        },
        hook(receivedAtMs) {
            if (!disposed && phase === "fishing") attempt.run.hook(receivedAtMs);
        },
        disconnectCast() {
            if (!disposed && canvas.isConnected && attempt) rearm("disconnected");
        },
        start() {
            if (disposed || (phase !== "ready" && phase !== "paused")) return;
            meter.start(canvas);
            report("running");
        },
        pause() {
            if (disposed || phase !== "running") return;
            meter.stop(canvas);
            report("paused");
        },
        castAgain() {
            if (!disposed && phase === "hooked") rearm(null);
        },
        dispose() {
            disposed = true;
            attempt?.run.dispose();
            attempt = null;
            meter.dispose(canvas);
        },
    };
}
