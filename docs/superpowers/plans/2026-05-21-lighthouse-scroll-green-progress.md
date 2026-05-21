# MiniPainterHub Lighthouse And Scroll Progress

## Goal

Improve end-user performance without lowering artwork quality. Lighthouse green is a release gate, but scroll smoothness and viewer responsiveness are also gates.

## Baseline Notes

- Branch created: `codex/hybrid-performance-green`.
- `dotnet build MiniPainterHub.sln`: passed.
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj --filter "RichImageViewerTests|PostDetailsViewerTests"`: passed, 21 tests.
- `npm --prefix e2e run perf:viewer`: blocked by reused manual server returning HTTP 405 for `/api/test-support/reset`.
- `npm --prefix e2e run audit:lighthouse`: blocked when run in parallel with Playwright because both touched `e2e/test-results`.

## Cycle 1 - Rejected Static Renderer Attempt

- Fix e2e tooling so performance scripts use an isolated Playwright port and Lighthouse writes outside Playwright's cleaned output folder.
- Add a dedicated scroll performance budget and script.
- Added anonymous public HTML for `/` and `/posts/{id}` that did not eagerly load Blazor WebAssembly.
- Rejected this route as a workaround because MiniPainterHub must stay on the original ASP.NET Core API + Blazor WebAssembly app architecture.
- `npm --prefix e2e run perf:scroll`: passed.
  - `public-home`: p95 16.8 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
  - `public-post-details`: p95 16.8 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
- `npm --prefix e2e run perf:viewer`: passed after correcting the cached-switch measurement and warming preview-only next frames.
  - 2K viewer open 1079 ms, warm switch 366 ms, cached switch 374 ms, screenshot 488 ms.
- `npm --prefix e2e run audit:lighthouse`: passed budget.
  - `/`: performance 100, accessibility 100, best-practices 100, SEO 100.
  - `/posts/28`: performance 100, accessibility 94, best-practices 100, SEO 100.
  - Lighthouse reported Windows temp cleanup warnings after JSON output; the script parsed the reports and passed the score gate.

## Cycle 2 - Architecture Correction

- Removed the static/SSR-like public renderer and restored normal Blazor WebAssembly fallback routing.
- Kept viewer-specific performance fixes that improve the real app instead of bypassing it.
- Reworked Lighthouse to start a deterministic Release-built local server, seed a real post through the API, and audit the normal WASM routes `/` and `/posts/{createdId}`.
- Updated scroll checks to measure real WASM home/post details routes instead of static public HTML selectors.

## Cycle 3 - Real WASM Performance Hardening

- Added server response compression for WASM/static assets.
- Enabled Release-only WASM AOT, IL stripping, and invariant globalization.
- Removed the broad WebAssembly authentication package from the client and kept only the authorization package the app actually uses.
- Lazy-loaded SignalR-related assemblies only for `/messages`.
- Split conversation summary reads from realtime chat startup so global authenticated UI and public profiles do not force SignalR assemblies onto normal page loads.
- Removed scroll-costly `backdrop-filter` usage from the normal app chrome.
- Kept the viewer quality path: fit/fill uses preview images, actual-size uses full images, and 2K viewer checks keep screenshots and dimensions in budget.

## Cycle 4 - Current Verification

- `dotnet build MiniPainterHub.sln`: passed.
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj`: passed, 182 tests.
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`: passed, 266 tests.
- `npm --prefix e2e run test:smoke`: passed, 19 tests.
- `npm --prefix e2e run perf:viewer`: passed.
  - `1280x720`: open 132 ms, warm switch 79 ms, cached switch 77 ms.
  - `1920x1080`: open 116 ms, warm switch 61 ms, cached switch 73 ms.
  - `2560x1440`: open 114 ms, warm switch 129 ms, cached switch 73 ms.
- `npm --prefix e2e run perf:scroll`: passed.
  - `wasm-home`: p95 16.8 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
  - `wasm-post-details`: p95 16.7 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
- `npm --prefix e2e run perf:all`: passed.
  - Viewer rerun: 2K open 127 ms, warm switch 81 ms, cached switch 64 ms.
  - Scroll rerun: both WASM routes stayed at 16.7-16.8 ms p95, 0 dropped frames, 0 long tasks.
  - Lighthouse Release WASM audit: `/` scored 100 performance, 93 accessibility, 96 best-practices, 90 SEO.
  - Lighthouse Release WASM audit: generated `/posts/3` scored 100 performance, 93 accessibility, 96 best-practices, 90 SEO.
  - Windows Lighthouse temp cleanup warning was tolerated only after JSON reports existed and score gates passed.

## Cycle 5 - Manual Lighthouse Discrepancy Check

- Reproduced the user's yellow/non-green result against the Debug `dotnet run` host on `http://127.0.0.1:5176`.
  - Mobile/default `/`: performance 36, accessibility 98, best-practices 100, SEO 91.
  - Desktop preset `/`: performance 50, accessibility 99, best-practices 100, SEO 91.
  - Debug transfer size was about 10 MB and bootup was dominated by unoptimized Blazor WebAssembly startup.
- Checked the Release-published WASM host on `http://127.0.0.1:5179`.
  - Mobile/default `/`: performance 98, accessibility 93, best-practices 96, SEO 90.
  - Mobile/default `/posts/30`: performance 97, accessibility 93, best-practices 96, SEO 90.
- Switched `http://127.0.0.1:5176` to the Release-published host and reran mobile/default Lighthouse.
  - Mobile/default `/`: performance 97, accessibility 93, best-practices 96, SEO 90.
  - Mobile/default `/posts/30`: performance 98, accessibility 93, best-practices 96, SEO 90.
- Conclusion: Chrome Lighthouse should be run against a Release-published host, not the Debug development host. The previous green result was valid for Release WASM, but the manual-local guidance must be clearer because `5176` Debug is expected to score poorly.

## Cycle 6 - Published Startup Regression

- User saw only the loading wheel on the Release-published local host.
- Browser console showed Blazor never started because the Mono WASM runtime crashed before app startup:
  - `MONO interpreter: NIY encountered in method get_BaseDirectory`
  - `MONO_WASM: mono_wasm_load_runtime () failed ExitStatus`
- Direct cause: Release AOT publish crashes this app/runtime combination before rendering. AOT with and without `WasmStripILAfterAOT`, and AOT with globalization restored, all failed.
- Release publish without AOT starts and renders correctly after a clean Release build.
- Removed Release AOT properties from `MiniPainterHub.WebApp.csproj` for now so normal `dotnet publish -c Release` produces a working app.
- Important correction: the previous Lighthouse green was a false positive because Lighthouse scored the static loading screen. Future Lighthouse gates must include a rendered-app assertion, not only category scores.
- Current running `http://127.0.0.1:5176/` is a Release-published no-AOT host and renders `Latest posts`.
- Known tradeoff: no-AOT Release renders correctly but mobile/default Lighthouse performance was 45 on the home page. The next performance cycle must recover startup score without reintroducing a non-rendering AOT build.

## Cycle 7 - Lighthouse Reevaluation With Render Assertion

- Re-audited the current `http://127.0.0.1:5176/` Release-published no-AOT host.
- Before each Lighthouse run, a browser render check confirmed the app reached real Blazor content with no startup console errors.
- Results:
  - `/` mobile/default: performance 39, accessibility 98, best-practices 100, SEO 91.
  - `/` desktop: performance 61, accessibility 99, best-practices 100, SEO 91.
  - `/posts/30` mobile/default: performance 44, accessibility 100, best-practices 100, SEO 91.
  - `/posts/30` desktop: performance 62, accessibility 100, best-practices 100, SEO 91.
- Key metrics:
  - Mobile TBT remains high: 2.31-3.03 seconds.
  - Mobile LCP remains high: about 15.8-15.9 seconds.
- Desktop TBT is about 510-530 ms and LCP about 2.9-3.0 seconds.
- Conclusion: the working published WASM app is not Lighthouse green yet. The next fix cycle should make the Lighthouse gate fail unless the app actually renders, then reduce startup JS/WASM cost without reintroducing the broken AOT path.

## Cycle 8 - Hardened Real-Content Lighthouse Gate

- Updated `e2e/scripts/lighthouse-audit.js` so it refuses to score a route until the real Blazor route content is visible.
  - `/` must render the home feed text, not the loading shell.
  - `/posts/{id}` must render `data-testid="post-title"` and `data-testid="post-details-gallery"`.
- Removed the category-only Lighthouse invocation so Lighthouse 13 diagnostics and insights are available for the decision log.
- Added metric extraction for FCP, LCP, TBT, Speed Index, CLS, LCP element details, transfer size, and largest assets.
- Render validation caught the previous AOT spinner failure and also caught an attempted `System.Text.RegularExpressions` lazy-load breakage before Lighthouse scoring.
- Decision: keep. This prevents false green scores.

## Cycle 9 - API JSON Payload Trim

- Removed client usage of `System.Net.Http.Json` convenience APIs and replaced request bodies with `ApiClient.CreateJsonContent(...)`.
- Result:
  - `System.Net.Http.Json.wasm` is no longer in the initial boot manifest.
  - Initial assembly count dropped by one.
  - Lighthouse movement was small and noisy, but this is a real startup payload reduction.
- Verification:
  - `dotnet test MiniPainterHub.WebApp.Tests\MiniPainterHub.WebApp.Tests.csproj --filter "ApiClientTests|CommentFormTests|PostDetailsViewerTests|RichImageViewerTests"` passed, 32 tests.
- Decision: keep.

## Cycle 10 - Home Feed LCP Candidate Priority

- Lighthouse identified the home LCP element as a `Post image`, specifically the second card image in the desktop two-column first row.
- Updated `CardList` so the first rendered row gets eager/high-priority image loading:
  - first two cards: `loading="eager"` and `fetchpriority="high"`
  - later cards: `loading="lazy"` and `fetchpriority="auto"`
- Added a bUnit test to lock this behavior.
- Result:
  - Lighthouse LCP discovery no longer reports the image as lazy/auto.
  - `requestDiscoverable` remains false because the real image URL is API-rendered after Blazor starts, which pure WASM cannot expose in the initial HTML without prerender/hybrid rendering.
- Verification:
  - `dotnet test MiniPainterHub.WebApp.Tests\MiniPainterHub.WebApp.Tests.csproj --filter "CardListVisibilityTests|PostCardTests|ApiClientTests"` passed, 19 tests.
- Decision: keep.

## Cycle 11 - Font And Icon Blocking Path

- Replaced remote Google Fonts with local `@font-face` declarations in `wwwroot/css/fonts.css`.
- Loaded `fonts.css` via preload/onload plus `font-display: swap`.
- Replaced the remote Bootstrap Icons stylesheet with a local subset.
- Deferred `JSHelpers/domHelpers.js`.
- Result:
  - Removes third-party font/icon blocking dependencies and makes the route more deterministic.
  - FCP became slightly worse in some desktop runs, while TBT improved slightly; net Lighthouse score stayed around 0.65-0.66.
- Decision: keep for determinism and best-practice local assets, but it is not the path to green.

## Cycle 12 - Additional WASM Lazy/Payload Attempts

- Tried lazy-loading `System.Text.RegularExpressions.wasm`.
  - Result: rejected. The Blazor router needs regular expressions at startup through route constraints, and the render gate caught the failure.
- Tried lazy-loading `System.Security.Cryptography.wasm`.
  - Result: rejected. Debug smoke exposed a startup failure because `HttpClientHandler` needs crypto during DI startup.
  - Lesson: even when a Release route render check passes, core runtime dependencies can still be invalid lazy-load candidates.
- Tried removing client performance configuration parsing to drop configuration assemblies.
  - Result: rejected. The boot manifest did not drop the configuration assemblies, and scores did not improve.
- Added `BlazorWebAssemblyPreserveCollationData=false` for Release.
  - Result: kept. The app does not use culture-sensitive invariant collation APIs on the current path, and Microsoft documents this as a supported WASM payload switch.
- Temporarily tried disabling Blazor timezone support.
  - Result: rejected. It only moved the post route from about 0.65 to 0.67 and risks local-time display regressions because the app uses `ToLocalTime()` in cards, post details, comments, messages, profiles, and admin screens.

## Cycle 13 - Current Real-WASM Status

- Latest real-content desktop Lighthouse runs remain yellow:
  - `/`: about 0.67 performance, accessibility 0.98, best-practices 1.00, SEO 0.91.
  - `/posts/{createdId}`: about 0.67 performance, accessibility 1.00, best-practices 1.00, SEO 0.91.
- Largest transfer offenders remain runtime/bootstrap assets:
  - `System.Private.CoreLib.wasm`
  - `dotnet.native.wasm`
  - `MiniPainterHub.WebApp.wasm`
  - `System.Text.Json.wasm`
  - `Microsoft.AspNetCore.Components.wasm`
  - `System.Net.Http.wasm`
- Scroll performance is green:
  - `wasm-home`: p95 16.8 ms, 0 dropped frames, 0 long tasks.
  - `wasm-post-details`: p95 16.8 ms, 0 dropped frames, 0 long tasks.
- Viewer performance is green:
  - 2K open around 120-150 ms in the latest runs.
  - image switching under budget.
  - screenshot capture succeeds.
  - static backdrop and thumbnail sizing stay stable.
- Conclusion: supported no-AOT pure Blazor WebAssembly tuning improved correctness and interaction smoothness, but does not honestly reach Lighthouse 0.90 on real rendered content. The remaining gap is dominated by cold-start Blazor/.NET runtime cost and API-discovered LCP content. Reaching all-green likely requires a separate architecture decision: prerender/hybrid rendering for public routes or another way to put meaningful route content and LCP hints in the initial HTML.

## Cycle 14 - Final Verification For This Pass

- `dotnet build MiniPainterHub.sln --nologo`: passed.
- `dotnet test MiniPainterHub.WebApp.Tests\MiniPainterHub.WebApp.Tests.csproj --nologo`: passed, 186 tests.
- `dotnet test MiniPainterHub.Server.Tests\MiniPainterHub.Server.Tests.csproj --nologo`: passed, 269 tests.
- `E2E_REUSE_EXISTING_SERVER=false E2E_PORT=5181 npm --prefix e2e run test:smoke`: passed, 19 tests.
- `npm --prefix e2e run perf:scroll`: passed.
  - `wasm-home`: p95 16.7 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
  - `wasm-post-details`: p95 16.7 ms, max 16.8 ms, 0 dropped frames, 0 long tasks.
- `npm --prefix e2e run perf:viewer`: passed.
  - 1280x720 open 113 ms.
  - 1920x1080 open 121 ms.
  - 2560x1440 open 137 ms.
  - cached switches stayed under 100 ms.
- `npm --prefix e2e run audit:lighthouse`: rendered real content, wrote summary, and failed only the 0.90 performance budget.
  - `/`: performance 0.67, FCP 446 ms, LCP 2607 ms, TBT 442 ms, SI 628 ms, CLS 0.000072, transfer 2,026,687 bytes.
  - `/posts/3`: performance 0.67, FCP 447 ms, LCP 2631 ms, TBT 442 ms, SI 648 ms, CLS 0.000072, transfer 2,024,874 bytes.

## Cycle 15 - Repeat-Visit And Runtime UX Hardening

- Added Release/static-host hardening:
  - Release server and WebApp builds no longer publish debug symbols intentionally.
  - Production blocks `.pdb` static requests.
  - Static header report is now emitted by `audit:lighthouse`.
  - `index.html`, `service-worker.js`, `service-worker-assets.js`, and `blazor.boot.json` are `no-cache`.
  - CSS/JS use one-day public cache, images/framework assets use one-week public cache, and fingerprinted names are eligible for immutable caching.
- Added a Blazor WebAssembly service-worker path:
  - Development worker installs without caching.
  - Published worker cache-first caches app-shell assets by generated asset-manifest version.
  - Runtime API caching is limited to anonymous public post GETs, network-first with a five-minute fallback.
  - Authenticated requests, mutation requests, moderation query shapes, uploads, and service-worker files are not cached.
  - Automatic registration happens only when the tab is hidden; performance tooling explicitly registers it for repeat-load measurement.
- Replaced the default spinner with a static branded feed skeleton that reserves the above-the-fold layout without using route text or test selectors that could satisfy the render gate.
- Removed global Bootstrap JS from startup and replaced the toast helper with a tiny local DOM implementation.
- Scroll hardening:
  - Removed `content-visibility: auto` from feed cards because the current feed page is small and deferred materialization produced 33 ms scroll frames.
  - Removed the feed card shadow; this was the concrete paint cost that made `perf:scroll` fail.
- Latest published audit:
  - Static headers passed, published static `.pdb` count is 0, and cached repeat-load framework transfer is 0 bytes.
  - Repeat load: `/` cached render 1149 ms, `/posts/3` cached render 1250 ms, no post-startup long tasks.
  - Cold Lighthouse remains yellow and is noisy/worse in this run: `/` performance 0.48, `/posts/3` performance 0.47, dominated by `blazor.webassembly.js` startup script evaluation.
  - Decision: keep the repeat-visit/runtime hardening, but do not claim cold Lighthouse improvement. Green cold Lighthouse still requires an architecture decision beyond pure no-AOT WASM.
- Verification:
  - `node --check e2e\scripts\lighthouse-audit.js`: passed.
  - `node --check MiniPainterHub.WebApp\wwwroot\service-worker.published.js`: passed.
  - `dotnet build MiniPainterHub.sln --nologo`: passed.
  - `dotnet test MiniPainterHub.WebApp.Tests\MiniPainterHub.WebApp.Tests.csproj --nologo`: passed, 186 tests.
  - `dotnet test MiniPainterHub.Server.Tests\MiniPainterHub.Server.Tests.csproj --nologo`: passed, 269 tests.
  - First smoke attempt timed out at 244 seconds; rerun with `E2E_REUSE_EXISTING_SERVER=false E2E_PORT=5182 npm --prefix e2e run test:smoke` passed, 19 tests.
  - `npm --prefix e2e run perf:scroll`: passed after removing feed-card shadow.
  - `npm --prefix e2e run perf:viewer`: passed.
  - `npm --prefix e2e run audit:lighthouse`: rendered real content and passed header/repeat checks, failed only the 0.90 cold performance budget.
  - Initial boot manifest after rejected crypto lazy-load: 48 initial assemblies, 15 lazy assemblies.
