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
The game page provides a responsive canvas with an **Animation demo** and an HTML status area.
Status changes from **Loading demo…** to **Demo ready** after initialization. The square remains
still until **Start demo** is selected. **Stop demo** pauses it, and the next start resumes from
the saved position without counting time spent paused. A startup failure shows **Unable to
load demo.**; a control failure shows **Unable to control demo.** Technical details are logged
in the browser console.
The HTML area also shows **Turns: 0** and **Last turn: None**. Each turn at an edge updates
these values through a JavaScript callback into C#. Stop preserves the count; reopening the
page starts a fresh count.
Both pages run independently of the API and database.

The canvas drawing buffer is **320 × 180 pixels**. CSS preserves its 16:9 aspect ratio and
scales the display inside a frame at most 960 CSS pixels wide. Pixelated image rendering
keeps low-resolution graphics sharp. The current technical demo draws a green 12×12 square
moving horizontally at 48 canvas pixels per second, eight pixels from either edge. This is
an animation demonstration, not a running fishing game. There is no hardware input handling yet.

The demo uses the `requestAnimationFrame` timestamp instead of counting frames. Long frame
intervals are capped at 100 ms, and switching tabs resets the time baseline. If reduced motion
is requested when the page initializes, the demo draws a still image, disables both buttons,
and shows **Reduced motion is enabled. Animation is disabled.**

## Demo controls

| State | Status | Start demo | Stop demo |
|-------|--------|------------|-----------|
| Loading | Loading demo… | Disabled | Disabled |
| Ready | Demo ready | Enabled | Disabled |
| Running | Demo running | Disabled | Enabled |
| Stopped | Demo stopped | Enabled | Disabled |
| Reduced motion | Reduced motion is enabled. Animation is disabled. | Disabled | Disabled |
| Error | Unable to load demo. / Unable to control demo. | Disabled | Disabled |

Both buttons are disabled while a control call is pending. They support Tab, Enter, and Space
and have visible focus indicators. Navigation remains available, including after an error.
Reopen the game page to create a fresh demo after a failure.

## Demo turn events

JavaScript reports each actual edge crossing, starting with the right edge after approximately
6.08 seconds of active animation, then the left edge after approximately 12.17 seconds in total.
The counter uses unrounded distance, so repeated drawings at the same edge pixel do not create
duplicate events. Initialization, Start, Stop, and time spent paused or hidden do not add turns.

The callback contract is `OnDemoTurnedAsync(side, turnCount)`: `side` is `"right"` or `"left"`,
and `turnCount` is the positive total for that canvas, starting at 1. Blazor displays the latest
valid, newer total and **Right** or **Left** in a polite live region. It ignores invalid sides,
nonpositive counts, duplicate or older totals, and events for a disposed component. An event
sent just before Stop may still update the counter after Stop; it does not change control status.

Callbacks are asynchronous and never block the animation loop. A callback failure on a live
page is logged once and disables further notifications for that instance, while animation and
controls continue. Pending failures after navigation are ignored. Reopen the game page to create
a fresh receiver; there are no retries or event-history synchronization in this demo.

## Build and test

```powershell
dotnet restore ByteBait.slnx
dotnet build ByteBait.slnx --configuration Release --no-restore
node --check Frontend/ByteBait.Client/wwwroot/js/game.js
node --check Frontend/ByteBait.Client.Tests/game.test.mjs
node --test Frontend/ByteBait.Client.Tests/game.test.mjs
git diff --check
```

The solution builds the frontend and backend together, including in the existing CI workflow.
`git diff --check` covers tracked changes; also check new, untracked source files before delivery,
accepting the project's Windows CRLF line endings. The JavaScript suite currently contains 33 tests.

Backend tests are separate from this standalone frontend verification:

```powershell
dotnet test ByteBait.slnx --configuration Release --no-build
```

The .NET tests cover the backend. Database tests require a dedicated PostgreSQL
test database through `ConnectionStrings__Default`; leave this unset to skip them locally.
Never point automated tests at a shared Neon database.

The JavaScript tests exercise the actual module with controlled canvas, animation clock,
visibility events, and DOM-removal observers. They cover timing, turning points, duplicate
initialization, late initialization, cleanup, startup errors, and reduced motion. They also
check manual start, pause/resume timing, rapid repeated commands, stale callbacks from an
earlier run, and cleanup while stopped. The existing
JavaScript CI job runs both syntax checks and these tests. Callback tests cover exact payloads,
turn ordering and totals, pause/resume, independent receivers, pending calls, rejection handling,
and navigation. These module tests do not run the .NET callback target or replace browser checks.

## Structure and responsibilities

- `Program.cs` starts Blazor in the browser and mounts the root component and page title outlet.
- `App.razor` routes requests to pages and focuses their headings after navigation.
- `Pages/Home.razor` renders the foundation page at `/` and links to the game.
- `Pages/Game.razor` owns `/game`, module initialization, controls, the callback target, and HTML feedback.
- `Pages/Game.razor.css` styles the game page without applying its retro theme to other pages.
- `wwwroot/js/game.js` owns canvas drawing, animation timing, edge events, and browser-resource cleanup.
- `wwwroot/index.html` is the browser entry document and loads the Blazor runtime.

The browser loads the static files, starts the .NET WebAssembly runtime, and renders the
Razor page. The client does not reference the API or data projects.

The canvas has the accessible name **ByteBait game area** and the description **Animation demo**.
Status is readable independently of the canvas and uses a polite live region. Navigation uses
ordinary keyboard-accessible links, and the existing router focuses each page's heading.

## Module lifecycle

After the canvas has rendered, Blazor imports `./js/game.js` once and calls
`initialize(canvas, dotNetReference)` with its element reference and a single
`DotNetObjectReference<Game>` callback target. `initialize` draws a still image and returns whether animation is
allowed; it never starts a loop. JavaScript keeps one demo per canvas in a `WeakMap` and only
writes to the canvas bitmap; it does not change Blazor's HTML. No per-frame calls cross into C#.

Blazor calls `start(canvas)` or `stop(canvas)` when the corresponding button is selected.
`start` returns whether the demo is running; `stop` pauses it without a return value. HTML
status changes only after a successful call. A busy guard rejects overlapping actions even
if clicks were queued before the disabled button state rendered. Return values confirm these
commands. Separately, each edge crossing calls
`dotNetReference.invokeMethodAsync("OnDemoTurnedAsync", side, turnCount)`. The public
`[JSInvokable]` method validates the event and uses `InvokeAsync` to update Blazor's HTML.

Repeated starts preserve one loop and repeated stops are harmless. A generation number prevents
callbacks from a previous run from drawing or replacing the current frame handle after resume.
Stopping retains the removal observer and visibility listener, so navigation still cleans up
a paused demo. A visibility change never starts animation that the user stopped.

A `MutationObserver` watches the stable `#app` host. When navigation removes a canvas, the
module cancels its scheduled frame, disconnects its observer, and removes its visibility
listener, and releases its callback proxy. The frame callback also checks whether the canvas is still connected. Cleanup is
idempotent and includes the still-image version used for reduced motion.

Blazor marks a departing component as disposed, waits for pending initialization and control
calls, and releases the module reference. A `finally` block disposes the C# callback reference
even after partial initialization failure. Late results cannot update the removed page. Startup errors
remain in the status area instead of replacing the app with an unhandled-error banner.

## Browser checks

Run the Release build from the repository root:

```powershell
dotnet run --project Frontend/ByteBait.Client --configuration Release --no-build --launch-profile http
```

Use Chrome at <http://localhost:5173/game>, with DevTools Console open to record unexpected
errors. Record each case as passed, failed, or not tested, including the browser version and
whether the result was observed directly or reported by the tester. An HTTP 200 response or a
passing Node test is not evidence that a browser case passed. Testing a control action followed
immediately by navigation does not prove the exact asynchronous overlap was reached.

| ID | Action | Expected result |
|----|--------|-----------------|
| B01 | Reload, Start, watch two turns, Stop, wait, then Start. | Initially one still square, **Demo ready**, Start enabled, Stop disabled, **0 / None**. Running gives **1 / Right** after about six active seconds, then **2 / Left** about six seconds later. Stop shows **Demo stopped** and preserves position and total; resume continues without counting paused time. A turn already sent before Stop may still complete. |
| B02 | Repeat Start/Stop ten times quickly, then leave it running. | Correct status and enabled buttons, stable speed, one square, and no unexpected console errors. |
| B03 | Use Tab, Shift+Tab, Enter, and Space. Check viewport widths of about 1280 and 390 CSS pixels, then browser zoom at 200%. | Links activate with Enter; buttons with Enter or Space. Focus is visible, disabled buttons cannot be activated, text and controls remain usable, and there is no horizontal page scrolling. Use Device Toolbar's Responsive mode for exact viewport widths. |
| B04 | Switch to another tab for ten seconds while running; repeat after Stop. | No large jump or extra turns for hidden time. A stopped demo remains stopped. |
| B05 | Navigate home and back while running, while stopped, immediately after Start, and near an edge crossing; repeat ten navigation cycles in total. | Each new game page has a fresh still image, **Demo ready**, and **0 / None**. No errors from the removed component appear on the home page or next game page. |
| B06 | Leave the page during a deliberately slowed first module import, following the procedure below. | Navigation completes without an unhandled error; the next visit initializes normally. If **Loading demo…** was not reached before leaving, record this attempt as inconclusive. |
| B07 | Emulate reduced motion before reloading, following the procedure below. | Still square, **Reduced motion is enabled. Animation is disabled.**, both buttons disabled, and **0 / None**. |
| B08 | Block the module, reload, then unblock and reload, following the procedure below. | **Unable to load demo.** and disabled buttons during failure, with a technical console error. Navigation remains usable; normal initialization returns after unblocking. |

### Slow initialization (B06)

Reload the home page `/` with normal networking first, so Blazor is ready and the document has
not imported the game module. In Network, select **Disable cache** and a slow **3G** preset
(called **Slow 3G** in some versions). Select **Open game**, then **Back to home** while
**Loading demo…** is visible. Restore the original network/cache settings before the next visit.
Start from a freshly reloaded home page for another attempt: disabling HTTP cache alone does
not clear a module already imported by the current document. See Chrome's
[network controls](https://developer.chrome.com/docs/devtools/network/reference/).

### Reduced motion (B07)

In DevTools, use `Ctrl+Shift+P` to open **Show Rendering**. Set **Emulate CSS media feature
prefers-reduced-motion** to `prefers-reduced-motion: reduce`, then reload `/game`. The demo
samples this preference at initialization. Restore the original emulation setting and reload
afterward. See Chrome's [media-feature emulation](https://developer.chrome.com/docs/devtools/rendering/emulate-css).

### Import failure (B08)

In DevTools Network, disable the test rule if an earlier attempt did not match. Clear any
request filter, enter **`game`**, and reload `/game` normally. The served import map resolves
`./js/game.js` to a fingerprinted name such as `./js/game.4dfkiaai16.js`; the fingerprint changes
when the module changes. Filtering for `game.js` or blocking only that literal filename misses
the fingerprinted request. See [Blazor static asset fingerprinting](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0).

Right-click the actual `game.<fingerprint>.js` module request and choose **Block request**
(**Block request URL** in older versions), letting Chrome
create the URL rule. Keep DevTools open and hard-reload with `Ctrl+Shift+R`. Confirm the request
shows `(blocked:devtools)` and the page shows **Unable to load demo.** with disabled controls.
A rule showing **0 affected** without a blocked request is inconclusive, not a verified import
failure. Remove only the test rule and hard-reload to verify recovery. See Chrome's
[request conditions](https://developer.chrome.com/docs/devtools/request-conditions).

### Accessibility evidence and cleanup

Inspect the status element's `role="status"` and `aria-live="polite"`, the turn region's
`aria-live="polite"` and `aria-atomic="true"`, and the canvas name and description. Source/DOM
inspection verifies the attributes; it does not verify actual screen-reader announcements.
Record screen-reader testing separately if performed.

Restore previous throttling, cache, motion emulation, viewport emulation, and zoom settings.
Remove test blocking rules, check a final normal load, and stop only the server started for
this verification. Never stop another developer's server to free the port.

Local verification is complete when the automatic checks and B01–B08 pass and any limitations
are recorded. Issue #7 additionally requires a reviewed PR to `dev`, green GitHub CI, and a
working Test-ByteBait deployment. Local verification alone does not close the issue.

### Local verification record — 2026-11-17

- Restore, Release build, JavaScript syntax checks, whitespace checks, and local asset delivery
  passed. The solution built with **0 warnings and 0 errors**; all **33 JavaScript tests passed**.
- Browser cases **B01–B08 passed**, reported by the local tester. Screenshots also document the
  blocked fingerprinted module, the controlled error state, and successful loading, animation,
  and turn callbacks after unblocking.
- Actual screen-reader announcements and precise overlap with a pending control call were
  not separately verified. The browser version was not recorded. GitHub CI, backend/database
  tests, hardware integration, and deployment were outside this local verification.

Hosting the client from the API belongs to #9; deployment belongs to #13.
