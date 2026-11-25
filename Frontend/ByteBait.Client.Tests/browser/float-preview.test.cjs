// Exercises the local simulation in Chrome. Each test chooses exactly when the next frame is drawn.
const assert = require("node:assert/strict");
const { after, before, test } = require("node:test");
const { chromium } = require("playwright-core");

const previewUrl = "http://127.0.0.1:5174/ByteBait.Client.Tests/browser/float-preview.html";
let browser;
before(async () => { browser = await chromium.launch({ channel: "chrome" }); });
after(async () => { await browser?.close(); });

async function openPreview(t) {
    const page = await browser.newPage();
    t.after(() => page.close());
    // A 60 Hz timer rarely lands exactly on a bite boundary. Deliver a frame and
    // any subsequent keyboard input at the same instant, using the real page handlers.
    await page.addInitScript(() => {
        const pending = new Map();
        let now = 0;
        let frameId = 0;
        Object.defineProperty(performance, "now", { value: () => now });
        window.requestAnimationFrame = callback => {
            pending.set(++frameId, callback);
            return frameId;
        };
        window.cancelAnimationFrame = id => pending.delete(id);
        window.advanceFrame = (deltaMs, draw = true) => {
            now += deltaMs;
            if (!draw) return pending.size;
            const callbacks = [...pending.values()];
            pending.clear();
            for (const callback of callbacks) callback(now);
            return pending.size;
        };
    });
    await page.goto(previewUrl);
    await page.getByText("Ready to cast.", { exact: true }).waitFor();
    const press = name => page.getByRole("button", { name }).press("Enter");
    return {
        page,
        cast: () => press("Simulate cast"),
        hook: () => press("Simulate hook"),
        advance: (delta, draw = true) => page.evaluate(([ms, drawFrame]) => window.advanceFrame(ms, drawFrame), [delta, draw]),
        status: () => page.getByRole("status").textContent(),
    };
}

// Classify the pixels at water level: the red tip moves down, the cream body disappears,
// and the two ripple colours appear only during a bite.
function readSignal(page) {
    return page.locator("canvas").evaluate(canvas => {
        const { data, width } = canvas.getContext("2d").getImageData(130, 80, 61, 40);
        let tipTop = null;
        let body = false, innerRing = false, outerRing = false;
        for (let i = 0; i < data.length; i += 4) {
            const rgb = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
            if (rgb === 0xff6956 && tipTop === null) tipTop = Math.floor(i / 4 / width);
            if (rgb === 0xfff1cb) body = true;
            if (rgb === 0xd6ecce) innerRing = true;
            if (rgb === 0x99ccc0) outerRing = true;
        }
        return { tipTop, body, innerRing, outerRing };
    });
}

const openWater = { tipTop: null, body: false, innerRing: false, outerRing: false };
const bite = { tipTop: null, body: false, innerRing: true, outerRing: true };

test("both nibbles leave a lower red tip, and rings replace it for the entire bite", async t => {
    const preview = await openPreview(t);
    assert.deepEqual(await readSignal(preview.page), openWater);
    await preview.cast();
    const waiting = await readSignal(preview.page);
    assert.notEqual(waiting.tipTop, null);
    assert.deepEqual(waiting, { ...openWater, tipTop: waiting.tipTop, body: true });
    await preview.advance(1500);
    const nibble = await readSignal(preview.page);
    assert.ok(nibble.tipTop > waiting.tipTop);
    assert.deepEqual(nibble, { ...openWater, tipTop: nibble.tipTop });

    let previous = 1500;
    for (const [time, signal] of [
        [2299, nibble], [2300, waiting], [3199, waiting], [3200, nibble], [3999, nibble],
        [4000, waiting], [4899, waiting], [4900, bite], [6399, bite], [6400, openWater],
    ]) {
        await preview.advance(time - previous);
        assert.deepEqual(await readSignal(preview.page), signal, `canvas at ${time} ms`);
        previous = time;
    }
    assert.equal(await preview.status(), "Fish escaped");
    assert.equal(await preview.advance(16), 0);
});

test("the visible signal agrees with a keyboard hook at the same instant", async t => {
    const preview = await openPreview(t);
    for (const [time, expected] of [
        [3200, "Fish scared away"], [4899, "Cast in progress."], [4900, "Fish hooked"],
        [6399, "Fish hooked"], [6400, "Fish escaped"],
    ]) {
        await preview.cast();
        await preview.advance(time);
        const signal = await readSignal(preview.page);
        assert.equal(signal.innerRing && signal.outerRing, expected === "Fish hooked");
        if (time < 6400) await preview.hook();
        assert.equal(await preview.status(), expected);
        if (expected !== "Cast in progress.") {
            assert.deepEqual(await readSignal(preview.page), openWater);
            assert.ok(await preview.page.getByRole("button", { name: "Simulate hook" }).isDisabled());
        }
    }
});

test("a pull at the closing boundary expires the cast even before the next frame", async t => {
    const preview = await openPreview(t);
    await preview.cast();
    await preview.advance(6399);
    assert.deepEqual(await readSignal(preview.page), bite);
    await preview.advance(1, false);
    await preview.hook();
    assert.equal(await preview.status(), "Fish escaped");
    assert.deepEqual(await readSignal(preview.page), openWater);
    assert.equal(await preview.advance(16), 0);
});

test("a hidden tab pauses the cast and restarting replaces the previous animation", async t => {
    const preview = await openPreview(t);
    const setHidden = hidden => preview.page.evaluate(value => {
        Object.defineProperty(document, "hidden", { configurable: true, value });
        document.dispatchEvent(new Event("visibilitychange"));
    }, hidden);
    await preview.cast();
    await preview.advance(4899);
    await setHidden(true);
    assert.equal(await preview.advance(6000), 0);
    await setHidden(false);
    assert.equal((await readSignal(preview.page)).body, true);
    assert.equal(await preview.advance(1), 1);
    assert.deepEqual(await readSignal(preview.page), bite);
    await preview.cast();
    assert.equal(await preview.advance(1), 1);
    assert.equal((await readSignal(preview.page)).body, true);
    await preview.hook();
    assert.equal(await preview.status(), "Cast in progress.");
});
