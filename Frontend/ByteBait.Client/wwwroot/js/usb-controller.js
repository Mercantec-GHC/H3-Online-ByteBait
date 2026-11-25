/**
 * Exchanges Oplà v1 lines with the game page and sends the live meter zone to the LEDs.
 * A newline completes a message; read boundaries have no meaning in the protocol.
 */
const pingIntervalMs = 5000;
const replyTimeoutMs = 15000;
// Give a healthy port time to turn its lights off without holding navigation up on a stalled write.
const ledShutdownTimeoutMs = 250;
// The Oplà USB sketch collects peak strength for WINDOW_MS after the trigger before sending THROW.
const throwWindowMs = 250;
const encoder = new TextEncoder();
// Do not reuse an old port's echo token when reconnecting or revisiting the game.
let nextPingNumber = 1;

function parseLine(line) {
    const [command, value, thirdField] = line.split(":");
    if (command === "ERR" || !line) return null;
    if (line === "HELLO:USB:1") return { type: "hello" };
    if (command === "HELLO") return { type: "error", code: "ARGS" };
    if (command === "HOOK") {
        return value === undefined ? { type: "hook" } : { type: "error", code: "ARGS" };
    }

    if (command === "PONG" || command === "THROW") {
        if (thirdField !== undefined || !/^-?\d+$/.test(value)) {
            return { type: "error", code: "ARGS" };
        }
        if (value.startsWith("-")) return { type: "error", code: "RANGE" };
        // Keep the echoed counter exact, even for a long sequence of decimal digits.
        if (command === "PONG") return { type: "pong", counter: value };
        const strength = Number(value);
        return strength <= 100 ? { type: "throw", strength } : { type: "error", code: "RANGE" };
    }

    // These valid v1 messages belong to later fishing actions.
    if (/^(YANK|TILT:[LR]|HOLD:[01])$/.test(line)) return null;
    return { type: "error", code: "UNKNOWN" };
}

/** Creates one bounded line buffer per connection. A discarded suffix cannot become a new cast. */
export function createUsbLineReader() {
    let line = "";
    let tooLong = false;
    return function read(bytes) {
        const events = [];
        for (const byte of bytes) {
            if (byte === 13) continue;
            if (byte === 10) {
                const event = tooLong ? { type: "error", code: "TOOLONG" } : parseLine(line);
                if (event) events.push(event);
                line = "";
                tooLong = false;
            } else if (line.length < 32) {
                line += String.fromCharCode(byte);
            } else {
                tooLong = true;
            }
        }
        return events;
    };
}

/** Look up the frame visible at the estimated trigger before crossing the Blazor interop boundary. */
function assessThrow(session, strength, receivedAtMs) {
    const result = { strength, receivedAtMs, zone: null, castAt: null, reason: null };
    if (session.oneWayMs === null) return { ...result, reason: "missingLatency" };

    const castTimeMs = receivedAtMs - throwWindowMs - session.oneWayMs;
    const frame = session.game?.readMeterAt(castTimeMs);
    if (!frame) return { ...result, reason: "missingHistory" };

    // A single painted frame may cover several trigger times. Keep the trigger time for the cast.
    return { ...result, zone: frame.zone, castAt: new Date(performance.timeOrigin + castTimeMs).toISOString() };
}

/**
 * Owns one game page's port selection, read loop and .NET receiver. Each successful
 * open gets a fresh line buffer. Tests can supply a simulated Web Serial adapter.
 */
export function createUsbController(receiver, serial = navigator.serial) {
    let active = null;
    let disposed = false;

    async function notify(method, ...args) {
        if (!disposed) await receiver.invokeMethodAsync(method, ...args);
    }

    /** Stop accepting replies before cancelling I/O. The first reason is shown after cleanup. */
    function stopSession(session, reason, turnOff = false) {
        if (session.endState) return;
        session.endState = reason;
        session.unsubscribeZone?.();
        globalThis.document?.removeEventListener("visibilitychange", checkVisibleConnection);
        session.game?.disconnectCast();
        clearTimeout(session.timer);
        session.pendingPings.clear();
        const stopWriter = turnOff && session.desiredZone !== null
            ? finishLedOutput(session) : session.writer?.abort();
        session.ioStopped = Promise.allSettled([session.reader?.cancel(), stopWriter]);
    }

    /** On an intentional close, let the current write finish, then send OFF on that same writer. */
    async function finishLedOutput(session) {
        const writer = session.writer;
        let abort;
        const timer = setTimeout(() => { abort = writer.abort().catch(() => {}); }, ledShutdownTimeoutMs);
        try {
            await session.writes;
            if (!abort && session.lastLedZone !== "off") await writer.write(encoder.encode("LED:OFF\n"));
        } finally {
            clearTimeout(timer);
            await abort;
        }
    }

    // A suspended tab may run its read or write callbacks before its overdue timer.
    function isLiveAt(session, now) {
        if (now >= session.deadline) stopSession(session, "timedOut");
        return !session.endState;
    }

    // On returning from a suspended tab the heartbeat timer may still be queued.
    function checkVisibleConnection() {
        if (!document.hidden && active) isLiveAt(active, performance.now());
    }

    /** Serialize PING, ERR and LED on the port's single writer. Ignore queued work after teardown. */
    function enqueueWrite(session, write) {
        session.writes = session.writes.then(() => {
            const now = performance.now();
            if (isLiveAt(session, now)) return write(now);
        }).catch(() => stopSession(session, "writeFailed"));
        return session.writes;
    }

    function writeLine(session, line, beforeWrite) {
        return enqueueWrite(session, now => {
            beforeWrite?.(now);
            return session.writer.write(encoder.encode(line + "\n"));
        });
    }

    /** Sample the newest requested zone when the writer becomes available, skipping obsolete colors. */
    function queueLed(session, zone) {
        session.desiredZone = zone;
        if (session.ledQueued) return;
        session.ledQueued = true;
        void enqueueWrite(session, () => {
            session.ledQueued = false;
            const latest = session.desiredZone;
            if (latest === session.lastLedZone) return;
            session.lastLedZone = latest;
            return session.writer.write(encoder.encode("LED:" + latest.toUpperCase() + "\n"));
        });
    }

    /** One timer wakes for either the next probe or the exact outstanding reply deadline. */
    function startProbes(session) {
        let nextPingAt = performance.now();
        session.deadline = nextPingAt + replyTimeoutMs;
        let pingQueued = false;

        function tick() {
            const now = performance.now();
            if (!isLiveAt(session, now)) return;
            if (now >= nextPingAt) {
                nextPingAt = now + pingIntervalMs;
                if (!pingQueued) {
                    pingQueued = true;
                    const counter = String(nextPingNumber++);
                    void writeLine(session, "PING:" + counter, sentAt => {
                        session.pendingPings.set(counter, sentAt);
                    }).then(() => { pingQueued = false; });
                }
            }
            session.timer = setTimeout(tick, Math.ceil(Math.min(nextPingAt, session.deadline) - now));
        }
        tick();
    }

    /** A newer accepted echo supersedes all earlier probes in send order. */
    function acceptPong(session, counter, receivedAtMs) {
        const sentAt = session.pendingPings.get(counter);
        if (sentAt === undefined || receivedAtMs < sentAt) return;
        for (const pending of session.pendingPings.keys()) {
            session.pendingPings.delete(pending);
            if (pending === counter) break;
        }
        session.deadline = receivedAtMs + replyTimeoutMs;
        const roundTripMs = receivedAtMs - sentAt;
        session.oneWayMs = roundTripMs / 2;
        return notify("OnUsbLatency", roundTripMs, session.oneWayMs);
    }

    async function receive(session) {
        const read = createUsbLineReader();
        let nextErrorAt = -Infinity;
        let errorQueued = false;
        while (!session.endState) {
            const { value, done } = await session.reader.read();
            if (done || session.endState) return;
            // Stamp the whole delivery before writes or UI callbacks can delay processing.
            const receivedAtMs = performance.now();
            if (!isLiveAt(session, receivedAtMs)) return;
            const notifications = [];
            for (const event of read(value)) {
                if (event.type === "throw") {
                    if (session.game?.hasCast()) {
                        notifications.push(notify("OnAdditionalThrow"));
                        continue;
                    }
                    const result = assessThrow(session, event.strength, receivedAtMs);
                    if (result.zone) {
                        result.attemptId = session.game.beginCast(receivedAtMs);
                    }
                    notifications.push(notify("OnUsbThrow", result));
                } else if (event.type === "hook") {
                    // HOOK carries no device timestamp. Pass the browser's receipt time directly.
                    notifications.push(notify("OnUsbHook", receivedAtMs));
                } else if (event.type === "pong") {
                    notifications.push(acceptPong(session, event.counter, receivedAtMs));
                } else if (event.type === "error" && !errorQueued && receivedAtMs >= nextErrorAt) {
                    errorQueued = true;
                    void writeLine(session, "ERR:" + event.code, sentAt => {
                        nextErrorAt = sentAt + 1000;
                    }).then(() => { errorQueued = false; });
                }
            }
            await Promise.all(notifications);
        }
    }

    async function run(session) {
        let endState = "disconnected";
        try {
            // Call requestPort in the original button gesture, before any other await.
            try {
                session.port = await serial.requestPort();
            } catch (error) {
                endState = error.name === "NotFoundError" ? "cancelled" : "selectionFailed";
                return;
            }
            if (session.endState) return;
            await session.port.open({ baudRate: 115200, dataBits: 8, parity: "none", stopBits: 1 });
            session.opened = true;
            if (session.endState) return;
            session.reader = session.port.readable.getReader();
            session.writer = session.port.writable.getWriter();
            startProbes(session);
            globalThis.document?.addEventListener("visibilitychange", checkVisibleConnection);
            session.unsubscribeZone = session.game?.subscribeZone(zone => queueLed(session, zone));
            await Promise.all([notify("OnUsbStatus", "connected"), receive(session)]);
        } catch {
            endState = session.opened ? "readFailed" : "openFailed";
        } finally {
            stopSession(session, endState);
            await session.ioStopped;
            session.reader?.releaseLock();
            await session.writes;
            session.writer?.releaseLock();
            if (session.opened) {
                try {
                    await session.port.close();
                } catch {
                    // A detached device may already have closed its native port.
                }
            }
            active = null;
            await notify("OnUsbStatus", session.endState);
        }
    }

    function connect(game) {
        if (disposed || active || !serial) return;
        const session = {
            game, oneWayMs: null,
            desiredZone: null, lastLedZone: null, ledQueued: false, unsubscribeZone: null,
            port: null, opened: false, reader: null, writer: null, endState: null,
            writes: Promise.resolve(), pendingPings: new Map(), timer: null,
        };
        active = session;
        session.done = run(session).catch(error => {
            if (!disposed) console.error("Unable to report the USB connection status.", error);
        });
    }

    async function disconnect() {
        const session = active;
        if (!session) return;
        stopSession(session, "disconnected", true);
        // The native chooser can outlive this page. A late selection is discarded by run().
        if (session.port) await session.done;
    }

    function onDisconnect(event) {
        if (active?.port === event.target) stopSession(active, "disconnected");
    }

    serial?.addEventListener("disconnect", onDisconnect);
    return {
        isSupported: () => Boolean(serial),
        connect,
        disconnect,
        async dispose() {
            disposed = true;
            serial?.removeEventListener("disconnect", onDisconnect);
            await disconnect();
        },
    };
}
