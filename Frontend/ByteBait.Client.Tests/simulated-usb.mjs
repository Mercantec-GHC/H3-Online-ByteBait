// Simulated Web Serial streams shared by the USB protocol and casting meter integration tests.
import assert from "node:assert/strict";
import { createUsbController } from "../ByteBait.Client/wwwroot/js/usb-controller.js";

/** Uses real Web Streams so a forgotten lock or an uncancelled read fails the test. */
export function simulateUsb(onWrite) {
    const calls = [], writes = [], opens = [];
    let input;
    let closes = 0;
    let selections = 0;
    const port = {
        async open(options) {
            opens.push(options);
            this.readable = new ReadableStream({ start(controller) { input = controller; } });
            this.writable = new WritableStream({
                write(chunk, stream) {
                    const line = new TextDecoder().decode(chunk);
                    writes.push(line);
                    return onWrite?.(line, stream);
                },
            });
        },
        async close() {
            assert.equal(this.readable.locked, false);
            assert.equal(this.writable.locked, false);
            closes++;
        },
    };
    const serial = new EventTarget();
    serial.requestPort = async () => { selections++; return port; };
    const controller = createUsbController({
        async invokeMethodAsync(...args) { calls.push(args); },
    }, serial);
    return {
        controller, serial, port, calls, writes, opens,
        get closes() { return closes; }, get selections() { return selections; },
        feed(text) { input.enqueue(new TextEncoder().encode(text)); },
        failRead() { input.error(new Error("Device removed")); },
        unplug() {
            const event = new Event("disconnect");
            Object.defineProperty(event, "target", { value: port });
            serial.dispatchEvent(event);
        },
    };
}
