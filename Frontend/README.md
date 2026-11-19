# Frontend

ByteBait's frontend is a standalone Blazor WebAssembly app targeting .NET 10.
The project is in `Frontend/ByteBait.Client/` and is included in the root solution.

## Requirements

- .NET 10 SDK. A .NET runtime alone cannot build the project.
- A current browser, such as Chrome or Edge.
- Node.js 22 for JavaScript syntax checks and module tests; no npm packages are required.

## Run locally

Run these commands from the repository root:

```powershell
dotnet restore ByteBait.slnx
dotnet run --project Frontend/ByteBait.Client --launch-profile http
```

Open <http://localhost:5173>. The development server does not open a browser automatically.
Local development uses HTTP on localhost, so no development certificate is needed.
Stop the server with `Ctrl+C`.

The foundation page at `/` displays **ByteBait** and **Frontend foundation is ready.**
Choose **Open game** to navigate to `/game`, or open <http://localhost:5173/game> directly.
The meter, USB connection and LEDs work on the standalone client. To save a cast, run
the API with a PostgreSQL connection and open its copy of the game at
<http://localhost:5250/game>. See the [backend setup](../Backend/README.md).

## Casting meter

The black needle starts at rest on the left. Choose **Start meter** to sweep across the arc
in two seconds and back in two more. Each pass through the green zone lasts 400 ms.
**Pause meter** freezes the needle. **Resume meter** continues in the same direction.
The meter waits for manual start even with reduced motion enabled. Switching tabs freezes
a running needle until the game is visible again. Returning to the game after leaving the
page opens a fresh meter.

`Pages/Game.razor` owns the keyboard-accessible controls and status. On first render it
imports `wwwroot/js/timing-meter.js` and calls `initialize(canvas)`. Subsequent `start(canvas)`
and `stop(canvas)` calls control one animation loop per canvas. JavaScript uses differences
between visible animation-frame timestamps to move and draw the needle. Removing the canvas
cancels its frame and releases its listener and observer.

The 320 × 180 drawing buffer scales with the page. The arc is centered at (160, 138),
with a radius of 96 pixels and a 16-pixel stroke. Reading from left to right, the five equal
segments are red, yellow, green, yellow and red. The same zone boundaries drive the painting
and `getZone(position)`, which takes a number from 0 to 1 and returns the lowercase zone name.
At a boundary the zone on the right applies, so 0.4 is green, 0.6 is yellow and 1 is red.

### Reading the needle history

`readMeterAt(canvas, targetMs)` returns `{ position, zone, frameTimestampMs }` for the last
frame drawn at or before `targetMs`. Both timestamps are milliseconds on the page's
`performance.now()` clock. Each frame is recorded immediately after its needle is drawn.
For example, if a green frame is drawn at 1180 ms and a yellow frame at 1220 ms, a lookup
for 1210 ms returns green with `frameTimestampMs` equal to 1180.

The target can be up to and including 500 ms before the lookup. During a visible rendering
stall, the last frame still describes the needle on screen, even when that frame is older
than 500 ms. Each canvas starts a fresh history with its first active frame after start,
resume or return to a visible tab. Pausing, hiding the tab or leaving the page clears its
history. An invalid target or a time outside the available history returns `null`.

## USB controller

Open `/game` in desktop Chrome or Edge on localhost or HTTPS. Close Arduino Serial Monitor
if it is using the controller. Choose **Connect USB** and select the MKR WiFi 1010 in
the browser's port picker. Once **USB connected.** appears, one valid `THROW:72` line
sets **Last throw strength** to 72 and adds one to **Throws received**. A cast can arrive
while the meter is paused. A new successful connection starts at zero.

`usb-controller.js` reads the port at 115200 baud, 8N1. Each connection has a separate
32-byte ASCII line buffer. LF completes a message and CR is ignored. The reader accepts
`HELLO:USB:1`, `PONG:<counter>` and integer throw strengths from 0 to 100. It can receive
a cast immediately after opening the port because the controller may already have sent
its startup greeting. Protocol errors are reported to the board at most once per second.
JavaScript timestamps the chunk that completes each throw with `performance.now()`.
Blazor shows the strength and increments the counter once per valid throw, including
when the meter has no zone for it.

### USB latency

The game sends a numbered `PING` when the port opens and every five seconds thereafter,
including while the meter is paused. JavaScript timestamps the actual write and the
received `PONG` with `performance.now()`. A matching echo gives the **Round-trip time**.
Half that value is the **Estimated one-way time**, assuming similar delays in both
directions. The page keeps the latest sample at full precision and displays milliseconds
to one decimal place. For example, an echo received 20 ms after sending gives a 10 ms estimate.

Before the first match the page shows **Measuring USB latency…**. After 15 seconds
without a matching reply, measured from opening or from the last accepted reply, the game
releases the port and clears the sample. Choose **Connect USB** to start a fresh measurement.
Unknown, duplicate and superseded echoes do not extend the deadline.

Choose **Disconnect USB** to release the port. Removing the cable or leaving the game also
ends the session. After reconnecting, an incomplete line from the previous session is
discarded. See the [controller protocol](../docs/protocol.md) for the message format.

### Live zone on the Oplà LEDs

All five LEDs follow the zone under the moving needle. Connecting during green sends
`LED:GREEN` immediately, even before the first latency reply. Across a full outward
sweep the board receives red, yellow, green, yellow and red. Frames in the same zone
share one command. A throw can score a previous zone while the LEDs follow the live one.

`subscribeZone(canvas, listener)` reports the current zone immediately and each change
after drawing. It reports `off` while the meter is ready, paused or hidden. Resume or
return to the running game to light the LEDs on the first visible frame. The USB session
writes these updates on the same writer as its probes. If a write stalls, the newest
waiting color replaces earlier ones. Disconnecting or leaving the page removes the
listener and attempts a final `LED:OFF` before releasing the port.

### Cast zone at the trigger

The controller reports strength after its 250 ms peak-measurement window. Since the USB
message carries no trigger timestamp, the browser estimates it by subtracting 250 ms and
the current connection's latest one-way latency from the receipt time. It looks up the last
frame drawn on the game canvas at or before that instant. A throw received at 1260 ms
with a 20 ms round trip targets 1000 ms. If the last frame visible at 1000 ms was green
and the needle is yellow on receipt, the page shows **Last cast zone: Green** while the needle
continues moving. The result keeps the estimated trigger time (1000 ms in this example)
as `castAt` in ISO 8601 UTC, using the page's `performance.timeOrigin`.

Before the connection's first matching echo, a throw shows **Cast not scored. Waiting
for USB latency.** A paused or hidden meter, a trigger before the first active frame, or
a target older than 500 ms gives **Cast not scored. No meter history for this throw.**
Either message replaces the previous zone. A new connection clears the last throw.

### Save a scored cast

Blazor sends one same-origin `POST /api/casts` per scored throw. The JSON carries the
historical `zone`, `strength` divided by 100, and the estimated trigger time `castAt`
in UTC. For example, `THROW:72` in green sends a strength of `0.72`, while the raw
strength on screen stays at 72. Throws without a score only update the local counter.

**Saving cast…** appears below the zone while the request is pending. **Cast saved.**
means the API returned `201 Created` after writing to PostgreSQL. A `400` displays
**Cast rejected by the server.** Other responses, a network error, or a ten-second
timeout display **Cast save could not be confirmed.** A response can be lost after an
insert, so the client sends each POST once. New throws and USB messages continue while
a save is pending. Only the latest throw on the current game page can show its receipt.
See the [Sprint 1 API contract](../docs/api-contract.md#gem-et-kast).

## Build and test

Run from the repository root:

```powershell
dotnet restore ByteBait.slnx
dotnet build ByteBait.slnx --configuration Release --no-restore
node --check Frontend/ByteBait.Client/wwwroot/js/timing-meter.js
node --check Frontend/ByteBait.Client/wwwroot/js/usb-controller.js
node --test "Frontend/ByteBait.Client.Tests/*.test.mjs"
git diff --check
```

The Node suite checks zone boundaries and arc geometry, needle positions at 30 and 60 fps,
both turning points, pause and resume on the return sweep, tab visibility, rapid commands,
navigation cleanup and initialization failures. History tests cover the draw time of a
zone change, the inclusive 500 ms limit, rendering stalls and separate canvas histories.
The USB tests supply simulated serial ports. They check split and batched messages,
error replies, exclusive reader and writer locks, unplugging, and cleanup when port
selection or opening finishes after navigation. Controlled-clock tests check probe timing,
matched and stale echoes, queued writes, the 15-second deadline and manual reconnection.
Cast integration tests feed serial bytes into the running meter and check the historical
zone, UTC trigger time, missing history, fractional latency at the 500 ms limit, and the
order of throws and replies in a single delivery.
LED tests check both sweeps, pause, tab visibility, unplugging, navigation and reconnecting
mid-sweep. A simulated slow port checks that old colors are dropped and casts still arrive.
It also runs the existing animation foundation tests. The original square demo and its
verification record are documented in the [issue #7 frontend README](https://github.com/Mercantec-GHC/H3-Online-ByteBait/blob/9387ca0ba1fd2f6276a42b8521d9fd6aa212f69a/Frontend/README.md).

## Browser checks

1. Start the Release client with `dotnet run --project Frontend/ByteBait.Client --configuration Release --no-build --launch-profile http` and open <http://localhost:5173/game>.
2. Set the viewport to 1280 CSS pixels wide, then 390. At each width check that the complete
   upper semicircle fits inside the frame, with green at the top, red at both ends and no
   horizontal page scrolling. The black needle should be still at the left endpoint.
3. Use the keyboard to activate **Start meter**, then **Pause meter** on the return sweep.
   Check that the needle stays still and **Resume meter** continues toward the left.
   Switch tabs while running and while paused. Only the running meter should resume.
4. Choose **Back to home** while running, return via **Open game**, and reload. Confirm
   a fresh, still meter on each visit. Enable reduced motion in the browser, reload and
   check that manual start and pause still work. Check the browser console for errors.
5. To check the error state, block the served `timing-meter.<fingerprint>.js` request in
   DevTools and hard-reload. Check the English error message, then remove the blocking
   rule and reload to confirm recovery.
6. For a physical USB check, connect an Oplà running the standard USB sketch, select it
   with **Connect USB**. After the first reply, check that both latency values are shown,
   including when the meter is paused. Start the meter, let it run for a full sweep, and
   cast near the middle of green. Check for **Last cast zone: Green**, a strength from
   0 to 100, and one received throw. The needle should keep moving. Pause and cast again.
   Expect the missing-history message and a count of two. Unplug, reconnect and confirm
   the counter starts at zero and the previous result has cleared.
7. For the physical LED check, open the API-hosted game at <http://localhost:5250/game>
   with the local API running. Start the meter and connect USB during a sweep. Watch all
   five LEDs together as the needle crosses red, yellow and green in both directions.
   Pause, resume, switch to another tab, then return. The LEDs should go dark while
   paused or hidden and follow the needle again when running and visible. Make one cast
   and check that the count increases by one while the colors keep changing. Choose
   **Disconnect USB** while lit and check the LEDs go dark with the cable still attached.

## Backend tests

`dotnet test ByteBait.slnx --configuration Release --no-build` runs the .NET tests.
Database tests require a dedicated PostgreSQL test database through `ConnectionStrings__Default`.
Leave it unset to skip those tests locally. Never point automated tests at a shared Neon database.

## Cast saving tests in Chrome

Start the local API host on port 5250 (or set `BYTEBAIT_TEST_PORT` to its local port).
From the repository root, run the optional browser
suite with Chrome installed. Its Playwright dependency is local to the browser tests.

```powershell
npm install --prefix Frontend/ByteBait.Client.Tests/browser --no-save --package-lock=false playwright-core@1.63.0
node --test Frontend/ByteBait.Client.Tests/browser/cast-saving.test.cjs
```

The tests drive the real game with simulated serial messages and controlled HTTP
responses. They check the JSON, request counts, failure messages, timeouts and delayed
receipts across throws, reconnection and navigation. To include a real insert, point
the API at a dedicated local test database, apply the existing migrations, and set
`BYTEBAIT_TEST_CAST_DATABASE=1` in the test process. The real-API case prints the
returned cast ID for comparison with the row in `casts`.

For a physical check, open the API-hosted game in Chrome and filter the Network tab
to `/api/casts`. Connect the Oplà, wait for the latency measurement and start the meter.
Note the counter and make one cast. Check for one increment, one POST returning 201,
and **Cast saved.** Use the response ID to check the new row and compare its zone,
normalized strength and UTC cast time with the request.
