// Exercises the real Blazor page with a simulated Oplà. HTTP is intercepted unless the database test is enabled.
const assert = require("node:assert/strict");
const { EventEmitter, once } = require("node:events");
const { test } = require("node:test");
const { chromium } = require("playwright-core");

const port = Number(process.env.BYTEBAIT_TEST_PORT ?? 5250);
const gameUrl = `http://localhost:${port}/game`;
const castUrl = new URL("/api/casts", gameUrl).href;

async function openGame(t, query = "", connect = true) {
    const browser = await chromium.launch({ channel: "chrome" });
    t.after(() => browser.close());
    const page = await browser.newPage();
    page.setDefaultTimeout(8000);
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    page.on("console", message => {
        if (message.type() === "error" && message.text().includes("Unhandled exception")) errors.push(message.text());
    });
    t.after(() => assert.deepEqual(errors, []));
    await page.addInitScript(() => {
        const serial = new EventTarget();
        const port = {
            async open() {
                this.readable = new ReadableStream({ start(controller) {
                    window.feedUsb = line => controller.enqueue(new TextEncoder().encode(line));
                } });
                this.writable = new WritableStream({ write(bytes) {
                    const line = new TextDecoder().decode(bytes);
                    window.usbWrites.push(line.trim());
                    if (line.startsWith("PING:")) {
                        window.lastPing = line;
                        if (window.echoPings) window.feedUsb(line.replace("PING:", "PONG:"));
                    }
                    if (line.startsWith("LED:") && window.stallLed) {
                        window.stallLed = false;
                        return new Promise(resolve => { window.releaseLed = resolve; });
                    }
                } });
            },
            async close() {},
        };
        window.usbWrites = [];
        window.unplugUsb = () => {
            const event = new Event("disconnect");
            // Web Serial bubbles the port's disconnect event to navigator.serial.
            Object.defineProperty(event, "target", { value: port });
            serial.dispatchEvent(event);
        };
        serial.requestPort = async () => port;
        Object.defineProperty(navigator, "serial", { value: serial });
    });
    const epoch = Date.UTC(2026, 0, 1);
    await page.clock.install({ time: new Date(epoch) });
    await page.goto(gameUrl + query);
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
    if (!connect) {
        await visible("Invalid level link. Open a level with a positive numeric ID.");
        return { page, visible };
    }
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
    const hook = () => page.evaluate(() => window.feedUsb("HOOK\n"));
    const leds = () => page.evaluate(() => window.usbWrites.filter(line => line.startsWith("LED:")));
    const signal = () => page.locator("canvas").getAttribute("aria-label");
    const setHidden = hidden => page.evaluate(value => {
        Object.defineProperty(document, "hidden", { configurable: true, value });
        document.dispatchEvent(new Event("visibilitychange"));
    }, hidden);
    return { page, click, feed, visible, receipt, calibrate, hook, leds, signal, setHidden };
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

// Record outcomes after the actual Blazor receiver has accepted the game state.
async function watchFishingOutcomes(page) {
    await page.evaluate(() => {
        window.fishingOutcomes = [];
        const prototype = DotNet.DotNetObject.prototype;
        const invoke = prototype.invokeMethodAsync;
        prototype.invokeMethodAsync = async function (method, ...args) {
            const result = await invoke.call(this, method, ...args);
            if (method === "OnGameState") {
                const [, outcome, cast] = args;
                if (["scared", "escaped", "hooked"].includes(outcome)) {
                    window.fishingOutcomes.push({ outcome, cast });
                }
            }
            return result;
        };
    });
    return () => page.evaluate(() => window.fishingOutcomes);
}

async function watchHooks(page) {
    const outcomes = await watchFishingOutcomes(page);
    return async () => (await outcomes()).filter(event => event.outcome === "hooked").map(event => event.cast);
}

// Simulated PR #101 receipts with deliberately distinct server identities and sizes.
const firstFish = { speciesId: 8, name: "Test perch", rarity: "common", weightKg: 0.82, lengthCm: 28 };
const secondFish = { speciesId: 3, name: "Test pike", rarity: "rare", weightKg: 2.41, lengthCm: 63.5 };
const thirdFish = { speciesId: 9, name: "Test trout", rarity: "common", weightKg: 1.35, lengthCm: 42 };

async function saveLevelCast(game, send, receipt) {
    const route = await send(74);
    assert.equal(route.request().method(), "POST");
    assert.equal(route.request().postDataJSON().levelId, receipt.levelId);
    await game.visible("Saving cast…");
    await route.fulfill({ status: 201, json: receipt });
    await game.receipt("Cast saved.");
}

test("three level casts on one port scare, lose and hook exactly one fish each", async t => {
    const game = await openGame(t, "?levelId=7");
    const { page, visible, leds } = game;
    await visible("Level 7");
    const outcomes = await watchFishingOutcomes(page);
    const apiCalls = [];
    page.on("request", request => { if (new URL(request.url()).pathname.startsWith("/api/")) apiCalls.push(request.url()); });
    const { send } = await interceptCasts(game);
    await game.calibrate();
    await page.evaluate(() => { window.echoPings = true; });

    // Compare all writes from the throw onward so a briefly queued color also fails the test.
    let fromThrow = (await leds()).length;
    await saveLevelCast(game, send, { id: 41, levelId: 7, fish: firstFish });
    await page.clock.runFor(3400);
    assert.match(await game.signal(), /Only the red tip/);
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF"]);
    await game.hook();
    await game.receipt("Fish scared away");
    await visible("Meter running.");
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF", "LED:RED"]);
    await page.clock.runFor(320);

    fromThrow = (await leds()).length;
    await saveLevelCast(game, send, { id: 58, levelId: 7, fish: secondFish });
    await page.clock.runFor(5100);
    assert.match(await game.signal(), /Two rings/);
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF"]);
    await page.clock.runFor(1320); // Cross the 6400 ms timeout and allow its next animation frame.
    await visible("Fish escaped");
    await game.hook();
    await page.clock.runFor(32);
    await visible("Fish escaped");
    await visible("Meter running.");
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF", "LED:RED"]);
    await page.clock.runFor(320);

    fromThrow = (await leds()).length;
    await saveLevelCast(game, send, { id: 87, levelId: 7, fish: thirdFish });
    await page.clock.runFor(5100);
    assert.match(await game.signal(), /Two rings/);
    await game.hook();
    await game.receipt("Fish hooked");
    await game.hook();
    await page.clock.runFor(100);
    await visible("Fish hooked");
    await visible("Throws received: 3");
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF"]);
    assert.deepEqual(await outcomes(), [
        { outcome: "scared", cast: null },
        { outcome: "escaped", cast: null },
        { outcome: "hooked", cast: { castId: 87, levelId: 7, fish: thirdFish } },
    ]);
    assert.doesNotMatch(await page.locator("body").innerText(), /Test perch|Test pike|Test trout|63\.5/);
    await game.click("Cast again");
    await visible("Meter running.");
    assert.deepEqual((await leds()).slice(fromThrow), ["LED:OFF", "LED:RED"]);
    assert.deepEqual(apiCalls, [castUrl, castUrl, castUrl]);
});

test("unplugging during a bite discards a partial hook before the next port and fish", async t => {
    const game = await openGame(t, "?levelId=7");
    const { page, visible } = game;
    const outcomes = await watchFishingOutcomes(page);
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    await saveLevelCast(game, send, { id: 41, levelId: 7, fish: firstFish });
    await page.clock.runFor(5100);
    assert.match(await game.signal(), /Two rings/);
    // The throw count confirms the reader processed this chunk, including its unfinished HO suffix.
    await page.evaluate(() => window.feedUsb("THROW:20\nHO"));
    await game.receipt("Throws received: 2");
    await page.evaluate(() => window.unplugUsb());
    await visible("USB disconnected.");
    await visible("Cast ended when USB disconnected.");

    await game.click("Connect USB");
    await visible("USB connected.");
    await visible("Measuring USB latency…");
    await visible("Throws received: 0");
    await game.calibrate();
    await saveLevelCast(game, send, { id: 87, levelId: 7, fish: secondFish });
    await page.clock.runFor(5100);
    assert.match(await game.signal(), /Two rings/);
    const priorWrites = await page.evaluate(() => window.usbWrites.length);
    await page.evaluate(() => window.feedUsb("OK\n"));
    await page.clock.runFor(32);
    const replies = await page.evaluate(start => window.usbWrites.slice(start), priorWrites);
    assert.ok(replies.includes("ERR:UNKNOWN"), "The reopened port must parse OK as a separate line");
    await visible("Cast in progress.");
    assert.deepEqual(await outcomes(), []);

    await game.hook();
    await game.receipt("Fish hooked");
    assert.deepEqual(await outcomes(), [{ outcome: "hooked", cast: { castId: 87, levelId: 7, fish: secondFish } }]);
    assert.equal(requests.length, 2);
});

test("missing, malformed or mismatched level receipts rearm the meter and discard hooks", async t => {
    const game = await openGame(t, "?levelId=7");
    const hooks = await watchHooks(game.page);
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    const valid = { id: 21, levelId: 7, fish: firstFish };
    for (const body of [
        JSON.stringify({ id: 21 }), JSON.stringify({ ...valid, fish: null }),
        JSON.stringify({ ...valid, levelId: 4 }), JSON.stringify({ ...valid, id: 0 }),
        JSON.stringify({ ...valid, fish: { ...firstFish, speciesId: 0 } }),
        JSON.stringify({ ...valid, fish: { ...firstFish, name: "" } }),
        JSON.stringify({ ...valid, fish: { ...firstFish, rarity: "unknown" } }),
        JSON.stringify({ ...valid, fish: { ...firstFish, lengthCm: 0 } }),
        JSON.stringify({ ...valid, fish: { ...firstFish, weightKg: -1 } }), "{",
    ]) {
        const route = await send(55);
        await route.fulfill({ status: 201, contentType: "application/json", body });
        await game.receipt("Cast saved, but no fish is available for this cast.");
        await game.visible("Meter running.");
        await game.hook();
        await game.page.clock.runFor(320);
    }
    assert.equal(requests.length, 10);
    assert.deepEqual(await hooks(), []);
});

test("invalid level links block casting including empty or repeated IDs", async t => {
    const game = await openGame(t, "?levelId=", false);
    for (const ending of ["0", "-1", "abc", "2147483648", "7&levelId=8"]) {
        await game.page.goto(gameUrl + "?levelId=" + ending);
        await game.visible("Invalid level link. Open a level with a positive numeric ID.");
        assert.equal(await game.page.getByRole("button", { name: "Connect USB" }).count(), 0);
    }
});

test("changing the level on the same game page discards a pending receipt", async t => {
    const game = await openGame(t, "?levelId=7");
    const { page } = game;
    const hooks = await watchHooks(page);
    await game.calibrate();
    const { send, requests } = await interceptCasts(game);
    const old = await send(50);
    // Use an internal link so Blazor reuses the route component, without reloading WebAssembly.
    await page.evaluate(() => {
        const link = document.createElement("a");
        link.href = "/game?levelId=8";
        document.body.append(link);
        link.click();
        link.remove();
    });
    await game.receipt("Level 8");
    await game.receipt("USB disconnected.");
    await game.click("Start meter");
    await game.click("Connect USB");
    await game.visible("USB connected.");
    await game.calibrate();
    const next = await send(61);
    assert.equal(next.request().postDataJSON().levelId, 8);
    await old.fulfill({ status: 201, json: { id: 41, levelId: 7, fish: firstFish } });
    await page.clock.runFor(64);
    await game.visible("Saving cast…");
    await next.fulfill({ status: 201, json: { id: 87, levelId: 8, fish: secondFish } });
    await game.receipt("Cast saved.");
    await page.clock.runFor(5020);
    await game.hook();
    await game.receipt("Fish hooked");
    await page.clock.runFor(64);
    assert.deepEqual(await hooks(), [{ castId: 87, levelId: 8, fish: secondFish }]);
    assert.equal(requests.length, 2);
});

test("Blazor receives a USB hook and keeps reading subsequent throws", async t => {
    const { page, feed, visible, receipt } = await openGame(t);
    await page.evaluate(() => window.feedUsb("HOOK\n"));
    // A missing .NET hook callback would end the read loop before it reads this next chunk.
    await feed(72);
    await receipt("Throws received: 1");
    await visible("Last throw strength: 72");
    await visible("USB connected.");
});

test("one scored throw waits for 201, then a USB pull in the bite hooks that cast", async t => {
    const game = await openGame(t);
    const { page, feed, visible, hook, leds } = game;
    await visible("Practice mode");
    const hooks = await watchHooks(page);
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
    const before = await leds();
    assert.equal(before.at(-1), "LED:OFF");

    await feed(99);
    await page.clock.runFor(6000);
    await hook();
    await visible("Throws received: 3");
    await visible("Last throw strength: 72");
    assert.match(await game.signal(), /pale body/);
    await route.fulfill({ status: 201, json: { id: 1 } });
    await game.receipt("Cast saved.");
    await visible("Cast in progress.");
    await page.clock.runFor(1600);
    assert.match(await game.signal(), /Only the red tip/);
    await page.clock.runFor(1750);
    assert.match(await game.signal(), /Only the red tip/);
    await page.clock.runFor(1700);
    assert.match(await game.signal(), /Two rings/);
    await hook();
    await visible("Fish hooked");
    await feed(0);
    await hook();
    await page.clock.runFor(1000);
    await visible("Throws received: 4");
    await visible("Fish hooked");
    assert.equal(await game.signal(), "Open water");
    assert.deepEqual(await hooks(), [null]);
    assert.deepEqual(await leds(), before);
    assert.equal(requests.length, 1);
    await game.click("Cast again");
    await visible("Meter running.");
    assert.equal((await leds()).at(-1), "LED:RED");
});

test("a stalled writer drops waiting zone colors and the meter restarts after either loss", async t => {
    const game = await openGame(t);
    const { page, visible, leds } = game;
    await page.setViewportSize({ width: 390, height: 844 });
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    // Hold a write that has already reached the port, then cross into another zone behind it.
    await page.evaluate(() => { window.stallLed = true; });
    await page.clock.runFor(870);
    const sentBeforeThrow = await leds();
    assert.equal(sentBeforeThrow.at(-1), "LED:RED");
    await page.clock.runFor(550);
    const first = await send(60);
    await visible("Saving cast…");
    await page.evaluate(() => window.releaseLed());
    await page.clock.runFor(32);
    const off = [...sentBeforeThrow, "LED:OFF"];
    assert.deepEqual(await leds(), off);
    await first.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await page.clock.runFor(1600);
    assert.deepEqual(await leds(), off);
    await game.hook();
    await visible("Fish scared away");
    await visible("Meter running.");
    assert.equal((await leds()).at(-1), "LED:RED");
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);

    // A new cast cannot score against the previous sweep's history.
    await game.feed(12);
    await game.receipt("Cast not scored. No meter history for this throw.");
    await page.clock.runFor(310);
    const next = await send(80);
    assert.equal(next.request().postDataJSON().zone, "red");
    await next.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await page.clock.runFor(6450);
    await visible("Fish escaped");
    await visible("Meter running.");
    await game.hook();
    assert.equal((await leds()).at(-1), "LED:RED");
    await page.clock.runFor(310);
    const third = await send(20);
    await third.fulfill({ status: 400, json: {} });
    await game.receipt("Cast rejected by the server.");
    assert.equal(requests.length, 3);
});

test("rejected, unexpected and lost replies each rearm the meter without replaying the POST", async t => {
    const game = await openGame(t);
    const { page, visible } = game;
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    for (const [raw, status, message] of [
        [0, 400, "Cast rejected by the server."],
        [100, 200, "Cast save could not be confirmed."],
        [30, null, "Cast save could not be confirmed."],
    ]) {
        const route = await send(raw);
        await visible("Saving cast…");
        assert.equal(route.request().postDataJSON().strength, raw / 100);
        if (status) await route.fulfill({ status, json: {} });
        else await route.abort("failed");
        await game.receipt(message);
        await visible("Meter running.");
        assert.equal((await game.leds()).at(-1), "LED:RED");
        await page.clock.runFor(320);
    }
    await game.click("Pause meter");
    await game.feed(31);
    await visible("Cast not scored. No meter history for this throw.");
    await visible("Throws received: 4");
    assert.equal(requests.length, 3);
});

test("late receipts cannot start the float after unplugging, reconnecting or leaving the page", async t => {
    const game = await openGame(t, "?levelId=7");
    const { page, visible, click } = game;
    const { send, requests } = await interceptCasts(game);
    await game.calibrate();
    const oldSession = await send(50);
    await page.evaluate(() => window.unplugUsb());
    await visible("USB disconnected.");
    await visible("Cast ended when USB disconnected.");
    await click("Connect USB");
    await visible("Throws received: 0");
    await game.calibrate();
    const onPreviousPage = await send(80);
    await oldSession.fulfill({ status: 201, json: { id: 2, levelId: 7, fish: firstFish } });
    await page.clock.runFor(64);
    await visible("Saving cast…");
    await visible("Last throw strength: 80");
    await page.getByRole("link", { name: "Back to home" }).press("Enter");
    await page.getByRole("link", { name: "Open game" }).press("Enter");
    await visible("USB disconnected.");
    await onPreviousPage.fulfill({ status: 201, json: { id: 3, levelId: 7, fish: secondFish } });
    await page.clock.runFor(32);
    await visible("Throws received: 0");
    await visible("Meter ready.");
    await visible("Practice mode");
    assert.equal(await page.getByText("Cast saved.", { exact: true }).count(), 0);
    assert.equal(requests.length, 2);
});

test("leaving during the bite releases the cast and the next visit starts with a still meter", async t => {
    const game = await openGame(t);
    const { page, visible } = game;
    await game.calibrate();
    const { send, requests } = await interceptCasts(game);
    const cast = await send(75);
    await cast.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await page.clock.runFor(5020);
    assert.match(await game.signal(), /Two rings/);
    await page.getByRole("link", { name: "Back to home" }).press("Enter");
    await page.getByRole("link", { name: "Open game" }).press("Enter");
    await visible("USB disconnected.");
    await page.clock.runFor(6000);
    await visible("Meter ready.");
    await visible("Throws received: 0");
    assert.equal(await game.signal(), "Casting meter");
    assert.equal(await page.getByText("Fish escaped", { exact: true }).count(), 0);
    assert.equal(requests.length, 1);
});

test("a hidden receipt and hidden pulls wait for visible time, while the firmware deadline stays fixed", async t => {
    const game = await openGame(t);
    const { page, visible, setHidden } = game;
    const { send } = await interceptCasts(game);
    await game.calibrate();
    await page.evaluate(() => { window.echoPings = true; });
    const first = await send(70);
    await setHidden(true);
    await first.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await page.clock.runFor(6000);
    await setHidden(false);
    assert.match(await game.signal(), /pale body/);
    await page.clock.runFor(1600);
    await setHidden(true);
    await game.hook();
    await page.clock.runFor(3000);
    await setHidden(false);
    assert.match(await game.signal(), /Only the red tip/);
    await game.hook();
    await visible("Fish scared away");

    await page.clock.runFor(310);
    const second = await send(80);
    await page.clock.runFor(3200);
    await second.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await setHidden(true);
    // The save and time spent hidden together leave too little time for the complete cadence.
    for (let i = 0; i < 13; i++) await page.clock.fastForward(4000);
    await setHidden(false);
    await visible("Controller motion window expired. Cast again.");
    await visible("Meter running.");
    await visible("USB connected.");
    assert.equal((await game.leds()).at(-1), "LED:RED");
});

test("losing the USB heartbeat discards a float paused in another tab", async t => {
    const game = await openGame(t);
    await game.calibrate();
    const { send } = await interceptCasts(game);
    const cast = await send(40);
    await cast.fulfill({ status: 201, json: {} });
    await game.receipt("Cast saved.");
    await game.setHidden(true);
    await game.page.clock.fastForward(16000);
    await game.visible("USB connection lost. No reply for 15 seconds.");
    await game.setHidden(false);
    await game.visible("Cast ended when USB disconnected.");
    assert.equal(await game.signal(), "Casting meter");
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
    await game.visible("Meter running.");
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

test("the fish from the real level endpoint reaches the hooked game state unchanged", {
    skip: process.env.BYTEBAIT_TEST_CAST_DATABASE !== "1" || !process.env.BYTEBAIT_TEST_LEVEL_ID,
}, async t => {
    const levelId = Number(process.env.BYTEBAIT_TEST_LEVEL_ID);
    assert.ok(Number.isInteger(levelId) && levelId > 0);
    const game = await openGame(t, "?levelId=" + levelId);
    const { page } = game;
    const hooks = await watchHooks(page);
    const apiCalls = [];
    page.on("request", request => { if (new URL(request.url()).pathname.startsWith("/api/")) apiCalls.push(request.url()); });
    await game.calibrate();
    const reply = page.waitForResponse(castUrl);
    await game.feed(72);
    const response = await reply;
    assert.equal(response.status(), 201);
    const saved = await response.json();
    assert.equal(saved.levelId, levelId);
    assert.ok(saved.id > 0 && saved.fish?.speciesId > 0);
    await game.receipt("Cast saved.");
    await page.clock.runFor(5050);
    await game.hook();
    await game.receipt("Fish hooked");
    await game.hook();
    await page.clock.runFor(100);
    assert.deepEqual(await hooks(), [{ castId: saved.id, levelId, fish: saved.fish }]);
    assert.deepEqual(apiCalls, [castUrl]);
    t.diagnostic("Verified PostgreSQL level cast " + JSON.stringify(saved));
});
