// Exercises the real Blazor page with a simulated Oplà. HTTP is intercepted unless the database test is enabled.
const assert = require("node:assert/strict");
const { EventEmitter, once } = require("node:events");
const { test } = require("node:test");
const { chromium } = require("playwright-core");

const port = Number(process.env.BYTEBAIT_TEST_PORT ?? 5250);
const gameUrl = `http://localhost:${port}/game`;
const castUrl = new URL("/api/casts", gameUrl).href;

async function openGame(t) {
    const browser = await chromium.launch({ channel: "chrome" });
    t.after(() => browser.close());
    const page = await browser.newPage();
    page.setDefaultTimeout(8000);
    await page.addInitScript(() => {
        const serial = new EventTarget();
        const port = {
            async open() {
                this.readable = new ReadableStream({ start(controller) {
                    window.feedUsb = line => controller.enqueue(new TextEncoder().encode(line));
                } });
                this.writable = new WritableStream({ write(bytes) {
                    const line = new TextDecoder().decode(bytes);
                    if (line.startsWith("PING:")) window.lastPing = line;
                } });
            },
            async close() {},
        };
        serial.requestPort = async () => port;
        Object.defineProperty(navigator, "serial", { value: serial });
    });
    const epoch = Date.UTC(2026, 0, 1);
    await page.clock.install({ time: new Date(epoch) });
    await page.goto(gameUrl);
    const visible = text => page.getByText(text, { exact: true }).waitFor();
    // Fetch and WebAssembly continuations cross real network and browser timer queues.
    const receipt = async text => {
        const status = page.getByText(text, { exact: true });
        const deadline = Date.now() + 8000;
        while (!(await status.isVisible()) && Date.now() < deadline) await page.clock.runFor(16);
        assert.ok(await status.isVisible(), await page.locator("body").innerText());
    };
    const click = async name => {
        await page.getByRole("button", { name, exact: true }).focus();
        await page.keyboard.press("Enter");
    };
    const feed = strength => page.evaluate(n => window.feedUsb("THROW:" + n + "\n"), strength);
    await visible("USB disconnected.");
    await page.clock.pauseAt(new Date(epoch + 15000));
    await click("Start meter");
    await click("Connect USB");
    await visible("USB connected.");
    const calibrate = async () => {
        await page.clock.runFor(20);
        await page.evaluate(() => window.feedUsb(window.lastPing.replace("PING:", "PONG:")));
        await visible("Estimated one-way time: 10.0 ms");
        await page.clock.runFor(1240);
    };
    return { page, click, feed, visible, receipt, calibrate };
}

// Hold each request until the test chooses its outcome, including out-of-order replies.
async function interceptCasts(game) {
    const events = new EventEmitter();
    const requests = [];
    await game.page.route(castUrl, route => {
        requests.push(route.request());
        events.emit("post", route);
    });
    const send = async strength => {
        const received = once(events, "post", { signal: AbortSignal.timeout(8000) });
        await game.feed(strength);
        return (await received)[0];
    };
    return { send, requests };
}

test("a scored throw sends the historical zone, normalized strength and UTC trigger once", async t => {
    const game = await openGame(t);
    const { page, feed, visible } = game;
    const { send, requests } = await interceptCasts(game);
    await feed(72);
    await visible("Cast not scored. Waiting for USB latency.");
    await game.calibrate();
    const expectedUtc = await page.evaluate(() => new Date(performance.timeOrigin + performance.now() - 260).toISOString());
    const route = await send(72);
    await visible("Last cast zone: Green");
    await visible("Saving cast…");
    assert.equal(route.request().method(), "POST");
    assert.match(route.request().headers()["content-type"], /application\/json/);
    const body = route.request().postDataJSON();
    assert.match(body.castAt, /Z$/);
    assert.deepEqual({ ...body, castAt: new Date(body.castAt).toISOString() }, { zone: "green", strength: 0.72, castAt: expectedUtc });
    await route.fulfill({ status: 201, json: { id: 1 } });
    await game.receipt("Cast saved.");
    for (const [raw, status, message] of [
        [0, 400, "Cast rejected by the server."],
        [100, 200, "Cast save could not be confirmed."],
    ]) {
        const attempt = await send(raw);
        assert.equal(attempt.request().postDataJSON().strength, raw / 100);
        await attempt.fulfill({ status, json: {} });
        await game.receipt(message);
    }
    const networkFailure = await send(30);
    await visible("Saving cast…");
    await networkFailure.abort("failed");
    await game.receipt("Cast save could not be confirmed.");
    await game.click("Pause meter");
    await feed(31);
    await visible("Cast not scored. No meter history for this throw.");
    await visible("Throws received: 6");
    assert.equal(requests.length, 4);
});

test("old replies cannot replace a newer cast or appear after reconnecting or leaving", async t => {
    const game = await openGame(t);
    const { page, visible, click } = game;
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    const first = await send(72);
    const second = await send(72);
    await visible("Throws received: 2");
    await second.fulfill({ status: 400, json: {} });
    await game.receipt("Cast rejected by the server.");
    await first.fulfill({ status: 201, json: { id: 1 } });
    await page.clock.runFor(32);
    await visible("Cast rejected by the server.");

    const oldSession = await send(50);
    await click("Disconnect USB");
    await visible("USB disconnected.");
    await click("Connect USB");
    await visible("Throws received: 0");
    await game.calibrate();
    await oldSession.fulfill({ status: 201, json: { id: 2 } });
    await page.clock.runFor(32);
    assert.equal(await page.getByText("Cast saved.", { exact: true }).count(), 0);

    const onPreviousPage = await send(80);
    await page.getByRole("link", { name: "Back to home" }).press("Enter");
    await page.getByRole("link", { name: "Open game" }).press("Enter");
    await visible("USB disconnected.");
    await onPreviousPage.fulfill({ status: 201, json: { id: 3 } });
    await page.clock.runFor(32);
    await visible("Throws received: 0");
    assert.equal(await page.getByText("Cast saved.", { exact: true }).count(), 0);
    assert.equal(requests.length, 4);
});

test("a timed-out POST stays unconfirmed and is not sent a second time", async t => {
    const game = await openGame(t);
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    await send(90);
    await game.visible("Saving cast…");
    await game.page.clock.runFor(10010);
    await game.receipt("Cast save could not be confirmed.");
    await game.visible("USB connected.");
    assert.equal(requests.length, 1);
});

test("one simulated throw through the real API receives a database receipt", {
    skip: process.env.BYTEBAIT_TEST_CAST_DATABASE !== "1",
}, async t => {
    const game = await openGame(t);
    const requests = [];
    game.page.on("request", r => { if (r.url() === castUrl) requests.push(r); });
    await game.calibrate();
    const reply = game.page.waitForResponse(castUrl);
    await game.feed(72);
    const response = await reply;
    assert.equal(response.status(), 201);
    await game.receipt("Cast saved.");
    await game.visible("Throws received: 1");
    assert.equal(requests.length, 1);
    const sent = requests[0].postDataJSON();
    const saved = await response.json();
    assert.ok(Number.isInteger(saved.id) && saved.id > 0);
    assert.equal(saved.zone, "green");
    assert.equal(saved.strength, 0.72);
    assert.equal(Date.parse(saved.castAt), Date.parse(sent.castAt));
    t.diagnostic("PostgreSQL cast receipt " + JSON.stringify(saved));
});
