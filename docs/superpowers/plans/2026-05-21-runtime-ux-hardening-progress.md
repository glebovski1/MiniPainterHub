# MiniPainterHub Runtime UX Hardening Progress

## Goal

Keep the original ASP.NET Core host plus Blazor WebAssembly WebApp architecture, accept that cold WASM Lighthouse can remain slower, and harden repeat visits plus post-startup interaction quality.

## Baseline Carried From Previous Pass

- Cold real-content desktop Lighthouse remained below green: about 0.47-0.48 performance on the latest published no-AOT run.
- Repeat load was the strong path: cached framework transfer was 0 bytes, `/` cached render was about 1149 ms, and `/posts/{id}` cached render was about 1250 ms.
- Scroll and viewer gates were passing after removing feed-card shadow and preserving the static viewer backdrop, stable thumbnails, resize recompute, preview-first fit/fill, and deferred preload behavior.

## Cycle 1 - Runtime UX Gate And Cache Policy Hardening

- Added `e2e/scripts/runtime-ux-check.js`.
  - Publishes and runs the real Release server in the local Lighthouse tooling environment.
  - Seeds a real post and measures `/` plus `/posts/{id}`.
  - Records cold route render, cached route render, route transition, API p95 duration, image decode duration, CLS, post-startup long tasks, service-worker control state, runtime cache contents, offline stale fallback, and 2K viewer open/switch timings.
  - Writes JSON plus Markdown summaries under `e2e/perf-results/runtime-ux/`.
- Added `perf:runtime` and `perf:cached` package scripts, and included them in `perf:all`.
- Tightened the published service worker's runtime API cache allowlist.
  - It now caches only anonymous `/api/posts` list requests with safe paging and false moderation flags.
  - It still caches exact anonymous `/api/posts/{id}` details.
  - It rejects authenticated requests, unknown query shapes, moderation/deleted query shapes, mutations, uploads, service-worker files, and non-origin requests.
- Added safe post-interactive anonymous warmup.
  - Runs only after Blazor content is rendered and the browser is idle.
  - Skips authenticated sessions, Save-Data, 2G-like connections, and hidden tabs.
  - Warms public feed page 2, a likely next post route, and a few thumbnail/preview images without touching private data.
- Hardened server API response headers.
  - API responses now emit `Cache-Control: no-store`, `Pragma: no-cache`, and `X-Content-Type-Options: nosniff`.
  - Static assets keep the existing cache policy.
- Extended `audit:lighthouse` header evidence.
  - Includes `runtimeWarmup.js` and public post API responses.
  - Fails if compressible static assets miss Brotli/gzip, API responses become publicly HTTP-cacheable, API JSON content type is wrong, `.pdb` files are published, or expected static cache policies drift.

## Verification Log

- `node --check e2e/scripts/runtime-ux-check.js` passed.
- `node --check e2e/scripts/lighthouse-audit.js` passed.
- `node --check MiniPainterHub.WebApp/wwwroot/service-worker.published.js` passed.
- `node --check MiniPainterHub.WebApp/wwwroot/JSHelpers/runtimeWarmup.js` passed.
- `dotnet build MiniPainterHub.sln --nologo` passed.
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj --nologo` passed: 186 tests.
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj --nologo` passed: 269 tests.
- `E2E_REUSE_EXISTING_SERVER=false E2E_PORT=5184 npm --prefix e2e run test:smoke` passed: 19 tests.
- `npm --prefix e2e run perf:scroll` passed.
  - `wasm-home`: p95 frame 16.7 ms, max frame 16.8 ms, 0 dropped frames, 0 long tasks.
  - `wasm-post-details`: p95 frame 16.7 ms, max frame 16.8 ms, 0 dropped frames, 0 long tasks.
- `npm --prefix e2e run perf:viewer` passed.
  - 1280x720: open 244 ms, cached switch 135 ms in the targeted run.
  - 1920x1080: open 260 ms, cached switch 183 ms in the targeted run.
  - 2560x1440: open 262 ms, cached switch 132 ms in the targeted run.
- `npm --prefix e2e run perf:runtime` passed.
  - `/`: cold render 645 ms, cached render 317 ms, cached framework transfer 0 bytes, post-startup >100 ms long tasks 0, cached CLS 0.0000.
  - `/posts/3`: cold render 585 ms, cached render 316 ms, cached framework transfer 0 bytes, post-startup >100 ms long tasks 0, cached CLS 0.0000.
  - Route transition `/` to `/posts/3`: 103 ms.
  - 2K viewer: open 188 ms, cached switch 102 ms.
  - Service worker ready/controlled: true; offline public post-list fallback: true.
- `npm --prefix e2e run perf:cached` passed.
  - `/`: cold render 735 ms, cached render 360 ms, cached framework transfer 0 bytes, post-startup >100 ms long tasks 0.
  - `/posts/3`: cold render 587 ms, cached render 307 ms, cached framework transfer 0 bytes, post-startup >100 ms long tasks 0.
- `npm --prefix e2e run audit:lighthouse` passed.
  - Header report confirmed no published `.pdb` files.
  - `/`, `/index.html`, service-worker files, CSS, JS, framework assets, and public API responses had the expected cache/compression/content-type policy.
  - `/`: performance 66 advisory, accessibility 98, best-practices 100, SEO 91, FCP 467 ms, LCP 2619 ms, TBT 465 ms, CLS 0.000496.
  - `/posts/3`: performance 66 advisory, accessibility 100, best-practices 100, SEO 91, FCP 447 ms, LCP 2669 ms, TBT 440 ms, CLS 0.000496.
  - The known Windows Lighthouse temporary-directory cleanup warning occurred after JSON was written and was tolerated by the script.
- `npm --prefix e2e run perf:all` passed.
  - Combined workflow executed viewer, scroll, runtime, cached, and Lighthouse checks.

## Decision

- Keep the changes.
- Runtime and cached-repeat gates pass on the real Release-published Blazor WebAssembly app.
- Lighthouse still validates real rendered Blazor content; cold performance remains yellow and is intentionally advisory in this phase rather than hidden behind a static/SSR bypass.
- No main artwork quality reduction was used.
