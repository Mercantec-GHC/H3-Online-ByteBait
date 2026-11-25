/**
 * Owns the casting meter's canvas and animation clock. Blazor issues control
 * commands; each canvas records its drawn positions and reports the live zone to USB.
 */
const zones = [
    { start: 0, name: "red" },
    { start: 0.2, name: "yellow" },
    { start: 0.4, name: "green" },
    { start: 0.6, name: "yellow" },
    { start: 0.8, name: "red" },
];

const colors = { red: "#ed625d", yellow: "#f1c553", green: "#60bd82" };
const centerX = 160;
const centerY = 138;
const radius = 96;
const bandWidth = 16;
const sweepMs = 2000;
const historyWindowMs = 500;
const meters = new WeakMap();

// Canvas angles increase clockwise. Positions 0 to 1 follow the upper semicircle.
function angleAt(position) {
    return Math.PI * (1 + position);
}

/** Returns the zone starting at or before the position, including both meter endpoints. */
export function getZone(position) {
    if (!Number.isFinite(position) || position < 0 || position > 1) {
        throw new RangeError("The meter position must be a number between 0 and 1.");
    }

    return zones.findLast(zone => zone.start <= position).name;
}

/** Keep the last frame at or before the cutoff: it was still visible when the window opened. */
function trimHistory(history, nowMs) {
    const cutoff = nowMs - historyWindowMs;
    while (history.length > 1 && history[1].frameTimestampMs <= cutoff) {
        history.shift();
    }
}

/**
 * Reads the last frame drawn at or before targetMs on the performance.now() clock.
 * Returns its position, zone and draw time, or null outside the active 500 ms window.
 */
export function readMeterAt(canvas, targetMs) {
    const meter = meters.get(canvas);
    if (!meter || !canvas.isConnected || document.hidden || !Number.isFinite(targetMs)) {
        return null;
    }

    const nowMs = performance.now();
    if (targetMs < nowMs - historyWindowMs || targetMs > nowMs) return null;

    trimHistory(meter.history, nowMs);
    const frame = meter.history.findLast(entry => entry.frameTimestampMs <= targetMs);
    return frame ? { ...frame, zone: getZone(frame.position) } : null;
}

/** Immediately sends the live zone (or "off"), then reports changes until unsubscribed. */
export function subscribeZone(canvas, listener) {
    const meter = meters.get(canvas);
    if (!meter || !canvas.isConnected) {
        listener("off");
        return () => {};
    }
    meter.listeners.add(listener);
    listener(document.hidden ? "off" : meter.zone);
    return () => meter.listeners.delete(listener);
}

/** Draws the five zones and a short radial needle at the given normalized position. */
export function drawTimingMeter(canvas, position = 0) {
    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Unable to create the casting meter's drawing context.");
    }

    context.fillStyle = "#112b37";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.lineWidth = bandWidth;
    context.lineCap = "butt";

    for (const [index, zone] of zones.entries()) {
        const end = zones[index + 1]?.start ?? 1;
        context.beginPath();
        context.arc(centerX, centerY, radius, angleAt(zone.start), angleAt(end));
        context.strokeStyle = colors[zone.name];
        context.stroke();
    }

    const angle = angleAt(position);
    const dx = Math.cos(angle);
    const dy = Math.sin(angle);
    const innerRadius = radius - bandWidth / 2;
    const outerRadius = radius + bandWidth / 2;
    context.beginPath();
    context.moveTo(centerX + innerRadius * dx, centerY + innerRadius * dy);
    context.lineTo(centerX + outerRadius * dx, centerY + outerRadius * dy);
    context.strokeStyle = "#000000";
    context.lineWidth = 4;
    context.stroke();
}

/** Mounts a still meter. Only an explicit start command may schedule its first frame. */
export function initialize(canvas) {
    const host = document.getElementById("app");
    if (!canvas?.isConnected || !host?.contains(canvas)) {
        throw new Error("The casting meter requires a mounted canvas.");
    }
    if (meters.has(canvas)) return;

    drawTimingMeter(canvas);
    const history = [];
    const meter = { setRunning, restart, dispose, history, zone: "off", listeners: new Set() };
    let running = false;
    let elapsedMs = 0;
    let previousTimestamp = null;
    let frameId = null;
    const observer = new MutationObserver(() => {
        if (!canvas.isConnected) dispose();
    });

    function reportZone(zone) {
        if (meter.zone === zone) return;
        meter.zone = zone;
        for (const listener of meter.listeners) listener(zone);
    }

    function scheduleFrame() {
        const requestedId = requestAnimationFrame(timestamp => {
            // An already queued callback can arrive after pause, a tab switch or navigation.
            if (frameId !== requestedId) return;
            frameId = null;
            if (!canvas.isConnected) {
                dispose();
                return;
            }
            if (document.hidden) {
                syncClock();
                return;
            }

            if (previousTimestamp !== null) elapsedMs += timestamp - previousTimestamp;
            previousTimestamp = timestamp;
            // Reflect the phase on the return sweep, including overshoot on a late frame.
            const phase = (elapsedMs % (2 * sweepMs)) / sweepMs;
            const position = Math.min(phase, 2 - phase);
            drawTimingMeter(canvas, position);
            // The animation timestamp can precede the draw when a frame is delayed.
            const frameTimestampMs = performance.now();
            history.push({ position, frameTimestampMs });
            trimHistory(history, frameTimestampMs);
            reportZone(getZone(position));
            scheduleFrame();
        });
        frameId = requestedId;
    }

    function syncClock() {
        if (frameId !== null) cancelAnimationFrame(frameId);
        frameId = null;
        previousTimestamp = null;
        history.length = 0;
        if (running && !document.hidden) scheduleFrame();
        else reportZone("off");
    }

    function setRunning(next) {
        if (running === next) return;
        running = next;
        syncClock();
    }

    function restart() {
        elapsedMs = 0;
        running = true;
        syncClock();
        drawTimingMeter(canvas, 0);
        if (!document.hidden) {
            history.push({ position: 0, frameTimestampMs: performance.now() });
            reportZone("red");
        }
    }

    // Blazor may remove the canvas before its asynchronous disposal runs.
    function dispose() {
        if (!meters.delete(canvas)) return;
        setRunning(false);
        meter.listeners.clear();
        document.removeEventListener("visibilitychange", syncClock);
        observer.disconnect();
    }

    meters.set(canvas, meter);
    document.addEventListener("visibilitychange", syncClock);
    observer.observe(host, { childList: true, subtree: true });
}

function setMeterRunning(canvas, running) {
    const meter = meters.get(canvas);
    if (!canvas?.isConnected || !meter) {
        throw new Error("The casting meter is unavailable.");
    }
    meter.setRunning(running);
}

/** Starts or resumes the needle from its last drawn position and direction. */
export function start(canvas) {
    setMeterRunning(canvas, true);
}

/** Freezes the displayed needle and excludes paused time from future sweeps. */
export function stop(canvas) {
    setMeterRunning(canvas, false);
}

/** Start a new sweep and history at the red endpoint after a cast. */
export function restart(canvas) {
    meters.get(canvas)?.restart();
}

export function dispose(canvas) {
    meters.get(canvas)?.dispose();
}
