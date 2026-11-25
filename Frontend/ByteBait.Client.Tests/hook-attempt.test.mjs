// Exercises one cast at a time using elapsed milliseconds supplied by the test.
import assert from "node:assert/strict";
import test from "node:test";
import { createHookAttempt } from "../ByteBait.Client/wwwroot/js/hook-attempt.js";

test("two nibbles precede the bite, with each phase starting exactly at its boundary", () => {
    const attempt = createHookAttempt();
    for (const [elapsedMs, expected] of [
        [0, "waiting"],
        [1499, "waiting"], [1500, "nibble"],
        [2299, "nibble"], [2300, "waiting"],
        [3199, "waiting"], [3200, "nibble"],
        [3999, "nibble"], [4000, "waiting"],
        [4899, "waiting"], [4900, "bite"],
        [6399, "bite"], [6400, "escaped"],
    ]) {
        assert.equal(attempt.update(elapsedMs), expected, `phase at ${elapsedMs} ms`);
    }
});

test("pulls while the float is still leave the fish available for the bite", () => {
    const attempt = createHookAttempt();
    for (const elapsedMs of [500, 2300, 4000, 4899]) {
        assert.equal(attempt.hook(elapsedMs), "waiting");
    }
    assert.equal(attempt.hook(4900), "hooked");
});

test("a pull during either nibble scares the fish", () => {
    for (const elapsedMs of [1500, 2299, 3200, 3999]) {
        assert.equal(createHookAttempt().hook(elapsedMs), "scared");
    }
});

// USB receipt time measured during the first visible dip on the physical Opla.
test("the physical pull received at 1949 ms scares the fish", () => {
    assert.equal(createHookAttempt().hook(1949), "scared");
});

test("a pull uses its own timestamp even if the last update preceded the bite", () => {
    for (const [elapsedMs, expected] of [
        [4899, "waiting"], [4900, "hooked"], [6399, "hooked"], [6400, "escaped"],
    ]) {
        const attempt = createHookAttempt();
        attempt.update(4000);
        assert.equal(attempt.hook(elapsedMs), expected, `pull at ${elapsedMs} ms`);
    }
});

test("a fish escapes even if no updates or pulls occur during the bite", () => {
    const attempt = createHookAttempt();
    assert.equal(attempt.update(0), "waiting");
    assert.equal(attempt.update(7000), "escaped");
});

test("a resolved cast keeps its outcome and the next cast starts independently", () => {
    for (const [elapsedMs, outcome] of [[1500, "scared"], [4900, "hooked"], [6400, "escaped"]]) {
        const attempt = createHookAttempt();
        assert.equal(attempt.hook(elapsedMs), outcome);
        const next = createHookAttempt();
        assert.equal(next.update(0), "waiting");
        assert.equal(attempt.hook(elapsedMs), outcome);
        assert.equal(attempt.update(7000), outcome);
        assert.equal(attempt.hook(7001), outcome);
        assert.equal(next.hook(4900), "hooked");
    }
});

test("invalid elapsed time cannot resolve a cast", () => {
    const attempt = createHookAttempt();
    for (const elapsedMs of [-1, NaN, Infinity, "4900"]) {
        assert.throws(() => attempt.update(elapsedMs), RangeError);
        assert.throws(() => attempt.hook(elapsedMs), RangeError);
    }
    assert.equal(attempt.hook(4900), "hooked");
});
