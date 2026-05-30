# Blazor WASM Performance Log

Purpose: capture the durable MiniPainterHub performance lessons from the May 2026 WASM hardening work.

When to read: before changing Blazor startup, post feed rendering, post details media, rich viewer behavior, service worker caching, Lighthouse gates, or production hosting headers.

Update triggers: new Lighthouse/runtime measurements, image pipeline changes, service worker policy changes, Blazor render-path changes, deployment/header changes, or any decision to introduce prerendering/hybrid rendering.

Related notes:
- [Architecture](<ARCHITECTURE.md>)
- [Deployment](<DEPLOYMENT.md>)
- [Workflow playbook](<../30 Process/WORKFLOW_PLAYBOOK.md>)
- [UI quality playbook](<../30 Process/UI_QUALITY_PLAYBOOK.md>)

## Executive Summary

MiniPainterHub should stay honest about what improved:

- The app now feels better after startup because scroll, viewer, cached visits, and route/runtime checks were hardened.
- Cold Lighthouse Performance is still not green for public post routes because the real app is a pure Blazor WebAssembly app and the main content is not available until Blazor boots.
- The right release bar for the current architecture is runtime user experience plus repeat-visit speed, not pretending that pure WASM can score like static HTML on first visit.
- If cold public Lighthouse green becomes business-critical, the next decision is architectural: prerender or hybrid-render public anonymous routes while preserving the WASM app for rich interactions.

## What Was Wrong

The original performance problem was not one single bug. It was several costs stacking together:

- **Cold WASM startup cost:** the browser must download, compile, and execute Blazor runtime and app assemblies before route content exists.
- **LCP discovery problem:** the post image can be marked eager and high priority, but Lighthouse still reports it is not discoverable in the initial HTML because Blazor renders it after startup.
- **Large LCP image pressure:** production `/posts/10` currently transfers a `1.97 MB` JPG as the LCP image.
- **Viewer paint cost:** the rich viewer used an artwork-backed blurred full-shell backdrop, which created expensive large-area paint/compositing work.
- **Thumbnail rail distortion:** desktop thumbnail sizing could stretch small thumbnails into tall strips, especially in large windows.
- **Resize invalidation bug:** viewer fit state could stay stale after resizing from mobile-like dimensions back to a large viewport.
- **Image preload pressure:** adjacent viewer images could be preloaded too eagerly and at too high a quality level for ordinary fit/fill viewing.
- **Duplicate comment/render work:** comments and related viewer surfaces could do work even when not visible or not active.
- **Missing measurement guardrails:** earlier Lighthouse checks could accidentally score the loading shell instead of real rendered Blazor content.
- **Production evidence gaps:** caching, compression, service-worker behavior, `.pdb` publish output, and runtime long-task behavior were not all gated together.

## What Changed

Architecture was corrected back to the original product shape:

- `MiniPainterHub.Server` remains the ASP.NET Core API/static host.
- `MiniPainterHub.WebApp` remains the Blazor WebAssembly app.
- The static/SSR-like bypass was removed.
- AOT was not re-enabled because prior Release AOT attempts caused runtime loading failures.

Viewer and post-detail runtime improvements:

- Replaced the expensive live-image blurred viewer backdrop with a cheap static backdrop.
- Fixed desktop thumbnail rail sizing with stable bounded thumbnail frames.
- Recomputed viewer fit state after meaningful resize events.
- Kept preview-first image selection for fit/fill modes.
- Reserved full image quality for actual-size or explicit zoom behavior.
- Deferred adjacent image preload work and kept ordinary preloads to preview variants.
- Reduced duplicated comment reload/render work around viewer tabs.

Feed and startup improvements:

- Added stable card/media dimensions to reduce layout shift and scroll jank.
- Removed or reduced expensive visual paint effects in scrolling surfaces.
- Localized font/icon dependencies instead of depending on heavier global blocking paths.
- Added a lightweight startup shell/skeleton so boot feels intentional instead of spinner-only.
- Deferred warmup work until after the app is interactive and idle.

Hosting, caching, and service worker hardening:

- Production blocks `.pdb` static serving.
- Publish output is validated to contain no `.pdb` files.
- Framework/static assets use long-lived cache headers where safe.
- `index.html`, boot manifests, and service-worker files avoid stale app-shell traps.
- API responses use no-store or controlled cache behavior.
- Service worker caching is restricted to safe anonymous public GET data only.
- Authenticated, mutation, upload, moderation, profile-sensitive, and unknown query requests are not cached as stale responses.

Tooling and gates added:

- Lighthouse audit now verifies route-specific rendered content before accepting scores.
- Runtime UX checks measure cold load, cached reload, route transition, long tasks, API timings, image decode behavior, scroll jank, and viewer timing.
- Scroll and viewer performance scripts cover desktop and 2K-like viewports.
- Header/cache/service-worker checks are part of the performance evidence.
- Progress logs are kept under `docs/superpowers/plans/`.

## Current Measured State

The most important current interpretation:

- Real user feel is better because scroll, viewer, and cached paths are fast.
- Cold public Lighthouse is still poor because pure WASM startup and LCP discovery remain structural costs.

Recent local gates from the final release preparation:

| Area | Result |
| --- | --- |
| Cached framework transfer | `0` bytes after first visit |
| Runtime cached route render | roughly `300 ms` range in local checks |
| Post-startup long tasks | `0` tasks over `100 ms` in latest local runtime checks |
| Scroll | p95 frame time around one frame, `0` dropped frames in latest gates |
| Viewer | open under `250 ms` locally in latest 2K runtime check; cached switch under budget |
| Publish output | `0` `.pdb` files |
| CI hard gate | server coverage passed at `83.89%` against `80%` |

Production `/posts/10` Lighthouse on 2026-05-21, before confirming the latest deploy completed:

| Mode | Performance | Accessibility | Best Practices | SEO |
| --- | ---: | ---: | ---: | ---: |
| Desktop | `52` | `100` | `100` | `91` |
| Mobile | `28-29` | `100` | `100` | `91` |

Production `/posts/10` key metrics:

| Metric | Desktop | Mobile |
| --- | ---: | ---: |
| FCP | `0.52 s` | `5.2-6.4 s` |
| LCP | `4.57 s` | about `26 s` |
| TBT | `609 ms` | `3.0-3.2 s` |
| Speed Index | `1.44 s` | `7.2-8.1 s` |
| CLS | near zero | near zero |
| TTFB | `50-63 ms` | `50-54 ms` |
| Total transfer | `4.47 MB` | `4.47 MB` |

Production LCP detail:

- LCP element: main post image, `Death Wing Strike Master`.
- It is already eager and high priority.
- Lighthouse says it is not discoverable in the initial document.
- Largest transfer: `10_0_20250928_163318.jpg`, about `1.97 MB`.

## Why It Feels Better Now

The improvement is mainly from removing runtime friction after Blazor is loaded:

- Scroll does less expensive paint work.
- Layout dimensions are more stable, so the browser does less reflow and visual correction.
- The viewer no longer maintains a huge blurred live-image layer.
- Viewer image switching avoids unnecessary full-quality preload pressure.
- Comment/viewer work is more lazy and more active-tab oriented.
- Repeat visits benefit from framework/app-shell caching.
- Warmup happens after idle, so it does not compete with first render.
- Performance scripts now catch regressions that a one-time Lighthouse score misses.

This is why the product can feel substantially better even while cold Lighthouse Performance remains yellow/red.

## Why Lighthouse Is Still Low

Lighthouse cold-load testing is intentionally harsh for Blazor WASM:

- It simulates a cold first visit.
- It throttles CPU and network, especially in mobile mode.
- It rewards content that is present in the first HTML document.
- Pure WASM content is rendered after boot, so the LCP image is discovered late.
- Main-thread boot time becomes Total Blocking Time.
- Large images amplify the delay once the route finally renders.

This does not mean the app is bad. It means the current architecture has a cold-start tradeoff that must be measured separately from repeat/runtime UX.

## How To Make It Even Better

Highest ROI next steps:

1. **Fix the image pipeline without lowering visible quality**
   - Generate responsive variants for post images.
   - Use `srcset` and `sizes`.
   - Serve a display-sized high-quality WebP/AVIF for above-the-fold post detail images.
   - Keep original/max quality for viewer actual-size and download-like experiences.
   - Add upload-time or background processing telemetry for variant sizes.

2. **Make LCP discoverable earlier if cold Lighthouse matters**
   - Pure WASM cannot put route-specific image HTML into the initial document.
   - For green public-route Lighthouse, evaluate prerendered or hybrid public anonymous post/feed routes.
   - Keep the WASM app for rich authenticated interactions and viewer behavior.

3. **Continue reducing startup work**
   - Lazy-load route-only assemblies where real bundle evidence supports it.
   - Keep SignalR/realtime, moderation, upload, and private/profile-heavy work off anonymous hot paths.
   - Avoid doing auth/profile/comment/viewer setup before visible route content needs it.

4. **Strengthen repeat-visit behavior**
   - Keep service worker cache-first for versioned app assets.
   - Use network-first or stale-while-revalidate only for safe anonymous public GET data.
   - Never cache private/authenticated data as stale content.

5. **Use runtime gates as the main regression shield**
   - Lighthouse remains useful, but it is not enough.
   - Keep measuring cached reload, route transition, scroll FPS/jank, long tasks, API timing, image decode, and viewer timing.

## Blazor Performance Lessons

Blazor WebAssembly has a different performance profile than static HTML, SSR, or a small JavaScript island:

- First load is expensive because the runtime and assemblies must arrive before the app can render real route content.
- Repeat visits can be very good if framework assets are fingerprinted, compressed, cached, and service-worker controlled correctly.
- Runtime UX can be excellent if the UI avoids heavy paints, duplicate render work, and unnecessary startup services.
- Lighthouse Performance is useful, but cold Lighthouse alone is a poor proxy for a logged-in, repeat-use WASM product.

Practical Blazor tips:

- Treat first render, repeat load, and post-interactive runtime as separate budgets.
- Prefer deterministic dimensions for cards, media, grids, thumbnails, and viewer stages.
- Avoid large blurred backdrops, heavy shadows, animated filters, and full-page compositing during scroll.
- Use `ShouldRender` only where render churn is proven; do not scatter it everywhere.
- Split large components when it reduces meaningful rerender scope.
- Defer non-visible data and services until the user enters that feature.
- Lazy-load heavy route-only assemblies only when bundle analysis proves a win.
- Use JavaScript interop for browser-specific measurement or viewer mechanics, but keep it idle/deferred when possible.
- Use `loading="eager"` and `fetchpriority="high"` only for the true above-the-fold LCP candidate.
- Keep below-the-fold images lazy.
- Always reserve image dimensions or aspect ratio to protect CLS.
- Use responsive image variants instead of sending original uploads into small display slots.
- Keep production Brotli/gzip and cache headers observable through tests, not tribal knowledge.
- Keep service worker caching privacy-safe: public anonymous GET only unless there is a strong reason and explicit invalidation model.
- Do not enable AOT by default. Measure bundle-size, cold-load, and runtime-smoothness deltas first.

## Decision Guidance

Use this rule of thumb:

- If users complain about scroll, viewer, route transitions, or repeat visits: optimize the current WASM app.
- If stakeholders require green cold Lighthouse on public anonymous pages: plan hybrid/prerendered public routes.
- If main artwork looks worse: reject the optimization unless it only affects thumbnails/previews and preserves the viewer/original quality path.
- If an optimization hides real Blazor content from Lighthouse: reject it as a workaround.

## Open Follow-Ups

- Confirm production after the pending deploy completes and rerun `/posts/10` Lighthouse.
- Add responsive image variants for post detail LCP images.
- Add a production runtime check for repeat visits, not only local checks.
- Consider a focused architecture decision record for hybrid/prerendered public routes if cold Lighthouse green remains a release requirement.
- Track GitHub Actions Node 20 deprecation warnings before the runner default changes.
