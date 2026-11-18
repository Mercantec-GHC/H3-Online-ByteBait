/**
 * Owns the canvas animation demo. Blazor supplies the mounted canvas and keeps ownership
 * of the surrounding HTML. Blazor controls at most one loop per canvas; JavaScript reports
 * turning points back to that component, and navigation cleans up both the loop and receiver.
 */
const demos = new WeakMap();
const squareSize = 12;
const edgePadding = 8;
const pixelsPerSecond = 48;
const maxDeltaSeconds = 0.1;

/**
 * Initializes a mounted canvas once with its .NET callback receiver and draws a still image.
 * Returns whether animation is allowed by the reduced-motion preference sampled at startup.
 * Throws for an invalid element, host, callback receiver, drawing size, or 2D context. A canvas removed during
 * Blazor's asynchronous module import is ignored without allocating animation resources.
 */
export function initialize(canvas, dotNetReference) {
    if (!(canvas instanceof HTMLCanvasElement)) {
        throw new TypeError("The game demo requires a canvas element.");
    }

    if (!canvas.isConnected) {
        return false;
    }

    const existing = demos.get(canvas);
    if (existing) {
        return existing.canAnimate;
    }

    const host = document.getElementById("app");
    if (!host || !host.contains(canvas)) {
        throw new Error("The game canvas must be mounted inside the app.");
    }

    if (typeof dotNetReference?.invokeMethodAsync !== "function") {
        throw new TypeError("The game demo requires a .NET callback reference.");
    }

    const travel = canvas.width - edgePadding * 2 - squareSize;
    if (travel <= 0 || canvas.height < squareSize) {
        throw new Error("The game canvas is too small for the animation demo.");
    }

    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("The game demo could not create a 2D drawing context.");
    }

    const canAnimate = !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    let elapsedSeconds = 0;
    let previousTimestamp = null;
    let frameId = null;
    let disposed = false;
    let running = false;
    let generation = 0;
    let turnCount = 0;
    let notificationsEnabled = true;

    // Observe the stable app host: the canvas's immediate parent disappears with the page.
    const observer = new MutationObserver(() => {
        if (!canvas.isConnected) {
            dispose();
        }
    });

    function resetTiming() {
        previousTimestamp = null;
    }

    /** Advances simulation time independently of the display's refresh rate. */
    function update(timestamp) {
        const deltaSeconds = previousTimestamp === null
            ? 0
            : Math.min(Math.max((timestamp - previousTimestamp) / 1000, 0), maxDeltaSeconds);

        previousTimestamp = timestamp;
        elapsedSeconds += deltaSeconds;

        // Count full edge crossings, not rounded pixel positions that can repeat near an edge.
        const reachedTurns = Math.floor(elapsedSeconds * pixelsPerSecond / travel);
        while (turnCount < reachedTurns) {
            turnCount++;
            void notifyTurn(turnCount % 2 === 1 ? "right" : "left", turnCount);
        }
    }

    /** Reports an edge crossing without waiting in the render loop or retrying failed calls. */
    async function notifyTurn(side, count) {
        if (disposed || !notificationsEnabled || !canvas.isConnected) {
            return;
        }

        try {
            await dotNetReference.invokeMethodAsync("OnDemoTurnedAsync", side, count);
        } catch (error) {
            // Navigation can remove the .NET receiver while an earlier call is still pending.
            if (disposed || !canvas.isConnected || !notificationsEnabled) {
                return;
            }

            notificationsEnabled = false;
            dotNetReference = null;
            console.error("Unable to report a ByteBait demo turn.", error);
        }
    }

    /** Redraws the bitmap; reflected travel preserves movement past either turning point. */
    function draw() {
        const phase = (elapsedSeconds * pixelsPerSecond) % (travel * 2);
        const x = edgePadding + Math.min(phase, travel * 2 - phase);
        const y = (canvas.height - squareSize) / 2;

        context.fillStyle = "#112b37";
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.fillStyle = "#8ec99b";
        context.fillRect(Math.round(x), Math.round(y), squareSize, squareSize);
    }

    function frame(timestamp, expectedGeneration) {
        // A delayed callback from an earlier run must not overwrite the current frame handle.
        if (disposed || !running || expectedGeneration !== generation) {
            return;
        }

        frameId = null;
        if (!canvas.isConnected) {
            dispose();
            return;
        }

        if (document.hidden) {
            resetTiming();
        } else {
            update(timestamp);
            draw();
        }

        frameId = requestAnimationFrame((nextTimestamp) => frame(nextTimestamp, expectedGeneration));
    }

    /** Resumes the saved position with a fresh time baseline, without duplicating the loop. */
    function startAnimation() {
        if (!canAnimate) {
            return false;
        }

        if (!running) {
            running = true;
            resetTiming();
            const currentGeneration = ++generation;
            frameId = requestAnimationFrame((timestamp) => frame(timestamp, currentGeneration));
        }

        return true;
    }

    /** Pauses animation while preserving its bitmap, elapsed time, observer, and listener. */
    function stopAnimation() {
        if (!running) {
            return;
        }

        running = false;
        generation++;
        resetTiming();
        if (frameId !== null) {
            cancelAnimationFrame(frameId);
            frameId = null;
        }
    }

    /** Stops callbacks and removes all browser resources. Safe to call more than once. */
    function dispose() {
        if (disposed) {
            return;
        }

        stopAnimation();
        disposed = true;
        notificationsEnabled = false;
        // C# owns disposal of the .NET reference; JavaScript only releases its proxy.
        dotNetReference = null;
        observer.disconnect();
        document.removeEventListener("visibilitychange", resetTiming);
        demos.delete(canvas);
    }

    demos.set(canvas, { canAnimate, start: startAnimation, stop: stopAnimation, dispose });
    observer.observe(host, { childList: true, subtree: true });
    context.imageSmoothingEnabled = false;
    draw();

    if (canAnimate) {
        document.addEventListener("visibilitychange", resetTiming);
    }

    return canAnimate;
}

/** Starts or resumes a live, initialized demo. Returns true only if it is running. */
export function start(canvas) {
    return getDemo(canvas).start();
}

/** Pauses a live, initialized demo. Repeated calls leave its position unchanged. */
export function stop(canvas) {
    getDemo(canvas).stop();
}

/** Rejects stale commands instead of silently pretending that an unavailable demo changed state. */
function getDemo(canvas) {
    const demo = demos.get(canvas);
    if (!demo || !canvas.isConnected) {
        demo?.dispose();
        throw new Error("The game demo is not initialized or its canvas was removed.");
    }

    return demo;
}
