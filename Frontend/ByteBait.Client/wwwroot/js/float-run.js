/**
 * Owns one float on the canvas. Rendering and USB pulls count only visible
 * time after the cast is confirmed, using the browser's monotonic clock.
 */
import { createHookAttempt, hookDurationMs } from "./hook-attempt.js";
import { drawFloatScene } from "./float-scene.js";

const descriptions = {
    waiting: "Float with its red tip and pale body above water",
    nibble: "Only the red tip of the float is above water",
    bite: "The float is underwater. Two rings mark the spot.",
};

/** Draw immediately on the chosen background. start() begins the cadence after confirmation. */
export function createFloatRun(canvas, onOutcome, deadlineMs = Infinity, drawBackground) {
    const attempt = createHookAttempt();
    let started = false;
    let disposed = false;
    let frameId = null;
    let segmentStart = null;
    let visibleMs = 0;
    let drawn;

    function paint(state) {
        if (drawn === state) return;
        drawFloatScene(canvas, state, drawBackground);
        canvas.setAttribute("aria-label", descriptions[state] ?? "Open water");
        drawn = state;
    }

    function dispose() {
        disposed = true;
        cancelAnimationFrame(frameId);
        frameId = null;
        document.removeEventListener("visibilitychange", syncVisibility);
    }

    function display(state) {
        if (state in descriptions) {
            paint(state);
        } else {
            dispose();
            paint(null);
            onOutcome(state);
        }
    }

    function frame() {
        frameId = null;
        if (disposed || !canvas.isConnected) return dispose();
        if (document.hidden || segmentStart === null) return;
        display(attempt.update(visibleMs + performance.now() - segmentStart));
        if (!disposed) frameId = requestAnimationFrame(frame);
    }

    function syncVisibility() {
        if (!started || disposed) return;
        const now = performance.now();
        if (document.hidden) {
            if (segmentStart !== null) visibleMs += now - segmentStart;
            segmentStart = null;
            cancelAnimationFrame(frameId);
            frameId = null;
        } else if (segmentStart === null) {
            // USB heartbeats do not extend the board's motion window after a THROW.
            if (now + Math.max(0, hookDurationMs - visibleMs) > deadlineMs) return display("expired");
            segmentStart = now;
            frame();
        }
    }

    paint("waiting");
    document.addEventListener("visibilitychange", syncVisibility);
    return {
        start() {
            started = true;
            syncVisibility();
        },
        hook(receivedAtMs) {
            if (disposed || document.hidden || segmentStart === null || receivedAtMs < segmentStart) return;
            display(attempt.hook(visibleMs + receivedAtMs - segmentStart));
        },
        dispose,
    };
}
