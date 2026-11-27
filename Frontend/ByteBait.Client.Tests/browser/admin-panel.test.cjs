// The admin panel (/admin) in Chrome with a mocked API: slow saves, a failed reload and a form from before
// another admin's save. Runs against the standalone client on port 5173, like cast-saving.test.cjs.
const assert = require("node:assert/strict");
const { test } = require("node:test");
const { chromium } = require("playwright-core");

const port = Number(process.env.BYTEBAIT_TEST_PORT ?? 5173);
const adminUrl = `http://localhost:${port}/admin`;
const api = path => new URL(path, adminUrl).href;

const lanes = [{ id: 1, number: 1, name: "Nordborg Lake" }];
const fish = (id, name, price, version) => ({
    id, laneId: 1, name, rarity: "common", depth: "shallow",
    minWeightKg: 1, maxWeightKg: 2, minLengthCm: 20, maxLengthCm: 40, price, version,
});

// Opens /admin as an admin. species is the list GET /api/admin/species answers with until a test changes it.
async function openAdmin(t, species) {
    const browser = await chromium.launch({ channel: "chrome" });
    t.after(() => browser.close());
    const page = await browser.newPage();
    page.setDefaultTimeout(8000);
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    t.after(() => assert.deepEqual(errors, []));

    const state = { species, speciesStatus: 200 };
    await page.route(api("/api/account/me"), route =>
        route.fulfill({ json: { id: "6f0f3f5e-52a1-4b0e-9d5c-0c1b7c2a8e41", userName: "admin", roles: ["admin"], money: 0 } }));
    await page.route(api("/api/account/antiforgery"), route => route.fulfill({ json: { token: "test-token" } }));
    await page.route(api("/api/admin/lanes"), route => route.fulfill({ json: lanes }));
    await page.route(api("/api/admin/species"), route => route.request().method() === "GET"
        ? route.fulfill(state.speciesStatus === 200 ? { json: state.species } : { status: state.speciesStatus, json: {} })
        : route.fallback());

    await page.goto(adminUrl);
    await page.getByRole("heading", { name: "Fish species", exact: true }).waitFor();
    const row = name => page.getByRole("row").filter({ has: page.getByRole("cell", { name, exact: true }) });
    return { page, state, row };
}

// Holds the next request to url until the test answers it
function holdNext(page, url, method) {
    return new Promise(resolve => {
        page.route(url, route => route.request().method() === method ? resolve(route) : route.fallback(), { times: 1 });
    });
}

test("while a save is on its way the rows are locked, and the receipt names the species that was saved", async t => {
    const { page, row } = await openAdmin(t, [fish(1, "Perch", 10, 7), fish(2, "Tench", 20, 7)]);
    await row("Perch").getByRole("button", { name: "Edit", exact: true }).click();
    await page.locator("#price").fill("80");

    const save = holdNext(page, api("/api/admin/species/1"), "PUT");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    const put = await save;

    assert.equal(put.request().postDataJSON().version, 7);
    assert.ok(await row("Tench").getByRole("button", { name: "Edit", exact: true }).isDisabled());
    assert.ok(await row("Tench").getByRole("button", { name: "Delete", exact: true }).isDisabled());

    await put.fulfill({ json: fish(1, "Perch", 80, 8) });
    await page.getByText("Perch saved.", { exact: true }).waitFor();
    assert.ok(await row("Tench").getByRole("button", { name: "Edit", exact: true }).isEnabled());
});

test("a confirmed delete removes the row, and a failed reload offers Try again", async t => {
    const { page, state, row } = await openAdmin(t, [fish(1, "Perch", 10, 7), fish(2, "Tench", 20, 7)]);
    await page.route(api("/api/admin/species/2"), route => route.request().method() === "DELETE"
        ? route.fulfill({ status: 204 })
        : route.fallback());
    state.speciesStatus = 503;

    await row("Tench").getByRole("button", { name: "Delete", exact: true }).click();
    await page.getByRole("button", { name: "Delete Tench", exact: true }).click();

    await page.getByText("Tench deleted.", { exact: true }).waitFor();
    await page.getByText("Couldn't load the latest species, so the list below may be out of date.").waitFor();
    assert.equal(await row("Tench").count(), 0);

    state.speciesStatus = 200;
    state.species = [fish(1, "Perch", 10, 7)];
    await page.getByRole("button", { name: "Try again", exact: true }).click();
    await page.getByText("Couldn't load the latest species", { exact: false }).waitFor({ state: "detached" });
    assert.equal(await row("Perch").count(), 1);
});

test("a form from before another admin's save is turned down and can be opened again", async t => {
    const { page, state, row } = await openAdmin(t, [fish(1, "Perch", 10, 7)]);
    await page.route(api("/api/admin/species/1"), route => route.request().method() === "PUT"
        ? route.fulfill({ status: 409, json: { title: "Species has changed", detail: "Another admin saved this species after you opened it. Open it again to see their change." } })
        : route.fallback());

    await row("Perch").getByRole("button", { name: "Edit", exact: true }).click();
    await page.locator("#name").fill("Big Perch");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await page.getByText("Another admin saved this species after you opened it. Open it again to see their change.").waitFor();

    // Meanwhile the other admin changed the price to 80
    state.species = [fish(1, "Perch", 80, 8)];
    await page.getByRole("button", { name: "Open the latest version", exact: true }).click();
    await page.getByRole("heading", { name: "Edit Perch", exact: true }).waitFor();
    assert.equal(await page.locator("#price").inputValue(), "80");
    assert.equal(await page.locator("#name").inputValue(), "Perch");
});
