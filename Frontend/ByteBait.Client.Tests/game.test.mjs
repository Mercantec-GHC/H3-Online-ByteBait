// Tests the casting meter and its USB integration with controlled browser primitives.
import assert from "node:assert/strict";
import test from "node:test";
import * as meter from "../ByteBait.Client/wwwroot/js/timing-meter.js";
import { simulateUsb } from "./simulated-usb.mjs";
const { drawTimingMeter, getZone } = meter;

test("the endpoints and both sides of every casting zone boundary have the expected zone", () => {
    assert.equal(getZone(0), "red");
    assert.equal(getZone(0.5), "green");
    assert.equal(getZone(1), "red");
    for (const [boundary, left, right] of [
        [0.2, "red", "yellow"],
        [0.4, "yellow", "green"],
        [0.6, "green", "yellow"],
        [0.8, "yellow", "red"],
    ]) {
        assert.equal(getZone(boundary - 0.0001), left);
        assert.equal(getZone(boundary), right);
        assert.equal(getZone(boundary + 0.0001), right);
    }
});

test("an invalid meter position cannot produce a cast zone", () => {
    for (const position of [-0.1, 1.1, NaN, Infinity, "0.5", null]) {
        assert.throws(() => getZone(position), RangeError);
    }
});

/** Captures the painted paths so timing assertions check the needle actually shown to the player. */
function recordStrokes() {
    let path;
    return {
        strokes: [],
        beginPath() { path = []; },
        arc(...args) { path.push(args); },
        moveTo(x, y) { path.push([x, y]); },
        lineTo(x, y) { path.push([x, y]); },
        stroke() { this.strokes.push({ path, color: this.strokeStyle, width: this.lineWidth }); },
    };
}

test("the meter paints five adjoining 36-degree zones across the upper semicircle", () => {
    const context = {
        ...recordStrokes(),
        fillRect(x, y, width, height) {
            assert.deepEqual([x, y, width, height], [0, 0, 320, 180]);
            assert.equal(this.fillStyle, "#112b37");
        },
    };
    drawTimingMeter({ width: 320, height: 180, getContext: () => context });

    const palette = { red: "#ed625d", yellow: "#f1c553", green: "#60bd82" };
    const strokes = context.strokes.slice(0, -1);
    assert.equal(strokes.length, 5);
    assert.equal(context.lineCap, "butt");
    for (const [index, stroke] of strokes.entries()) {
        assert.equal(stroke.path.length, 1);
        assert.equal(stroke.width, 16);
        const [x, y, radius, startAngle, endAngle, anticlockwise = false] = stroke.path[0];
        assert.deepEqual([x, y, radius, anticlockwise], [160, 138, 96, false]);
        assert.ok(Math.abs(startAngle - (180 + index * 36) * Math.PI / 180) < 1e-12);
        assert.ok(Math.abs(endAngle - (216 + index * 36) * Math.PI / 180) < 1e-12);
        if (index > 0) assert.equal(startAngle, strokes[index - 1].path[0][4]);
        assert.equal(stroke.color, palette[getZone((index + 0.5) / 5)]);
    }
});

test("an unavailable 2D context produces a drawing error for the game page", () => {
    assert.throws(() => drawTimingMeter({ getContext: () => null }), /drawing context/);
});

const settleCallbacks = () => new Promise((resolve) => setImmediate(resolve));

/** Supplies only the canvas, clock, events, and DOM-removal observer used by the module. */
function createBrowser(t, { reducedMotion = false } = {}) {
    const frames = new Map();
    const observers = [];
    const canvases = new Set();
    const visibilityListeners = new Set();
    let nextFrameId = 1;
    let nowMs = 0;

    class Canvas {
        constructor({ connected = true, withContext = true } = {}) {
            this.width = 320;
            this.height = 180;
            this.isConnected = connected;
            this.draws = [];
            const draws = this.draws;
            this.context = withContext ? {
                ...recordStrokes(),
                fillStyle: "",
                imageSmoothingEnabled: true,
                fillRect(...rectangle) {
                    draws.push({ color: this.fillStyle, rectangle });
                },
            } : null;
            canvases.add(this);
        }

        getContext(type) {
            assert.equal(type, "2d");
            return this.context;
        }
    }

    const host = { contains: (element) => canvases.has(element) };
    const document = {
        hidden: false,
        getElementById: (id) => id === "app" ? host : null,
        addEventListener(type, listener) {
            assert.equal(type, "visibilitychange");
            visibilityListeners.add(listener);
        },
        removeEventListener(type, listener) {
            assert.equal(type, "visibilitychange");
            visibilityListeners.delete(listener);
        },
    };

    class Observer {
        constructor(callback) {
            this.callback = callback;
            this.active = false;
            this.disconnectCount = 0;
            observers.push(this);
        }

        observe(target, options) {
            assert.equal(target, host);
            assert.deepEqual(options, { childList: true, subtree: true });
            this.active = true;
        }

        disconnect() {
            this.active = false;
            this.disconnectCount++;
        }
    }

    const browserGlobals = {
        HTMLCanvasElement: Canvas,
        MutationObserver: Observer,
        document,
        performance: { now: () => nowMs, timeOrigin: Date.UTC(2026, 0, 1) },
        window: {
            matchMedia(query) {
                assert.equal(query, "(prefers-reduced-motion: reduce)");
                return { matches: reducedMotion };
            },
        },
        requestAnimationFrame(callback) {
            const id = nextFrameId++;
            frames.set(id, callback);
            return id;
        },
        cancelAnimationFrame(id) {
            frames.delete(id);
        },
    };

    const originals = new Map();
    for (const [name, value] of Object.entries(browserGlobals)) {
        originals.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
        Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
    }

    t.after(() => {
        for (const canvas of canvases) {
            canvas.isConnected = false;
        }
        for (const observer of observers) {
            if (observer.active) observer.callback();
        }
        for (const [name, descriptor] of originals) {
            if (descriptor) Object.defineProperty(globalThis, name, descriptor);
            else delete globalThis[name];
        }
    });

    return {
        canvas: new Canvas(),
        createCanvas: (options) => new Canvas(options),
        document,
        frames,
        observers,
        visibilityListeners,
        setTime(timestamp) { nowMs = timestamp; },
        runFrame(timestamp, drawTimeMs = timestamp) {
            nowMs = drawTimeMs;
            const callbacks = [...frames.values()];
            frames.clear();
            for (const callback of callbacks) callback(timestamp);
        },
        setHidden(hidden) {
            document.hidden = hidden;
            for (const listener of visibilityListeners) listener();
        },
        remove(canvas) {
            canvas.isConnected = false;
            // Removing any page subtree notifies every observer on the stable app host.
            for (const observer of observers) {
                if (observer.active) observer.callback();
            }
        },
    };
}

function assertNeedleAt(canvas, position) {
    const needle = canvas.context.strokes.at(-1);
    assert.equal(needle.color, "#000000");
    assert.equal(needle.width, 4);
    assert.equal(needle.path.length, 2);
    for (const [index, radius] of [88, 104].entries()) {
        const [x, y] = needle.path[index];
        const angle = position * Math.PI;
        assert.ok(Math.abs(x - (160 - radius * Math.cos(angle))) < 1e-8);
        assert.ok(Math.abs(y - (138 - radius * Math.sin(angle))) < 1e-8);
    }
}

/** Exercise the same byte stream, active canvas and meter module that are wired together on /game. */
async function startUsbMeter(t, onWrite) {
    const browser = createBrowser(t);
    const usb = simulateUsb(onWrite);
    t.after(() => usb.controller.dispose());
    meter.initialize(browser.canvas);
    meter.start(browser.canvas);
    browser.runFrame(0);
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    return { browser, usb };
}

function latestPong(usb) {
    return usb.writes.findLast(line => line.startsWith("PING:")).replace("PING", "PONG");
}

function ledLines(usb) {
    return usb.writes.filter(line => line.startsWith("LED:"));
}

test("the wire follows both sweeps without repeating a zone or waiting for a PONG", async t => {
    const { browser, usb } = await startUsbMeter(t);
    for (let time = 100; time <= 4000; time += 100) {
        browser.runFrame(time);
        await settleCallbacks();
    }
    assert.deepEqual(ledLines(usb), [
        "LED:RED\n", "LED:YELLOW\n", "LED:GREEN\n", "LED:YELLOW\n", "LED:RED\n",
        "LED:YELLOW\n", "LED:GREEN\n", "LED:YELLOW\n", "LED:RED\n",
    ]);
});

test("a ready, paused or hidden meter turns LEDs off until a visible active frame is drawn", async t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    meter.initialize(canvas);
    usb.controller.connect(canvas, meter);
    await settleCallbacks();
    meter.start(canvas);
    browser.runFrame(0);
    await settleCallbacks();
    browser.runFrame(1000);
    await settleCallbacks();
    meter.stop(canvas);
    meter.stop(canvas);
    browser.setHidden(true);
    browser.setHidden(false);
    await settleCallbacks();
    assert.deepEqual(ledLines(usb), ["LED:OFF\n", "LED:RED\n", "LED:GREEN\n", "LED:OFF\n"]);

    meter.start(canvas);
    browser.runFrame(2000);
    await settleCallbacks();
    browser.setHidden(true);
    await settleCallbacks();
    browser.setHidden(false);
    browser.runFrame(3000);
    await settleCallbacks();
    browser.remove(canvas);
    await usb.controller.dispose();
    assert.deepEqual(ledLines(usb).slice(4), ["LED:GREEN\n", "LED:OFF\n", "LED:GREEN\n", "LED:OFF\n"]);
    assert.equal(usb.closes, 1);
});

test("disconnect turns the lights off; opening mid-sweep syncs green and unplug drops pending colors", async t => {
    const { browser, usb } = await startUsbMeter(t);
    await usb.controller.disconnect();
    browser.runFrame(1000);
    await settleCallbacks();
    assert.deepEqual(ledLines(usb), ["LED:RED\n", "LED:OFF\n"]);
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    assert.equal(usb.writes.at(-1), "LED:GREEN\n");
    browser.runFrame(1400);
    usb.unplug();
    await settleCallbacks();
    browser.runFrame(1500);
    assert.deepEqual(ledLines(usb), ["LED:RED\n", "LED:OFF\n", "LED:GREEN\n"]);
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    assert.equal(usb.writes.at(-1), "LED:YELLOW\n");
    await usb.controller.dispose();
    assert.equal(usb.writes.at(-1), "LED:OFF\n");
});

test("a slow writer sends only the newest waiting zone and keeps receiving THROW", async t => {
    const gate = Promise.withResolvers();
    const { browser, usb } = await startUsbMeter(t, line => line === "LED:RED\n" ? gate.promise : undefined);
    t.after(() => gate.resolve());
    for (const time of [400, 800, 1200, 1600, 2600, 3000]) {
        browser.runFrame(time);
        await settleCallbacks();
    }
    usb.feed("THROW:70\n");
    await settleCallbacks();
    assert.equal(usb.calls.at(-1)[1].strength, 70);
    assert.deepEqual(ledLines(usb), ["LED:RED\n"]);
    gate.resolve();
    await settleCallbacks();
    assert.deepEqual(ledLines(usb), ["LED:RED\n", "LED:GREEN\n"]);
});

test("disconnect releases a stalled LED writer and discards colors waiting behind it", async t => {
    t.mock.timers.enable({ apis: ["setTimeout"] });
    const { browser, usb } = await startUsbMeter(t, (line, stream) => {
        if (line === "LED:RED\n") {
            return new Promise(resolve => stream.signal.addEventListener("abort", resolve, { once: true }));
        }
    });
    browser.runFrame(1000);
    const closed = usb.controller.disconnect();
    t.mock.timers.tick(250);
    await closed;
    assert.equal(usb.closes, 1);
    assert.deepEqual(ledLines(usb), ["LED:RED\n"]);
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    assert.equal(usb.writes.at(-1), "LED:GREEN\n");
});

test("a THROW received at 1260 ms scores the green frame at 990 and timestamps the trigger at 1000", async t => {
    const { browser, usb } = await startUsbMeter(t);
    browser.setTime(20);
    usb.feed(latestPong(usb));
    await settleCallbacks();
    browser.runFrame(990);
    browser.runFrame(1260);
    assertNeedleAt(browser.canvas, 0.63);
    assert.equal(meter.readMeterAt(browser.canvas, 1260).zone, "yellow");
    usb.feed("THROW:72\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbThrow", {
        strength: 72, receivedAtMs: 1260, zone: "green", castAt: "2026-01-01T00:00:01.000Z", reason: null,
    }]);
    assert.equal(ledLines(usb).at(-1), "LED:YELLOW\n");
    browser.runFrame(1400);
    assertNeedleAt(browser.canvas, 0.7);

    meter.stop(browser.canvas);
    usb.feed("THROW:11\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbThrow", {
        strength: 11, receivedAtMs: 1400, zone: null, castAt: null, reason: "missingHistory",
    }]);
    meter.start(browser.canvas);
    browser.runFrame(1410);
    browser.runFrame(1710);
    browser.setHidden(true);
    usb.feed("THROW:12\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbThrow", {
        strength: 12, receivedAtMs: 1710, zone: null, castAt: null, reason: "missingHistory",
    }]);
});

test("a newer matching echo changes the trigger estimate only for subsequent throws in the session", async t => {
    t.mock.timers.enable({ apis: ["setTimeout"] });
    const { browser, usb } = await startUsbMeter(t);
    browser.setTime(20);
    usb.feed(latestPong(usb));
    await settleCallbacks();
    browser.runFrame(5000);
    t.mock.timers.tick(5000);
    await settleCallbacks();
    browser.runFrame(5240);
    browser.runFrame(5500);
    usb.feed("THROW:75\n" + latestPong(usb) + "THROW:75\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.slice(-3), [
        ["OnUsbThrow", { strength: 75, receivedAtMs: 5500, zone: "yellow", castAt: "2026-01-01T00:00:05.240Z", reason: null }],
        ["OnUsbLatency", 500, 250],
        ["OnUsbThrow", { strength: 75, receivedAtMs: 5500, zone: "green", castAt: "2026-01-01T00:00:05.000Z", reason: null }],
    ]);
});

test("the 500 ms history limit includes its boundary and uses the unrounded latency sample", async t => {
    const { browser, usb } = await startUsbMeter(t);
    browser.setTime(500);
    usb.feed(latestPong(usb));
    await settleCallbacks();
    browser.runFrame(1000);
    browser.runFrame(1500);
    usb.feed("THROW:90\n");
    await settleCallbacks();
    assert.equal(usb.calls.at(-1)[1].zone, "green");
    assert.equal(usb.calls.at(-1)[1].castAt, "2026-01-01T00:00:01.000Z");

    await usb.controller.disconnect();
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    browser.runFrame(2000.0625); // RTT 500.0625 ms puts the trigger just outside the retained interval.
    usb.feed(latestPong(usb) + "THROW:80\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbThrow", {
        strength: 80, receivedAtMs: 2000.0625, zone: null, castAt: null, reason: "missingHistory",
    }]);
});

test("reopening requires a fresh echo and evaluates a batch of casts and a zero-RTT PONG in wire order", async t => {
    const { browser, usb } = await startUsbMeter(t);
    const oldPong = latestPong(usb);
    usb.feed(oldPong + "THROW:10\n"); // Even with a sample, the target predates the first frame.
    await settleCallbacks();
    assert.equal(usb.calls.at(-1)[1].reason, "missingHistory");
    await usb.controller.disconnect();
    browser.runFrame(990);
    browser.runFrame(1250);
    usb.controller.connect(browser.canvas, meter);
    await settleCallbacks();
    usb.feed(oldPong + "THROW:1\n" + latestPong(usb) + "THROW:2\n");
    await settleCallbacks();
    assert.deepEqual(usb.calls.slice(-3), [
        ["OnUsbThrow", { strength: 1, receivedAtMs: 1250, zone: null, castAt: null, reason: "missingLatency" }],
        ["OnUsbLatency", 0, 0],
        ["OnUsbThrow", { strength: 2, receivedAtMs: 1250, zone: "green", castAt: "2026-01-01T00:00:01.000Z", reason: null }],
    ]);
});

test("the meter waits for manual start, including with reduced motion enabled", t => {
    const browser = createBrowser(t, { reducedMotion: true });
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.initialize(canvas);
    assertNeedleAt(canvas, 0);
    assert.equal(canvas.draws.length, 1);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 1);
    assert.equal(browser.observers.length, 1);
    meter.start(canvas);
    browser.runFrame(5000);
    browser.runFrame(6000);
    assertNeedleAt(canvas, 0.5);
});

test("at 30 and 60 fps the needle reaches the same points on both sweeps", t => {
    const browser = createBrowser(t);
    for (const fps of [30, 60]) {
        const canvas = browser.createCanvas();
        meter.initialize(canvas);
        meter.start(canvas);
        browser.runFrame(0);
        const checkpoints = [0, 0.5, 1, 0.5, 0];
        for (let frame = 1; frame <= 4 * fps; frame++) {
            browser.runFrame(frame * 1000 / fps);
            if (frame % fps === 0) assertNeedleAt(canvas, checkpoints[frame / fps]);
        }
        browser.remove(canvas);
    }
});

test("late frames carry overshoot past both turning points", t => {
    const browser = createBrowser(t);
    meter.initialize(browser.canvas);
    meter.start(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(1980);
    browser.runFrame(2020);
    assertNeedleAt(browser.canvas, 0.99);
    browser.runFrame(4040);
    assertNeedleAt(browser.canvas, 0.02);
});

test("pausing on the return sweep preserves the frame and direction through rapid controls", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.start(canvas);
    browser.runFrame(0);
    browser.runFrame(2500);
    assertNeedleAt(canvas, 0.75);
    const drawCount = canvas.draws.length;
    const [staleFrame] = browser.frames.values();
    for (let i = 0; i < 5; i++) {
        meter.stop(canvas);
        meter.stop(canvas);
        assert.equal(browser.frames.size, 0);
        meter.start(canvas);
        meter.start(canvas);
        assert.equal(browser.frames.size, 1);
    }
    staleFrame(8000);
    assert.equal(canvas.draws.length, drawCount);
    browser.runFrame(9000);
    assertNeedleAt(canvas, 0.75);
    browser.runFrame(9500);
    assertNeedleAt(canvas, 0.5);
});

test("a tab switch freezes a running needle and never restarts a paused meter", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.start(canvas);
    browser.runFrame(1000);
    browser.runFrame(1500);
    assertNeedleAt(canvas, 0.25);
    const drawCount = canvas.draws.length;
    const [staleFrame] = browser.frames.values();
    browser.setHidden(true);
    assert.equal(browser.frames.size, 0);
    browser.runFrame(9000);
    browser.setHidden(false);
    staleFrame(9500);
    assert.equal(canvas.draws.length, drawCount);
    browser.runFrame(10000);
    assertNeedleAt(canvas, 0.25);
    browser.runFrame(10500);
    assertNeedleAt(canvas, 0.5);
    meter.stop(canvas);
    browser.setHidden(true);
    browser.setHidden(false);
    assert.equal(browser.frames.size, 0);
});

test("navigation releases meter resources and a fresh canvas starts at rest", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.start(canvas);
    const [staleFrame] = browser.frames.values();
    browser.remove(canvas);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.equal(browser.observers[0].disconnectCount, 1);
    assert.equal(browser.frames.size, 0);
    assert.throws(() => meter.start(canvas), /unavailable/);
    assert.throws(() => meter.stop(canvas), /unavailable/);
    const next = browser.createCanvas();
    meter.initialize(next);
    staleFrame(4000);
    assert.equal(canvas.draws.length, 1);
    assert.equal(browser.frames.size, 0);
    assertNeedleAt(next, 0);
});

test("failed initialization leaves no meter ready to animate", t => {
    const browser = createBrowser(t);
    const canvas = browser.createCanvas({ withContext: false });
    assert.throws(() => meter.initialize(canvas), /drawing context/);
    assert.throws(() => meter.start(canvas), /unavailable/);
    assert.throws(() => meter.initialize(browser.createCanvas({ connected: false })), /mounted canvas/);
    assert.equal(browser.observers.length, 0);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.equal(browser.frames.size, 0);
});

test("history changes zone when the new needle has been drawn", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.start(canvas);
    browser.runFrame(0);
    browser.runFrame(1180);
    assertNeedleAt(canvas, 0.59);

    // This frame starts at the zone boundary, but its black stroke finishes at 1220 ms.
    const context = canvas.context;
    const stroke = context.stroke;
    context.stroke = function () {
        stroke.call(this);
        if (this.strokeStyle === "#000000") browser.setTime(1220);
    };
    browser.runFrame(1200, 1215);
    assertNeedleAt(canvas, 0.6);
    const green = { position: 0.59, zone: "green", frameTimestampMs: 1180 };
    assert.deepEqual(meter.readMeterAt(canvas, 1210), green);
    assert.deepEqual(meter.readMeterAt(canvas, 1219), green);
    assert.deepEqual(meter.readMeterAt(canvas, 1220), { position: 0.6, zone: "yellow", frameTimestampMs: 1220 });
});

test("the 500 ms window includes its oldest instant throughout a visible rendering stall", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    meter.start(canvas);
    browser.runFrame(0);
    browser.runFrame(900);
    browser.runFrame(1000);
    browser.setTime(1700);

    const green = { position: 0.5, zone: "green", frameTimestampMs: 1000 };
    assert.deepEqual(meter.readMeterAt(canvas, 1600), green);
    assert.deepEqual(meter.readMeterAt(canvas, 1200), green);
    for (const target of [1199, 1701, NaN, Infinity, -1, null, "1600"]) {
        assert.equal(meter.readMeterAt(canvas, target), null);
    }

    browser.runFrame(1700);
    assert.deepEqual(meter.readMeterAt(canvas, 1200), green);
    assert.deepEqual(meter.readMeterAt(canvas, 1699), green);
    assert.equal(meter.readMeterAt(canvas, 1700).zone, "red");
});

test("each active interval starts its history with the first new frame", t => {
    const browser = createBrowser(t);
    const { canvas } = browser;
    meter.initialize(canvas);
    assert.equal(meter.readMeterAt(canvas, 0), null);
    meter.start(canvas);
    assert.equal(meter.readMeterAt(canvas, 0), null);
    browser.runFrame(10);
    browser.runFrame(110);
    meter.start(canvas);
    assert.equal(meter.readMeterAt(canvas, 100).frameTimestampMs, 10);

    meter.stop(canvas);
    assert.equal(meter.readMeterAt(canvas, 110), null);
    meter.start(canvas);
    assert.equal(meter.readMeterAt(canvas, 110), null);
    browser.runFrame(150);
    assert.equal(meter.readMeterAt(canvas, 149), null);
    assert.equal(meter.readMeterAt(canvas, 150).frameTimestampMs, 150);

    const [staleFrame] = browser.frames.values();
    browser.setHidden(true);
    assert.equal(meter.readMeterAt(canvas, 150), null);
    browser.setHidden(false);
    staleFrame(170);
    assert.equal(meter.readMeterAt(canvas, 150), null);
    browser.runFrame(200);
    assert.equal(meter.readMeterAt(canvas, 199), null);
    assert.equal(meter.readMeterAt(canvas, 200).frameTimestampMs, 200);
});

test("canvases have independent histories and navigation discards the removed meter", t => {
    const browser = createBrowser(t);
    const first = browser.canvas;
    const second = browser.createCanvas();
    meter.initialize(first);
    meter.start(first);
    browser.runFrame(0);
    meter.initialize(second);
    meter.start(second);
    browser.runFrame(1000);

    assert.equal(meter.readMeterAt(first, 1000).zone, "green");
    assert.equal(meter.readMeterAt(second, 999), null);
    assert.equal(meter.readMeterAt(second, 1000).zone, "red");
    browser.remove(first);
    assert.equal(meter.readMeterAt(first, 1000), null);
    assert.equal(meter.readMeterAt(second, 1000).zone, "red");
    assert.equal(meter.readMeterAt(browser.createCanvas(), 1000), null);
});
