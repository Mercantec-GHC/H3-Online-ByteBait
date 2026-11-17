// Tests the real game module with controlled browser primitives, without a browser or packages.
import assert from "node:assert/strict";
import test from "node:test";
import { initialize as initializeModule, start, stop } from "../ByteBait.Client/wwwroot/js/game.js";

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

    class Canvas {
        constructor({ connected = true, withContext = true } = {}) {
            this.width = 320;
            this.height = 180;
            this.isConnected = connected;
            this.draws = [];
            const draws = this.draws;
            this.context = withContext ? {
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
        runFrame(timestamp) {
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
