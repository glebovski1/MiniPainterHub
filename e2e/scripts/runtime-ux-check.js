const { spawn, spawnSync } = require("child_process");
const fs = require("fs");
const path = require("path");
const { chromium } = require("@playwright/test");
const { VIEWER_IMAGE_PATHS, createRichViewerPost } = require("../tests/helpers/viewer-scenario");

const e2eRoot = path.resolve(__dirname, "..");
const repoRoot = path.resolve(e2eRoot, "..");
const budget = require("../lighthouse/budget.json").runtime || {};
const outputDir = process.env.RUNTIME_UX_OUTPUT_DIR
  || path.join(e2eRoot, "perf-results", "runtime-ux");
const publishDir = process.env.RUNTIME_UX_PUBLISH_DIR
  || path.join(outputDir, "server-publish");
const port = process.env.RUNTIME_UX_PORT || "5183";
const baseUrl = (process.env.RUNTIME_UX_BASE_URL || `http://127.0.0.1:${port}`).replace(/\/$/, "");
const ownsServer = !process.env.RUNTIME_UX_BASE_URL;
const serverEnvironment = process.env.RUNTIME_UX_SERVER_ENVIRONMENT || "Lighthouse";
const resetToken = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const localDbInstance = process.env.E2E_LOCALDB_INSTANCE || "MiniPainterHubRuntimeUx";
const defaultDbName = `MiniPainterHub_RuntimeUx_${Date.now()}_${process.pid}`;
const connectionString = process.env.E2E_CONNECTION_STRING
  || `Server=(localdb)\\${localDbInstance};Database=${defaultDbName};Trusted_Connection=True;MultipleActiveResultSets=true`;
const cachedOnly = process.argv.includes("--cached-only");

fs.mkdirSync(outputDir, { recursive: true });

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function runChecked(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: e2eRoot,
    stdio: "inherit",
    shell: false,
    ...options
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(" ")} failed with exit ${result.status}.`);
  }
}

function publishServer() {
  fs.rmSync(publishDir, { recursive: true, force: true });
  fs.mkdirSync(publishDir, { recursive: true });
  runChecked("dotnet", [
    "publish",
    "../MiniPainterHub.Server/MiniPainterHub.Server.csproj",
    "--configuration",
    "Release",
    "--output",
    publishDir,
    "--nologo"
  ]);
}

function ensureWindowsLocalDbInstance() {
  if (process.platform !== "win32") {
    return;
  }

  const localDbInstancePowerShell = localDbInstance.replace(/'/g, "''");
  runChecked("powershell.exe", [
    "-NoLogo",
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-Command",
    [
      "$localDbInstances = sqllocaldb info",
      `if ($localDbInstances -notcontains '${localDbInstancePowerShell}') { sqllocaldb create '${localDbInstancePowerShell}' | Out-Null }`,
      `sqllocaldb start '${localDbInstancePowerShell}' | Out-Null`
    ].join("; ")
  ]);
}

function startServer() {
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: serverEnvironment,
    DOTNET_ENVIRONMENT: serverEnvironment,
    ASPNETCORE_URLS: baseUrl,
    ConnectionStrings__DefaultConnection: connectionString,
    Jwt__Key: process.env.Jwt__Key || "DevelopmentOnlyMiniPainterHubJwtSigningKey-DoNotUseOutsideDevelopment-2026",
    DevelopmentSeedCredentials__AdminPassword: process.env.DevelopmentSeedCredentials__AdminPassword || "P@ssw0rd!",
    DevelopmentSeedCredentials__UserPassword: process.env.DevelopmentSeedCredentials__UserPassword || "User123!",
    TestSupport__ResetEnabled: "true",
    TestSupport__ResetToken: resetToken
  };

  ensureWindowsLocalDbInstance();

  return spawn("dotnet", [
    path.join(publishDir, "MiniPainterHub.Server.dll"),
    "--urls",
    baseUrl
  ], { cwd: publishDir, env, stdio: "pipe" });
}

function attachServerLogging(child) {
  child.stdout.on("data", (chunk) => process.stdout.write(chunk));
  child.stderr.on("data", (chunk) => process.stderr.write(chunk));
}

function stopServer(child) {
  if (!child || child.killed) {
    return;
  }

  if (process.platform === "win32" && child.pid) {
    spawnSync("taskkill.exe", ["/pid", String(child.pid), "/t", "/f"], { stdio: "ignore" });
    return;
  }

  child.kill();
}

async function waitForServer() {
  const healthUrl = `${baseUrl}/healthz`;
  const startedAt = Date.now();
  let lastError;

  while (Date.now() - startedAt < 180_000) {
    try {
      const response = await fetch(healthUrl);
      if (response.ok) {
        return;
      }

      lastError = new Error(`HTTP ${response.status}`);
    } catch (error) {
      lastError = error;
    }

    await delay(750);
  }

  throw new Error(`Timed out waiting for ${healthUrl}${lastError ? ` (${lastError.message})` : ""}.`);
}

async function fetchJson(route, options = {}) {
  const response = await fetch(`${baseUrl}${route}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  });

  const text = await response.text();
  let body = null;
  if (text.length > 0) {
    body = JSON.parse(text);
  }

  return { response, body, text };
}

async function resetAppState() {
  const { response, text } = await fetchJson("/api/test-support/reset", {
    method: "POST",
    headers: {
      "X-Test-Support-Token": resetToken
    }
  });

  if (!response.ok) {
    throw new Error(`Reset failed with HTTP ${response.status}: ${text}`);
  }
}

async function loginSeedUser() {
  const { response, body, text } = await fetchJson("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({
      userName: "user",
      password: "User123!"
    })
  });

  if (!response.ok || !body?.token) {
    throw new Error(`Login failed with HTTP ${response.status}: ${text}`);
  }

  return body.token;
}

async function createRuntimePost(token) {
  const form = new FormData();
  form.append("Title", "Runtime UX fixture");
  form.append("Content", [
    "Runtime UX measurement content for the real Blazor WebAssembly route.",
    "The fixture keeps artwork quality intact and exercises post details, image decode, and viewer entry.",
    "Measurements should catch repeat-load, route-transition, scroll-adjacent, and viewer regressions."
  ].join("\n\n"));
  form.append("tags", "runtime");
  form.append("tags", "performance");
  form.append("tags", "wasm");

  const imageBuffer = fs.readFileSync(VIEWER_IMAGE_PATHS[0]);
  form.append("images", new Blob([imageBuffer], { type: "image/png" }), "runtime-route.png");

  const response = await fetch(`${baseUrl}/api/posts/with-image`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`
    },
    body: form
  });
  const text = await response.text();
  const body = text.length > 0 ? JSON.parse(text) : null;

  if (!response.ok || !body?.id) {
    throw new Error(`Create runtime post failed with HTTP ${response.status}: ${text}`);
  }

  return `/posts/${body.id}`;
}

function toUrl(route) {
  if (/^https?:\/\//i.test(route)) {
    return route;
  }

  return `${baseUrl}${route.startsWith("/") ? route : `/${route}`}`;
}

function renderExpectationFor(route) {
  const routePath = /^https?:\/\//i.test(route)
    ? new URL(route).pathname
    : route;

  if (routePath === "/" || routePath === "") {
    return () => {
      const app = document.querySelector("#app");
      const text = app?.textContent || "";
      return !app?.querySelector(".loading-progress-shell")
        && text.includes("Latest community posts")
        && Boolean(document.querySelector(".post-card"));
    };
  }

  if (/^\/posts\/\d+\/?$/.test(routePath)) {
    return () => {
      const app = document.querySelector("#app");
      return !app?.querySelector(".loading-progress-shell")
        && Boolean(document.querySelector("[data-testid='post-title']"))
        && Boolean(document.querySelector("[data-testid='post-details-gallery']"));
    };
  }

  return () => {
    const app = document.querySelector("#app");
    return Boolean(app)
      && !app.querySelector(".loading-progress-shell")
      && (app.textContent || "").trim().length > 0;
  };
}

async function addPerformanceObservers(context) {
  await context.addInitScript(() => {
    window.__mphPerf = {
      longTasks: [],
      layoutShifts: [],
      largestContentfulPaint: null
    };

    window.__mphResetPerf = () => {
      window.__mphPerf.longTasks = [];
      window.__mphPerf.layoutShifts = [];
      window.__mphPerf.largestContentfulPaint = null;
      performance.clearResourceTimings?.();
      performance.clearMeasures?.();
      performance.clearMarks?.();
    };

    if ("PerformanceObserver" in window) {
      try {
        const longTaskObserver = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            window.__mphPerf.longTasks.push({
              duration: entry.duration,
              startTime: entry.startTime
            });
          }
        });
        longTaskObserver.observe({ type: "longtask", buffered: true });
      } catch {
      }

      try {
        const layoutShiftObserver = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            if (!entry.hadRecentInput) {
              window.__mphPerf.layoutShifts.push({
                value: entry.value,
                startTime: entry.startTime,
                sources: Array.from(entry.sources || []).map((source) => {
                  const node = source.node;
                  return {
                    node: node instanceof Element
                      ? `${node.tagName.toLowerCase()}${node.id ? `#${node.id}` : ""}${node.className ? `.${String(node.className).trim().replace(/\s+/g, ".")}` : ""}${node.getAttribute("data-testid") ? `[data-testid="${node.getAttribute("data-testid")}"]` : ""}`
                      : null,
                    previousRect: source.previousRect,
                    currentRect: source.currentRect
                  };
                })
              });
            }
          }
        });
        layoutShiftObserver.observe({ type: "layout-shift", buffered: true });
      } catch {
      }

      try {
        const lcpObserver = new PerformanceObserver((list) => {
          const entries = list.getEntries();
          window.__mphPerf.largestContentfulPaint = entries[entries.length - 1] || null;
        });
        lcpObserver.observe({ type: "largest-contentful-paint", buffered: true });
      } catch {
      }
    }
  });
}

async function createMeasuredContext(browser, viewport = { width: 1350, height: 940 }) {
  const context = await browser.newContext({ baseURL: baseUrl, viewport });
  await addPerformanceObservers(context);
  return context;
}

async function waitForRouteContent(page, route) {
  await page.waitForFunction(renderExpectationFor(route), undefined, { timeout: 60_000 });
}

async function settleVisiblePage(page) {
  await page.evaluate(async () => {
    await document.fonts?.ready;
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
}

async function navigateAndMeasure(page, route, label) {
  await page.evaluate(() => window.__mphResetPerf?.()).catch(() => {});
  const startedAt = Date.now();
  await page.goto(toUrl(route), { waitUntil: "domcontentloaded", timeout: 60_000 });
  await waitForRouteContent(page, route);
  const routeRenderMs = Date.now() - startedAt;
  await settleVisiblePage(page);
  const snapshot = await runtimeSnapshot(page);

  return { label, routeRenderMs, ...snapshot };
}

async function reloadAndMeasure(page, route, label) {
  await page.evaluate(() => window.__mphResetPerf?.());
  const startedAt = Date.now();
  await page.reload({ waitUntil: "domcontentloaded", timeout: 60_000 });
  await waitForRouteContent(page, route);
  const routeRenderMs = Date.now() - startedAt;
  await settleVisiblePage(page);
  const snapshot = await runtimeSnapshot(page);

  return { label, routeRenderMs, ...snapshot };
}

async function runtimeSnapshot(page) {
  return page.evaluate(async () => {
    const decodeStartedAt = performance.now();
    const images = Array.from(document.images);
    await Promise.all(images.map((image) => {
      if (typeof image.decode !== "function") {
        return Promise.resolve();
      }

      return image.decode().catch(() => {});
    }));
    const imageDecodeMs = Math.round(performance.now() - decodeStartedAt);

    const navigation = performance.getEntriesByType("navigation")[0];
    const resources = performance.getEntriesByType("resource") || [];
    const transferSize = (entry) => entry?.transferSize || 0;
    const apiResources = resources
      .filter((entry) => entry.name.includes("/api/"))
      .map((entry) => ({
        url: entry.name,
        durationMs: Math.round(entry.duration),
        transferSize: transferSize(entry)
      }));
    const apiDurations = apiResources.map((entry) => entry.durationMs).sort((left, right) => left - right);
    const p95Index = Math.min(apiDurations.length - 1, Math.floor(apiDurations.length * 0.95));
    const frameworkResources = resources.filter((entry) => entry.name.includes("/_framework/"));
    const imageResources = resources.filter((entry) => entry.initiatorType === "img");
    const largestResources = resources
      .map((entry) => ({
        url: entry.name,
        initiatorType: entry.initiatorType,
        transferSize: transferSize(entry),
        durationMs: Math.round(entry.duration)
      }))
      .sort((left, right) => right.transferSize - left.transferSize)
      .slice(0, 10);

    return {
      navigationDurationMs: navigation?.duration || null,
      domContentLoadedMs: navigation?.domContentLoadedEventEnd || null,
      loadEventMs: navigation?.loadEventEnd || null,
      firstContentfulPaintMs: performance.getEntriesByName("first-contentful-paint")[0]?.startTime || null,
      largestContentfulPaintMs: window.__mphPerf?.largestContentfulPaint?.startTime || null,
      cumulativeLayoutShift: (window.__mphPerf?.layoutShifts || []).reduce((total, entry) => total + entry.value, 0),
      layoutShifts: window.__mphPerf?.layoutShifts || [],
      longTasksOver50Ms: (window.__mphPerf?.longTasks || []).filter((entry) => entry.duration > 50),
      longTasksOver100Ms: (window.__mphPerf?.longTasks || []).filter((entry) => entry.duration > 100),
      totalResourceTransferBytes: resources.reduce((total, entry) => total + transferSize(entry), 0),
      frameworkTransferBytes: frameworkResources.reduce((total, entry) => total + transferSize(entry), 0),
      imageTransferBytes: imageResources.reduce((total, entry) => total + transferSize(entry), 0),
      apiP95Ms: apiDurations.length > 0 ? apiDurations[p95Index] : 0,
      apiResources,
      imageDecodeMs,
      imageCount: images.length,
      domElements: document.querySelectorAll("*").length,
      largestResources
    };
  });
}

async function registerAndControlServiceWorker(page, route) {
  let state = await page.evaluate(async () => {
    if (!("serviceWorker" in navigator)) {
      return { supported: false, ready: false, controlled: false };
    }

    const registration = await navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });
    await navigator.serviceWorker.ready;
    return {
      supported: true,
      ready: Boolean(registration.active),
      controlled: Boolean(navigator.serviceWorker.controller),
      scope: registration.scope
    };
  });

  if (state.supported && !state.controlled) {
    await page.reload({ waitUntil: "domcontentloaded", timeout: 60_000 });
    await waitForRouteContent(page, route);
    state = await page.evaluate(() => ({
      supported: true,
      ready: true,
      controlled: Boolean(navigator.serviceWorker.controller),
      scope: navigator.serviceWorker.controller?.scriptURL || null
    }));
  }

  return state;
}

async function measureRepeatLoad(browser, route) {
  const context = await createMeasuredContext(browser);
  const page = await context.newPage();

  try {
    const cold = await navigateAndMeasure(page, route, "cold");
    const serviceWorker = await registerAndControlServiceWorker(page, route);
    await reloadAndMeasure(page, route, "service-worker-warmup");
    const cached = await reloadAndMeasure(page, route, "cached");
    await page.evaluate(() => window.__mphResetPerf?.());
    await page.waitForTimeout(2000);
    const postStartup = await runtimeSnapshot(page);

    return {
      route,
      url: toUrl(route),
      serviceWorker,
      cold,
      cached,
      postStartup
    };
  } finally {
    await context.close();
  }
}

async function measureRouteTransition(browser, postRoute) {
  const context = await createMeasuredContext(browser);
  const page = await context.newPage();

  try {
    await navigateAndMeasure(page, "/", "transition-source");
    await page.evaluate(() => window.__mphResetPerf?.());
    const link = page.locator(`a[href="${postRoute}"]`).first();
    await link.waitFor({ state: "visible", timeout: 20_000 });
    const startedAt = Date.now();
    await link.click();
    await waitForRouteContent(page, postRoute);
    const routeTransitionMs = Date.now() - startedAt;
    await settleVisiblePage(page);
    const snapshot = await runtimeSnapshot(page);

    return {
      from: "/",
      to: postRoute,
      routeTransitionMs,
      ...snapshot
    };
  } finally {
    await context.close();
  }
}

async function waitForViewerImage(page) {
  await page.waitForFunction(() => {
    const image = document.querySelector("[data-testid='viewer-stage-image']");
    return image instanceof HTMLImageElement
      && image.complete
      && image.naturalWidth > 0
      && image.naturalHeight > 0;
  }, undefined, { timeout: 30_000 });
}

async function waitForViewerFit(page) {
  await page.waitForFunction(() => {
    const stage = document.querySelector("[data-testid='viewer-stage']");
    const fitbox = document.querySelector("[data-testid='viewer-stage-fitbox']");
    const image = document.querySelector("[data-testid='viewer-stage-image']");
    if (!(stage instanceof HTMLElement)
      || !(fitbox instanceof HTMLElement)
      || !(image instanceof HTMLImageElement)
      || image.naturalWidth <= 0
      || image.naturalHeight <= 0) {
      return false;
    }

    const stageRect = stage.getBoundingClientRect();
    const fitboxWidth = Number.parseFloat(fitbox.style.width);
    const fitboxHeight = Number.parseFloat(fitbox.style.height);
    const scale = Math.min(stageRect.width / image.naturalWidth, stageRect.height / image.naturalHeight);
    const expectedWidth = image.naturalWidth * scale;
    const expectedHeight = image.naturalHeight * scale;

    return Math.abs(fitboxWidth - expectedWidth) <= 4
      && Math.abs(fitboxHeight - expectedHeight) <= 4;
  }, undefined, { timeout: 30_000 });
}

async function switchViewerImage(page, controlTestId) {
  const previousSrc = await page.getByTestId("viewer-stage-image").getAttribute("src");
  const startedAt = Date.now();
  await page.getByTestId(controlTestId).click();
  await page.waitForFunction((src) => {
    const image = document.querySelector("[data-testid='viewer-stage-image']");
    return image instanceof HTMLImageElement && image.getAttribute("src") !== src;
  }, previousSrc, { timeout: 30_000 });
  await waitForViewerImage(page);
  await waitForViewerFit(page);
  return Date.now() - startedAt;
}

async function measureViewer(browser, token) {
  const context = await createMeasuredContext(browser, { width: 2560, height: 1440 });
  const page = await context.newPage();

  try {
    await page.goto(toUrl("/"), { waitUntil: "domcontentloaded", timeout: 60_000 });
    await page.evaluate((authToken) => {
      localStorage.setItem("authToken", authToken);
    }, token);
    await page.reload({ waitUntil: "domcontentloaded", timeout: 60_000 });
    await page.getByTestId("nav-logout").waitFor({ state: "visible", timeout: 30_000 });
    const viewerPost = await createRichViewerPost(page, context.request, "runtime", { extraPlainCommentsCount: 2 });
    const viewerRoute = `/posts/${viewerPost.postId}`;
    await navigateAndMeasure(page, viewerRoute, "viewer-source");

    const trigger = page.getByTestId("post-details-open-viewer-hero");
    await trigger.waitFor({ state: "visible", timeout: 20_000 });
    await trigger.scrollIntoViewIfNeeded();
    await page.evaluate(() => window.__mphResetPerf?.());
    const startedAt = Date.now();
    await trigger.evaluate((button) => button.click());
    await page.getByTestId("rich-image-viewer-modal").waitFor({ state: "visible", timeout: 30_000 });
    await waitForViewerImage(page);
    await waitForViewerFit(page);
    const openMs = Date.now() - startedAt;
    const warmSwitchMs = await switchViewerImage(page, "viewer-stage-next");
    const cachedSwitchMs = await switchViewerImage(page, "viewer-stage-prev");
    const snapshot = await runtimeSnapshot(page);

    return {
      viewport: "2560x1440",
      route: viewerRoute,
      openMs,
      warmSwitchMs,
      cachedSwitchMs,
      ...snapshot
    };
  } finally {
    await context.close();
  }
}

async function verifyServiceWorkerPolicy(browser, postRoute) {
  const context = await createMeasuredContext(browser);
  const page = await context.newPage();
  const safeListUrl = "/api/posts?page=1&pageSize=1&includeDeleted=False&deletedOnly=False";
  const unsafeModerationUrl = "/api/posts?page=77&pageSize=1&includeDeleted=True&deletedOnly=False";
  const unsafeUnknownQueryUrl = "/api/posts?page=78&pageSize=1&includeDeleted=False&deletedOnly=False&moderationQueue=true";
  const unsafeAuthorizedUrl = "/api/posts?page=79&pageSize=1&includeDeleted=False&deletedOnly=False";

  try {
    await navigateAndMeasure(page, "/", "service-worker-source");
    const serviceWorker = await registerAndControlServiceWorker(page, "/");
    const onlineResult = await page.evaluate(async ({ safeListUrl, unsafeUnknownQueryUrl, unsafeAuthorizedUrl }) => {
      await fetch(safeListUrl, { credentials: "omit" });
      await fetch(unsafeUnknownQueryUrl, { credentials: "omit" }).catch(() => {});
      await fetch(unsafeAuthorizedUrl, {
        headers: {
          Authorization: "Bearer invalid-token-for-cache-policy-check"
        }
      }).catch(() => {});

      const cacheNames = await caches.keys();
      const runtimeCacheName = cacheNames.find((name) => name.startsWith("minipainterhub-runtime-")) || null;
      const runtimeUrls = runtimeCacheName
        ? (await (await caches.open(runtimeCacheName)).keys()).map((request) => new URL(request.url).pathname + new URL(request.url).search)
        : [];

      return {
        cacheNames,
        runtimeCacheName,
        runtimeUrls
      };
    }, { safeListUrl, unsafeUnknownQueryUrl, unsafeAuthorizedUrl });

    await context.setOffline(true);
    const offlineFallback = await page.evaluate(async (safeListUrl) => {
      try {
        const response = await fetch(safeListUrl, { credentials: "omit" });
        return {
          ok: response.ok,
          status: response.status,
          cachedAt: response.headers.get("X-MiniPainterHub-SW-Cached-At")
        };
      } catch (error) {
        return {
          ok: false,
          error: error?.message || String(error)
        };
      }
    }, safeListUrl);
    await context.setOffline(false);

    return {
      serviceWorker,
      safeListUrl,
      unsafeModerationUrl,
      unsafeUnknownQueryUrl,
      unsafeAuthorizedUrl,
      ...onlineResult,
      offlineFallback,
      upgradeInvalidation: inspectServiceWorkerUpgradeInvalidation()
    };
  } finally {
    await context.setOffline(false).catch(() => {});
    await context.close();
  }
}

function inspectServiceWorkerUpgradeInvalidation() {
  const publishedWorkerPath = path.join(publishDir, "wwwroot", "service-worker.js");
  const sourceWorkerPath = path.join(repoRoot, "MiniPainterHub.WebApp", "wwwroot", "service-worker.published.js");
  const workerPath = fs.existsSync(publishedWorkerPath) ? publishedWorkerPath : sourceWorkerPath;
  const source = fs.readFileSync(workerPath, "utf8");

  return {
    workerPath,
    appShellCacheUsesManifestVersion: /app-shell-\$\{self\.assetsManifest\.version\}/.test(source),
    runtimeCacheUsesManifestVersion: /runtime-\$\{self\.assetsManifest\.version\}/.test(source),
    activationDeletesOldMiniPainterHubCaches: /startsWith\("minipainterhub-"\)/.test(source)
      && /caches\.delete\(key\)/.test(source),
    serviceWorkerAssetsExcludedFromAppShell: /url\.startsWith\("service-worker"\)/.test(source),
    moderationFlagsMustBeFalseForRuntimeCache: /isFalseQueryValue\(searchParams\.get\("includeDeleted"\)\)/.test(source)
      && /isFalseQueryValue\(searchParams\.get\("deletedOnly"\)\)/.test(source)
  };
}

function collectFailures(report) {
  const failures = [];
  for (const result of report.repeatLoads) {
    if (typeof budget.cachedRouteRenderMs === "number"
      && result.cached.routeRenderMs > budget.cachedRouteRenderMs) {
      failures.push(`${result.route}: cached render ${result.cached.routeRenderMs}ms is above ${budget.cachedRouteRenderMs}ms.`);
    }

    if (typeof budget.cachedFrameworkTransferBytes === "number"
      && result.cached.frameworkTransferBytes > budget.cachedFrameworkTransferBytes) {
      failures.push(`${result.route}: cached framework transfer ${result.cached.frameworkTransferBytes} bytes is above ${budget.cachedFrameworkTransferBytes} bytes.`);
    }

    if (typeof budget.postStartupMaxLongTasksOver100Ms === "number"
      && result.postStartup.longTasksOver100Ms.length > budget.postStartupMaxLongTasksOver100Ms) {
      failures.push(`${result.route}: post-startup long tasks over 100ms ${result.postStartup.longTasksOver100Ms.length} is above ${budget.postStartupMaxLongTasksOver100Ms}.`);
    }

    if (typeof budget.cumulativeLayoutShift === "number"
      && result.cached.cumulativeLayoutShift > budget.cumulativeLayoutShift) {
      failures.push(`${result.route}: cached CLS ${result.cached.cumulativeLayoutShift} is above ${budget.cumulativeLayoutShift}.`);
    }

    if (typeof budget.apiP95Ms === "number" && result.cached.apiP95Ms > budget.apiP95Ms) {
      failures.push(`${result.route}: cached API p95 ${result.cached.apiP95Ms}ms is above ${budget.apiP95Ms}ms.`);
    }

    if (typeof budget.imageDecodeMs === "number" && result.cached.imageDecodeMs > budget.imageDecodeMs) {
      failures.push(`${result.route}: cached image decode ${result.cached.imageDecodeMs}ms is above ${budget.imageDecodeMs}ms.`);
    }
  }

  if (!report.serviceWorkerPolicy.serviceWorker.ready || !report.serviceWorkerPolicy.serviceWorker.controlled) {
    failures.push("Service worker was not ready and controlling the runtime policy check page.");
  }

  if (!report.serviceWorkerPolicy.runtimeCacheName) {
    failures.push("Service worker runtime cache was not created.");
  }

  if (!report.serviceWorkerPolicy.runtimeUrls.some((url) => url === report.serviceWorkerPolicy.safeListUrl)) {
    failures.push("Safe anonymous post-list GET was not stored in the runtime cache.");
  }

  for (const unsafeUrl of [
    report.serviceWorkerPolicy.unsafeModerationUrl,
    report.serviceWorkerPolicy.unsafeUnknownQueryUrl,
    report.serviceWorkerPolicy.unsafeAuthorizedUrl
  ]) {
    if (report.serviceWorkerPolicy.runtimeUrls.some((url) => url === unsafeUrl)) {
      failures.push(`Unsafe API request was cached: ${unsafeUrl}.`);
    }
  }

  if (!report.serviceWorkerPolicy.offlineFallback.ok
    || !report.serviceWorkerPolicy.offlineFallback.cachedAt) {
    failures.push("Safe anonymous post-list GET did not fall back from the runtime cache while offline.");
  }

  for (const [key, value] of Object.entries(report.serviceWorkerPolicy.upgradeInvalidation)) {
    if (key === "workerPath") {
      continue;
    }

    if (!value) {
      failures.push(`Service worker upgrade invalidation evidence failed: ${key}.`);
    }
  }

  if (!cachedOnly) {
    if (typeof budget.routeTransitionMs === "number"
      && report.routeTransition.routeTransitionMs > budget.routeTransitionMs) {
      failures.push(`Route transition ${report.routeTransition.routeTransitionMs}ms is above ${budget.routeTransitionMs}ms.`);
    }

    if (typeof budget.viewerOpenMs === "number" && report.viewer.openMs > budget.viewerOpenMs) {
      failures.push(`Viewer open ${report.viewer.openMs}ms is above ${budget.viewerOpenMs}ms.`);
    }

    if (typeof budget.viewerCachedSwitchMs === "number" && report.viewer.cachedSwitchMs > budget.viewerCachedSwitchMs) {
      failures.push(`Viewer cached switch ${report.viewer.cachedSwitchMs}ms is above ${budget.viewerCachedSwitchMs}ms.`);
    }
  }

  return failures;
}

function writeMarkdownReport(report, markdownPath) {
  const lines = [
    "# Runtime UX Performance Report",
    "",
    `- Base URL: ${report.baseUrl}`,
    `- Server environment: ${report.serverEnvironment}`,
    `- Mode: ${report.mode}`,
    `- Checked at: ${report.checkedAt}`,
    "",
    "## Repeat Loads",
    "",
    "| Route | Cold render | Cached render | Cached framework transfer | Post-startup >100ms long tasks | Cached CLS | API p95 | Image decode |",
    "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ...report.repeatLoads.map((result) =>
      `| ${result.route} | ${result.cold.routeRenderMs}ms | ${result.cached.routeRenderMs}ms | ${result.cached.frameworkTransferBytes} bytes | ${result.postStartup.longTasksOver100Ms.length} | ${result.cached.cumulativeLayoutShift.toFixed(4)} | ${result.cached.apiP95Ms}ms | ${result.cached.imageDecodeMs}ms |`),
    "",
    "## Route And Viewer",
    "",
    cachedOnly
      ? "- Skipped in cached-only mode."
      : `- Home to post route transition: ${report.routeTransition.routeTransitionMs}ms`,
    cachedOnly
      ? "- Viewer skipped in cached-only mode."
      : `- 2K viewer open: ${report.viewer.openMs}ms; cached switch: ${report.viewer.cachedSwitchMs}ms`,
    "",
    "## Service Worker",
    "",
    `- Ready: ${report.serviceWorkerPolicy.serviceWorker.ready}`,
    `- Controlled: ${report.serviceWorkerPolicy.serviceWorker.controlled}`,
    `- Runtime cache: ${report.serviceWorkerPolicy.runtimeCacheName || "missing"}`,
    `- Offline public post-list fallback: ${report.serviceWorkerPolicy.offlineFallback.ok}`,
    "",
    "## Failures",
    "",
    ...(report.failures.length > 0 ? report.failures.map((failure) => `- ${failure}`) : ["- None"])
  ];

  fs.writeFileSync(markdownPath, `${lines.join("\n")}\n`, "utf8");
}

async function main() {
  let serverProcess = null;
  if (ownsServer) {
    publishServer();
    serverProcess = startServer();
    attachServerLogging(serverProcess);
    await waitForServer();
  }

  try {
    await resetAppState();
    const token = await loginSeedUser();
    const postRoute = await createRuntimePost(token);
    const routes = ["/", postRoute];
    const browser = await chromium.launch({ headless: true });

    try {
      const repeatLoads = [];
      for (const route of routes) {
        repeatLoads.push(await measureRepeatLoad(browser, route));
      }

      const serviceWorkerPolicy = await verifyServiceWorkerPolicy(browser, postRoute);
      const routeTransition = cachedOnly ? null : await measureRouteTransition(browser, postRoute);
      const viewer = cachedOnly ? null : await measureViewer(browser, token);
      const report = {
        baseUrl,
        serverEnvironment,
        checkedAt: new Date().toISOString(),
        mode: cachedOnly ? "cached-only" : "runtime",
        routes,
        budgets: budget,
        repeatLoads,
        serviceWorkerPolicy,
        routeTransition,
        viewer,
        failures: []
      };
      report.failures = collectFailures(report);

      const summaryPath = path.join(outputDir, cachedOnly ? "cached-summary.json" : "summary.json");
      const markdownPath = path.join(outputDir, cachedOnly ? "cached-summary.md" : "summary.md");
      fs.writeFileSync(summaryPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
      writeMarkdownReport(report, markdownPath);

      console.log(`Runtime UX summary written to ${summaryPath}`);
      console.log(`Runtime UX markdown written to ${markdownPath}`);
      for (const result of repeatLoads) {
        console.log(`${result.route}: cold=${result.cold.routeRenderMs}ms, cached=${result.cached.routeRenderMs}ms, cached framework transfer=${result.cached.frameworkTransferBytes} bytes, post-startup >100ms long tasks=${result.postStartup.longTasksOver100Ms.length}`);
      }

      if (!cachedOnly) {
        console.log(`Route transition / -> ${postRoute}: ${routeTransition.routeTransitionMs}ms`);
        console.log(`2K viewer: open=${viewer.openMs}ms, cached switch=${viewer.cachedSwitchMs}ms`);
      }

      if (report.failures.length > 0) {
        console.error("Runtime UX budget failures:");
        for (const failure of report.failures) {
          console.error(`- ${failure}`);
        }

        process.exitCode = 1;
      }
    } finally {
      await browser.close();
    }
  } finally {
    if (serverProcess) {
      stopServer(serverProcess);
    }
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
