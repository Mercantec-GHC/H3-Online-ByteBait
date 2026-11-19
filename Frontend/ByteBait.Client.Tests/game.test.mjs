// Tests the casting meter and the earlier animation module with controlled browser primitives.
import assert from "node:assert/strict";
import test from "node:test";
import { initialize as initializeModule, start, stop } from "../ByteBait.Client/wwwroot/js/game.js";
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

const noOpReceiver = { invokeMethodAsync: async () => {} };

// Existing animation tests need a valid receiver without depending on its event payloads.
function initialize(canvas, receiver = noOpReceiver) {
    return initializeModule(canvas, receiver);
}

/** Records the public JS-to-.NET contract; a custom result can simulate interop failures. */
function createReceiver(invoke = async () => {}) {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(method, side, turnCount) {
            calls.push({ method, side, turnCount });
            return invoke(method, side, turnCount);
        },
    };
}

function runFrames(browser, from, to) {
    for (let timestamp = from; timestamp <= to; timestamp += 100) {
        browser.runFrame(timestamp);
    }
}

function turn(side, turnCount) {
    return { method: "OnDemoTurnedAsync", side, turnCount };
}

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

function square(canvas) {
    return canvas.draws.findLast((draw) => draw.color === "#8ec99b").rectangle;
}

function initializeAndStart(canvas, receiver) {
    assert.equal(initialize(canvas, receiver), true);
    assert.equal(start(canvas), true);
}

test("initialization draws a still image and repeated initialization never starts animation", (t) => {
    const browser = createBrowser(t);
    assert.equal(initialize(browser.canvas), true);

    assert.equal(initialize(browser.canvas), true);

    assert.deepEqual(square(browser.canvas), [8, 84, 12, 12]);
    assert.equal(browser.canvas.context.imageSmoothingEnabled, false);
    assert.equal(browser.canvas.draws.length, 2);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers.length, 1);
    assert.equal(browser.visibilityListeners.size, 1);
});

test("movement is identical after three seconds at 30, 60, and 120 frames per second", (t) => {
    const browser = createBrowser(t);
    for (const fps of [30, 60, 120]) {
        const canvas = browser.createCanvas();
        initializeAndStart(canvas);
        browser.runFrame(0);
        for (let i = 1; i <= fps * 3; i++) browser.runFrame(i * 1000 / fps);

        assert.equal(square(canvas)[0], 152);
        browser.remove(canvas);
    }
});

test("the first frame establishes timing even when its timestamp is large", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(100000);
    assert.equal(square(browser.canvas)[0], 8);
    browser.runFrame(100100);
    assert.equal(square(browser.canvas)[0], 13);
});

test("turning around preserves the distance travelled past the boundary", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    for (let timestamp = 0; timestamp <= 7000; timestamp += 100) {
        browser.runFrame(timestamp);
    }

    assert.equal(square(browser.canvas)[0], 256);
    const positions = browser.canvas.draws
        .filter((draw) => draw.color === "#8ec99b")
        .map((draw) => draw.rectangle[0]);
    assert.ok(positions.every((x) => x >= 8 && x <= 300));
});

test("a delayed frame advances by no more than 100 milliseconds", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(10000);
    assert.equal(square(browser.canvas)[0], 13);
});

test("a hidden tab does not advance the demo or jump when it becomes visible", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(100);
    const drawCount = browser.canvas.draws.length;

    browser.setHidden(true);
    browser.runFrame(10000);
    assert.equal(browser.canvas.draws.length, drawCount);

    browser.setHidden(false);
    browser.runFrame(20000);
    assert.equal(square(browser.canvas)[0], 13);
    browser.runFrame(20100);
    assert.equal(square(browser.canvas)[0], 18);
});

test("DOM removal cancels the loop and releases the observer and listeners exactly once", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    const staleFrame = [...browser.frames.values()][0];
    const drawCount = browser.canvas.draws.length;

    browser.remove(browser.canvas);
    browser.observers[0].callback();
    staleFrame(1000);

    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.equal(browser.observers[0].disconnectCount, 1);
    assert.equal(browser.canvas.draws.length, drawCount);
});

test("the frame callback also cleans up if DOM removal has not been observed yet", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.canvas.isConnected = false;
    browser.runFrame(1000);

    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.equal(browser.observers[0].disconnectCount, 1);
});

test("separate canvases keep independent loops and cleanup", (t) => {
    const browser = createBrowser(t);
    const secondCanvas = browser.createCanvas();
    initializeAndStart(browser.canvas);
    initializeAndStart(secondCanvas);
    assert.equal(browser.frames.size, 2);

    browser.remove(browser.canvas);
    assert.equal(browser.frames.size, 1);
    assert.equal(browser.visibilityListeners.size, 1);
    browser.runFrame(0);
    browser.runFrame(100);
    assert.equal(square(secondCanvas)[0], 13);
});

test("a canvas removed while the module loads does not allocate browser resources", (t) => {
    const browser = createBrowser(t);
    const canvas = browser.createCanvas({ connected: false });
    assert.equal(initialize(canvas), false);

    assert.equal(canvas.draws.length, 0);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers.length, 0);
});

test("a missing drawing context fails before any animation resources are allocated", (t) => {
    const browser = createBrowser(t);
    const canvas = browser.createCanvas({ withContext: false });
    assert.throws(() => initialize(canvas), /2D drawing context/);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers.length, 0);
    assert.equal(browser.visibilityListeners.size, 0);
});

test("invalid elements and a missing app host fail with a clear error", (t) => {
    const browser = createBrowser(t);
    assert.throws(() => initialize({}), /canvas element/);
    browser.document.getElementById = () => null;
    assert.throws(() => initialize(browser.canvas), /inside the app/);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers.length, 0);
});

test("reduced motion draws a still frame and retains cleanup without scheduling animation", (t) => {
    const browser = createBrowser(t, { reducedMotion: true });
    const receiver = createReceiver();
    assert.equal(initialize(browser.canvas, receiver), false);
    assert.equal(start(browser.canvas), false);
    stop(browser.canvas);

    assert.deepEqual(square(browser.canvas), [8, 84, 12, 12]);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.deepEqual(receiver.calls, []);
    browser.remove(browser.canvas);
    assert.equal(browser.observers[0].disconnectCount, 1);
});

test("manual start is idempotent and reinitializing a running demo preserves its single loop", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    const scheduledIds = [...browser.frames.keys()];

    assert.equal(start(browser.canvas), true);
    assert.equal(initialize(browser.canvas), true);
    assert.deepEqual([...browser.frames.keys()], scheduledIds);
    assert.equal(browser.observers.length, 1);
    assert.equal(browser.visibilityListeners.size, 1);
});

test("stop freezes the bitmap and start resumes without counting time spent paused", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(100);
    const position = square(browser.canvas);
    const drawCount = browser.canvas.draws.length;

    stop(browser.canvas);
    stop(browser.canvas);
    browser.runFrame(60000);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.canvas.draws.length, drawCount);
    assert.deepEqual(square(browser.canvas), position);
    assert.equal(browser.observers[0].active, true);
    assert.equal(browser.visibilityListeners.size, 1);

    assert.equal(start(browser.canvas), true);
    browser.runFrame(120000);
    assert.deepEqual(square(browser.canvas), position);
    browser.runFrame(120100);
    assert.equal(square(browser.canvas)[0], 18);
});

test("callbacks from a stopped run cannot draw or overwrite the new run's frame handle", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    const staleFrame = [...browser.frames.values()][0];
    stop(browser.canvas);
    staleFrame(10000);
    assert.equal(browser.frames.size, 0);

    start(browser.canvas);
    const currentIds = [...browser.frames.keys()];
    const drawCount = browser.canvas.draws.length;
    staleFrame(20000);
    assert.deepEqual([...browser.frames.keys()], currentIds);
    assert.equal(browser.canvas.draws.length, drawCount);

    browser.remove(browser.canvas);
    assert.equal(browser.frames.size, 0);
});

test("rapid repeated start and stop calls do not allocate additional loops or observers", (t) => {
    const browser = createBrowser(t);
    initialize(browser.canvas);
    stop(browser.canvas);

    for (let i = 0; i < 50; i++) {
        start(browser.canvas);
        start(browser.canvas);
        assert.equal(browser.frames.size, 1);
        stop(browser.canvas);
        stop(browser.canvas);
        assert.equal(browser.frames.size, 0);
    }

    assert.equal(browser.observers.length, 1);
    assert.equal(browser.visibilityListeners.size, 1);
    assert.deepEqual(square(browser.canvas), [8, 84, 12, 12]);
});

test("visibility changes never restart a demo stopped by the user", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(100);
    stop(browser.canvas);
    const drawCount = browser.canvas.draws.length;

    browser.setHidden(true);
    browser.runFrame(10000);
    browser.setHidden(false);
    browser.runFrame(20000);

    assert.equal(browser.frames.size, 0);
    assert.equal(browser.canvas.draws.length, drawCount);
});

test("navigation cleans up a stopped demo and a new page starts with a fresh still image", (t) => {
    const browser = createBrowser(t);
    initializeAndStart(browser.canvas);
    browser.runFrame(0);
    browser.runFrame(100);
    stop(browser.canvas);
    browser.remove(browser.canvas);

    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers[0].disconnectCount, 1);
    assert.equal(browser.visibilityListeners.size, 0);

    const newCanvas = browser.createCanvas();
    assert.equal(initialize(newCanvas), true);
    assert.deepEqual(square(newCanvas), [8, 84, 12, 12]);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 1);
});

test("control commands reject unknown or removed canvases without starting animation", (t) => {
    const browser = createBrowser(t);
    assert.throws(() => start(browser.canvas), /not initialized/);
    assert.throws(() => stop(browser.canvas), /not initialized/);
    assert.throws(() => start(null), /not initialized/);

    initializeAndStart(browser.canvas);
    browser.canvas.isConnected = false;
    assert.throws(() => start(browser.canvas), /canvas was removed/);
    assert.throws(() => stop(browser.canvas), /canvas was removed/);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.visibilityListeners.size, 0);
    assert.equal(browser.observers[0].disconnectCount, 1);
});

test("a missing callback receiver or unusable canvas dimensions fail before allocating resources", (t) => {
    const browser = createBrowser(t);
    for (const receiver of [undefined, null, {}, { invokeMethodAsync: true }]) {
        assert.throws(() => initializeModule(browser.canvas, receiver), /callback reference/);
    }

    browser.canvas.width = 28;
    assert.throws(() => initialize(browser.canvas), /too small/);
    browser.canvas.width = 320;
    browser.canvas.height = 11;
    assert.throws(() => initialize(browser.canvas), /too small/);
    assert.equal(browser.frames.size, 0);
    assert.equal(browser.observers.length, 0);
    assert.equal(browser.visibilityListeners.size, 0);
});

test("initialization and repeated start or stop commands do not report turns", (t) => {
    const browser = createBrowser(t);
    const receiver = createReceiver();
    initialize(browser.canvas, receiver);
    stop(browser.canvas);
    start(browser.canvas);
    start(browser.canvas);
    stop(browser.canvas);
    stop(browser.canvas);

    assert.deepEqual(receiver.calls, []);
    assert.equal(browser.frames.size, 0);
});

test("only unrounded edge crossings report Right then Left with increasing totals", (t) => {
    const browser = createBrowser(t);
    const receiver = createReceiver();
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6000);
    browser.runFrame(6075);

    // Rounding has already drawn the rightmost pixel, but the actual edge is still ahead.
    assert.equal(square(browser.canvas)[0], 300);
    assert.deepEqual(receiver.calls, []);
    browser.runFrame(6100);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    runFrames(browser, 6200, 12100);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    browser.runFrame(12200);
    assert.deepEqual(receiver.calls, [turn("right", 1), turn("left", 2)]);
    runFrames(browser, 12300, 13000);
    assert.equal(receiver.calls.length, 2);
});

test("turn payloads are identical at 30, 60, and 120 frames per second", (t) => {
    const browser = createBrowser(t);
    for (const fps of [30, 60, 120]) {
        const canvas = browser.createCanvas();
        const receiver = createReceiver();
        initializeAndStart(canvas, receiver);
        browser.runFrame(0);
        for (let frame = 1; frame <= fps * 13; frame++) browser.runFrame(frame * 1000 / fps);

        assert.deepEqual(receiver.calls, [turn("right", 1), turn("left", 2)]);
        browser.remove(canvas);
    }
});

test("stop and resume preserve the turn count and exclude paused time", (t) => {
    const browser = createBrowser(t);
    const receiver = createReceiver();
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6100);
    stop(browser.canvas);
    browser.runFrame(60000);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);

    start(browser.canvas);
    browser.runFrame(120000);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    runFrames(browser, 120100, 126100);
    assert.deepEqual(receiver.calls, [turn("right", 1), turn("left", 2)]);
});

test("hidden time cannot generate turns or catch up when the tab becomes visible", (t) => {
    const browser = createBrowser(t);
    const receiver = createReceiver();
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6000);
    browser.setHidden(true);
    browser.runFrame(60000);
    browser.setHidden(false);
    browser.runFrame(120000);
    assert.deepEqual(receiver.calls, []);

    browser.runFrame(120100);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);
});

test("stale or detached frame callbacks cannot report a pending edge crossing", (t) => {
    const browser = createBrowser(t);
    const receiver = createReceiver();
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6000);
    const staleFrame = [...browser.frames.values()][0];
    stop(browser.canvas);
    start(browser.canvas);
    staleFrame(6100);
    assert.deepEqual(receiver.calls, []);
    assert.equal(browser.frames.size, 1);

    // The RAF guard must also work before MutationObserver delivers the DOM change.
    browser.canvas.isConnected = false;
    browser.runFrame(6200);
    staleFrame(6300);
    assert.deepEqual(receiver.calls, []);
    assert.equal(browser.frames.size, 0);
});

test("receivers belong to one canvas and replacement pages start counting from zero", (t) => {
    const browser = createBrowser(t);
    const firstReceiver = createReceiver();
    const ignoredReceiver = createReceiver();
    initializeAndStart(browser.canvas, firstReceiver);
    initialize(browser.canvas, ignoredReceiver);
    runFrames(browser, 0, 6100);

    const newCanvas = browser.createCanvas();
    const newReceiver = createReceiver();
    initializeAndStart(newCanvas, newReceiver);
    runFrames(browser, 6200, 12300);
    assert.deepEqual(firstReceiver.calls, [turn("right", 1), turn("left", 2)]);
    assert.deepEqual(newReceiver.calls, [turn("right", 1)]);
    assert.deepEqual(ignoredReceiver.calls, []);

    browser.remove(browser.canvas);
    runFrames(browser, 12400, 18400);
    assert.equal(firstReceiver.calls.length, 2);
    assert.deepEqual(newReceiver.calls, [turn("right", 1), turn("left", 2)]);
});

test("pending callbacks do not block animation or controls and may complete after Stop", async (t) => {
    const browser = createBrowser(t);
    const completions = [];
    const receiver = createReceiver(() => new Promise((resolve) => completions.push(resolve)));
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 13000);
    assert.deepEqual(receiver.calls, [turn("right", 1), turn("left", 2)]);
    assert.equal(browser.frames.size, 1);
    assert.equal(square(browser.canvas)[0], 48);

    stop(browser.canvas);
    assert.equal(browser.frames.size, 0);
    // A later event can finish before an earlier one; C# uses totals to reject older events.
    completions[1]();
    completions[0]();
    await settleCallbacks();
    assert.equal(start(browser.canvas), true);
    browser.runFrame(60000);
    assert.equal(square(browser.canvas)[0], 48);
    assert.equal(receiver.calls.length, 2);
});

test("callback rejection is handled once without stopping animation or controls", async (t) => {
    const browser = createBrowser(t);
    const logs = [];
    t.mock.method(console, "error", (...args) => logs.push(args));
    const failure = new Error("Callback target unavailable");
    const receiver = createReceiver(() => Promise.reject(failure));
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6100);
    await settleCallbacks();

    assert.equal(logs.length, 1);
    assert.equal(logs[0][1], failure);
    runFrames(browser, 6200, 20000);
    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    assert.equal(browser.frames.size, 1);
    stop(browser.canvas);
    assert.equal(start(browser.canvas), true);
    browser.runFrame(60000);
    assert.equal(browser.frames.size, 1);
    assert.equal(logs.length, 1);
});

test("a synchronous callback failure is also contained without breaking the loop", async (t) => {
    const browser = createBrowser(t);
    const logs = [];
    t.mock.method(console, "error", (...args) => logs.push(args));
    const receiver = createReceiver(() => { throw new Error("Invalid callback proxy"); });
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 13000);
    await settleCallbacks();

    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    assert.equal(logs.length, 1);
    assert.equal(browser.frames.size, 1);
});

test("multiple pending callback failures produce one log and disable future notifications", async (t) => {
    const browser = createBrowser(t);
    const logs = [];
    const rejections = [];
    t.mock.method(console, "error", (...args) => logs.push(args));
    const receiver = createReceiver(() => new Promise((resolve, reject) => rejections.push(reject)));
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 13000);
    assert.equal(rejections.length, 2);
    rejections[1](new Error("Second callback failed first"));
    rejections[0](new Error("First callback failed later"));
    await settleCallbacks();
    runFrames(browser, 13100, 20000);

    assert.equal(logs.length, 1);
    assert.equal(receiver.calls.length, 2);
    assert.equal(browser.frames.size, 1);
});

test("late rejection after navigation is ignored and does not affect a new receiver", async (t) => {
    const browser = createBrowser(t);
    const logs = [];
    t.mock.method(console, "error", (...args) => logs.push(args));
    let rejectCallback;
    const receiver = createReceiver(() => new Promise((resolve, reject) => { rejectCallback = reject; }));
    initializeAndStart(browser.canvas, receiver);
    runFrames(browser, 0, 6100);
    const staleFrame = [...browser.frames.values()][0];
    browser.remove(browser.canvas);

    const newCanvas = browser.createCanvas();
    const newReceiver = createReceiver();
    initializeAndStart(newCanvas, newReceiver);
    rejectCallback(new Error("Old callback target disposed"));
    await settleCallbacks();
    staleFrame(6200);
    runFrames(browser, 10000, 16100);

    assert.deepEqual(receiver.calls, [turn("right", 1)]);
    assert.deepEqual(newReceiver.calls, [turn("right", 1)]);
    assert.deepEqual(logs, []);
    assert.equal(browser.frames.size, 1);
});
