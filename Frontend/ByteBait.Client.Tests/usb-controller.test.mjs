// Exercises the controller protocol with the same byte chunks Web Serial delivers.
import assert from "node:assert/strict";
import test from "node:test";
import { createUsbLineReader } from "../ByteBait.Client/wwwroot/js/usb-controller.js";
import { simulateUsb } from "./simulated-usb.mjs";

const bytes = text => new TextEncoder().encode(text);

test("a split THROW line produces exactly one cast when its newline arrives", () => {
    const read = createUsbLineReader();
    assert.deepEqual(read(bytes("THR")), []);
    assert.deepEqual(read(bytes("OW:72\r\n")), [{ type: "throw", strength: 72 }]);
});

test("batched HELLO and PONG are distinct from casts at the strength limits", () => {
    const read = createUsbLineReader();
    assert.deepEqual(read(bytes("HELLO:USB:1\nPONG:44\nTHROW:0\r\nTHROW:100\n")), [
        { type: "hello" }, { type: "pong", counter: "44" },
        { type: "throw", strength: 0 }, { type: "throw", strength: 100 },
    ]);
});

test("invalid messages produce protocol errors without generating a throw or echoing ERR", () => {
    const read = createUsbLineReader();
    for (const [line, code] of [
        ["THROW:-1", "RANGE"], ["THROW:101", "RANGE"], ["THROW:72x", "ARGS"],
        ["THROW:2:3", "ARGS"], ["THROW:1.2", "ARGS"], ["PONG:", "ARGS"],
        ["HELLO:WIFI:1", "ARGS"], ["UNRECOGNIZED", "UNKNOWN"],
    ]) {
        assert.deepEqual(read(bytes(line + "\n")), [{ type: "error", code }]);
    }
    assert.deepEqual(read(bytes("ERR:UNKNOWN\nHOLD:1\nHOOK\n\n")), []);
});

test("the 32-byte limit discards the entire oversized line and recovers at the newline", () => {
    const read = createUsbLineReader();
    const counter = "9".repeat(27);
    assert.deepEqual(read(bytes("PONG:" + counter + "\r\n")), [{ type: "pong", counter }]);
    assert.deepEqual(read(bytes("A".repeat(500))), []);
    assert.deepEqual(read(bytes("THROW:72\nTHROW:5\n")), [
        { type: "error", code: "TOOLONG" }, { type: "throw", strength: 5 },
    ]);
});

const settle = () => new Promise(resolve => setImmediate(resolve));

function waitingForLatency(strength, receivedAtMs) {
    return ["OnUsbThrow", { strength, receivedAtMs, zone: null, castAt: null, reason: "missingLatency" }];
}

function useUsbClock(t, now = 0) {
    t.mock.timers.enable({ apis: ["Date", "setTimeout"], now });
    t.mock.method(performance, "now", () => Date.now());
    return async ms => {
        t.mock.timers.tick(ms);
        await settle();
    };
}

function pongFor(ping) {
    assert.match(ping, /^PING:\d+\n$/);
    return ping.replace("PING:", "PONG:");
}

test("probes at 0, 5 and 10 seconds; 15 seconds of silence releases the port exactly once", async t => {
    const advance = useUsbClock(t);
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.writes, ["PING:1\n"]);
    await advance(4999);
    assert.equal(usb.writes.length, 1);
    await advance(1);
    assert.equal(usb.writes.at(-1), "PING:2\n");
    await advance(5000);
    assert.equal(usb.writes.at(-1), "PING:3\n");
    await advance(4999);
    usb.feed("HELLO:USB:1\nTHROW:72\nPONG:99999\n");
    await settle();
    assert.equal(usb.closes, 0);
    await advance(1);
    assert.equal(usb.closes, 1);
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "timedOut"]);
    await advance(30000);
    assert.equal(usb.writes.length, 3);
    assert.equal(usb.closes, 1);
});

test("a matched 20 ms echo gives 10 ms one-way; duplicates and other traffic cannot renew it", async t => {
    const advance = useUsbClock(t, 1000);
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    const pong = pongFor(usb.writes[0]);
    await advance(20);
    usb.feed(pong);
    await settle();
    assert.deepEqual(usb.calls, [["OnUsbStatus", "connected"], ["OnUsbLatency", 20, 10]]);
    await advance(4980);
    usb.feed(pong + "PONG:00000\nTHROW:0\n");
    await settle();
    assert.equal(usb.calls.filter(call => call[0] === "OnUsbLatency").length, 1);
    await advance(10019);
    assert.equal(usb.closes, 0);
    await advance(1);
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "timedOut"]);
    assert.equal(usb.closes, 1);
});

test("out-of-order replies and a previous connection's tokens cannot overwrite a fresh sample", async t => {
    const advance = useUsbClock(t);
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    const older = pongFor(usb.writes[0]);
    await advance(5000);
    const newer = pongFor(usb.writes[1]);
    await advance(7);
    usb.feed(newer + older + newer);
    await settle();
    assert.deepEqual(usb.calls.slice(1), [["OnUsbLatency", 7, 3.5]]);
    await advance(5000);
    const outstanding = pongFor(usb.writes.at(-1));
    await usb.controller.disconnect();
    usb.controller.connect();
    await settle();
    const fresh = pongFor(usb.writes.at(-1));
    assert.notEqual(outstanding, fresh);
    usb.feed(outstanding);
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "connected"]);
    await advance(3);
    usb.feed(fresh);
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbLatency", 3, 1.5]);
});

test("queued PING starts its clock at the actual write after an ERR; receipt stays responsive", async t => {
    const advance = useUsbClock(t);
    const gate = Promise.withResolvers();
    const usb = simulateUsb(line => line.startsWith("ERR:") ? gate.promise : undefined);
    t.after(() => { gate.resolve(); return usb.controller.dispose(); });
    usb.controller.connect();
    await settle();
    await advance(4990);
    usb.feed("BAD\n");
    await settle();
    await advance(10);
    assert.equal(usb.writes.length, 2);
    assert.equal(usb.writes.at(-1), "ERR:UNKNOWN\n");
    usb.feed("THROW:72\n");
    await settle();
    assert.deepEqual(usb.calls.at(-1), waitingForLatency(72, 5000));
    await advance(200);
    gate.resolve();
    await settle();
    const pong = pongFor(usb.writes.at(-1));
    await advance(20);
    usb.feed(pong);
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbLatency", 20, 10]);
});

test("write failure releases the connection and navigation cancels all subsequent probes", async t => {
    const advance = useUsbClock(t);
    let fail = true;
    const usb = simulateUsb(() => { if (fail) throw new Error("Device unavailable"); });
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "writeFailed"]);
    assert.equal(usb.closes, 1);
    fail = false;
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "connected"]);
    await usb.controller.dispose();
    const callCount = usb.calls.length;
    await advance(20000);
    assert.equal(usb.writes.length, 2);
    assert.equal(usb.closes, 2);
    assert.equal(usb.calls.length, callCount);
});

test("a late PONG cannot revive a session when the browser delays its timeout callback", async t => {
    useUsbClock(t);
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    // Advance the monotonic clock without executing the browser timer.
    t.mock.timers.setTime(15001);
    usb.feed(pongFor(usb.writes[0]));
    await settle();
    assert.deepEqual(usb.calls, [["OnUsbStatus", "connected"], ["OnUsbStatus", "timedOut"]]);
    assert.equal(usb.closes, 1);
});

test("timeout cancels a stalled write and discards queued probes before reconnect", async t => {
    const advance = useUsbClock(t);
    const usb = simulateUsb((line, stream) => {
        if (line.startsWith("ERR:")) {
            return new Promise(resolve => stream.signal.addEventListener("abort", resolve, { once: true }));
        }
    });
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    usb.feed("BAD\n");
    await settle();
    await advance(5000);
    await advance(10000);
    assert.equal(usb.closes, 1);
    assert.equal(usb.writes.length, 2);
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "timedOut"]);
    usb.controller.connect();
    await settle();
    assert.equal(usb.writes.length, 3);
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "connected"]);
});

test("double click opens one 8N1 port, and a cast can arrive before HELLO", async t => {
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    usb.controller.connect();
    await settle();
    assert.equal(usb.selections, 1);
    assert.deepEqual(usb.opens, [{ baudRate: 115200, dataBits: 8, parity: "none", stopBits: 1 }]);
    usb.feed("THR");
    await settle();
    assert.deepEqual(usb.calls, [["OnUsbStatus", "connected"]]);
    const earliest = performance.now();
    usb.feed("OW:72\r\nHELLO:USB:1\nPONG:44\nTHROW:0\n");
    await settle();
    const latest = performance.now();
    const timestamp = usb.calls[1][1].receivedAtMs;
    assert.ok(timestamp >= earliest && timestamp <= latest);
    assert.deepEqual(usb.calls.slice(1), [waitingForLatency(72, timestamp), waitingForLatency(0, timestamp)]);
});

test("ERR responses are limited to one per second and do not cause an echo", async t => {
    let now = 40;
    t.mock.method(performance, "now", () => now);
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    usb.feed("THROW:101\nTHROW:9x\nERR:UNKNOWN\n");
    await settle();
    now += 999;
    usb.feed("BAD\n");
    await settle();
    assert.deepEqual(usb.writes.filter(line => line.startsWith("ERR:")), ["ERR:RANGE\n"]);
    now++;
    usb.feed("ERR:RANGE\n" + "X".repeat(33) + "\nTHROW:100\n");
    await settle();
    assert.deepEqual(usb.writes.filter(line => line.startsWith("ERR:")), ["ERR:RANGE\n", "ERR:TOOLONG\n"]);
    assert.deepEqual(usb.calls.at(-1), waitingForLatency(100, now));
});

test("unplug closes once, and reconnect starts with an empty line buffer", async t => {
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    usb.controller.connect();
    await settle();
    usb.feed("THR");
    await settle();
    usb.unplug();
    await settle();
    assert.equal(usb.closes, 1);
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "disconnected"]);
    await usb.controller.disconnect();
    assert.equal(usb.closes, 1);
    usb.controller.connect();
    await settle();
    usb.feed("OW:72\nTHROW:5\n");
    await settle();
    assert.deepEqual(usb.calls.filter(args => args[0] === "OnUsbThrow").map(args => args[1].strength), [5]);
    await usb.controller.dispose();
    assert.equal(usb.closes, 2);
    assert.equal(usb.calls.at(-1)[0], "OnUsbThrow");
});

test("a cancelled chooser, busy port and read failure each leave the controller reusable", async t => {
    const usb = simulateUsb();
    t.after(() => usb.controller.dispose());
    const select = usb.serial.requestPort;
    usb.serial.requestPort = async () => { throw new DOMException("Cancelled", "NotFoundError"); };
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "cancelled"]);
    usb.serial.requestPort = async () => ({ async open() { throw new Error("Busy"); } });
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "openFailed"]);
    usb.serial.requestPort = select;
    usb.controller.connect();
    await settle();
    usb.failRead();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "readFailed"]);
    assert.equal(usb.closes, 1);
    usb.controller.connect();
    await settle();
    assert.deepEqual(usb.calls.at(-1), ["OnUsbStatus", "connected"]);
});

test("navigation during the native chooser discards its late selection", async () => {
    const usb = simulateUsb();
    const choice = Promise.withResolvers();
    usb.serial.requestPort = () => choice.promise;
    usb.controller.connect();
    await usb.controller.dispose();
    choice.resolve(usb.port);
    await settle();
    assert.equal(usb.opens.length, 0);
    assert.deepEqual(usb.calls, []);
});

test("navigation during port.open closes a late-opened port without notifying the old page", async () => {
    const usb = simulateUsb();
    const ready = Promise.withResolvers();
    const open = usb.port.open.bind(usb.port);
    usb.port.open = async options => { await ready.promise; await open(options); };
    usb.controller.connect();
    await settle();
    const released = usb.controller.dispose();
    ready.resolve();
    await released;
    assert.equal(usb.closes, 1);
    assert.deepEqual(usb.calls, []);
});
