const { spawn, spawnSync } = require("child_process");
const fs = require("fs");
const path = require("path");
const { chromium } = require("@playwright/test");

const e2eRoot = path.resolve(__dirname, "..");
const budgetPath = process.env.LIGHTHOUSE_BUDGET_PATH
  || path.join(e2eRoot, "lighthouse", "budget.json");
const budget = JSON.parse(fs.readFileSync(budgetPath, "utf8"));
const categoryBudgets = budget.lighthouse?.categories || {};
const advisoryCategories = new Set(budget.lighthouse?.advisoryCategories || []);
const repeatLoadBudgets = budget.repeatLoad || {};
const outputDir = process.env.LIGHTHOUSE_OUTPUT_DIR
  || path.join(e2eRoot, "perf-results", "lighthouse");
const publishDir = process.env.LIGHTHOUSE_PUBLISH_DIR
  || path.join(outputDir, "server-publish");
const preset = process.env.LIGHTHOUSE_PRESET || "desktop";
const port = process.env.LIGHTHOUSE_PORT || "5178";
const baseUrl = (process.env.LIGHTHOUSE_BASE_URL || `http://127.0.0.1:${port}`).replace(/\/$/, "");
const ownsServer = !process.env.LIGHTHOUSE_BASE_URL;
const serverEnvironment = process.env.LIGHTHOUSE_SERVER_ENVIRONMENT || "Lighthouse";
const resetToken = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const localDbInstance = process.env.E2E_LOCALDB_INSTANCE || "MiniPainterHubLighthouse";
const defaultDbName = `MiniPainterHub_Lighthouse_${Date.now()}_${process.pid}`;
const connectionString = process.env.E2E_CONNECTION_STRING
  || `Server=(localdb)\\${localDbInstance};Database=${defaultDbName};Trusted_Connection=True;MultipleActiveResultSets=true`;
const localBin = process.platform === "win32"
  ? path.join(e2eRoot, "node_modules", ".bin", "lighthouse.cmd")
  : path.join(e2eRoot, "node_modules", ".bin", "lighthouse");

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

async function createAuditPost(token) {
  const { response, body, text } = await fetchJson("/api/posts", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`
    },
    body: JSON.stringify({
      title: "Lighthouse WASM audit fixture",
      content: [
        "Release-mode Lighthouse audit content for the real Blazor WebAssembly route.",
        "This page intentionally exercises the normal API-backed post details flow without a static HTML bypass.",
        "The artwork stays backed by the same preview/full-image data used by the interactive viewer."
      ].join("\n\n"),
      tags: ["lighthouse", "performance", "wasm"],
      images: [
        {
          imageUrl: "/uploads/images/9_minis1.jpg",
          previewUrl: "/uploads/images/1021_3_download.jpeg",
          thumbnailUrl: "/uploads/images/1021_3_download.jpeg",
          width: 1200,
          height: 900
        }
      ]
    })
  });

  if (!response.ok || !body?.id) {
    throw new Error(`Create audit post failed with HTTP ${response.status}: ${text}`);
  }

  return `/posts/${body.id}`;
}

async function discoverPostRoute() {
  const { response, body } = await fetchJson("/api/posts?page=1&pageSize=1");
  const post = body?.items?.[0] || body?.Items?.[0];
  if (!response.ok || !post?.id) {
    throw new Error("No post route is available for Lighthouse. Start the self-hosted audit or provide LIGHTHOUSE_POST_ROUTE.");
  }

  return `/posts/${post.id}`;
}

async function resolvePostRoute() {
  if (process.env.LIGHTHOUSE_POST_ROUTE) {
    return process.env.LIGHTHOUSE_POST_ROUTE;
  }

  if (ownsServer) {
    await resetAppState();
    const token = await loginSeedUser();
    return createAuditPost(token);
  }

  return discoverPostRoute();
}

function resolveRoutes(postRoute) {
  const rawRoutes = process.env.LIGHTHOUSE_ROUTES
    ? process.env.LIGHTHOUSE_ROUTES.split(",")
    : budget.lighthouse?.routes || ["/", "$createdPost"];

  return rawRoutes
    .map((route) => route.trim())
    .filter(Boolean)
    .map((route) => route === "$createdPost" ? postRoute : route);
}

function toAuditUrl(route) {
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
    return {
      name: "home feed",
      predicate: () => {
        const app = document.querySelector("#app");
        const text = app?.textContent || "";
        return !app?.querySelector(".loading-progress-shell")
          && text.includes("Latest community posts")
          && Boolean(document.querySelector(".post-card"));
      }
    };
  }

  if (/^\/posts\/\d+\/?$/.test(routePath)) {
    return {
      name: "post details",
      predicate: () => {
        const app = document.querySelector("#app");
        return !app?.querySelector(".loading-progress-shell")
          && Boolean(document.querySelector("[data-testid='post-title']"))
          && Boolean(document.querySelector("[data-testid='post-details-gallery']"));
      }
    };
  }

  return {
    name: "Blazor app content",
    predicate: () => {
      const app = document.querySelector("#app");
      return Boolean(app)
        && !app.querySelector(".loading-progress-shell")
        && (app.textContent || "").trim().length > 0;
    }
  };
}

async function assertRouteRendered(route) {
  const url = toAuditUrl(route);
  const expectation = renderExpectationFor(route);
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({
    viewport: { width: preset === "desktop" ? 1350 : 390, height: preset === "desktop" ? 940 : 844 }
  });
  const browserErrors = [];

  page.on("console", (message) => {
    if (message.type() === "error") {
      browserErrors.push(message.text());
    }
  });

  page.on("pageerror", (error) => browserErrors.push(error.message));

  try {
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60_000 });
    try {
      await page.waitForFunction(expectation.predicate, undefined, { timeout: 60_000 });
    } catch (error) {
      const timeoutState = await page.evaluate(() => {
        const app = document.querySelector("#app");
        const errorUi = document.querySelector("#blazor-error-ui");

        return {
          title: document.title,
          appText: (app?.textContent || "").replace(/\s+/g, " ").trim().slice(0, 700),
          bodyText: document.body.innerText.replace(/\s+/g, " ").trim().slice(0, 700),
          loadingShellPresent: Boolean(app?.querySelector(".loading-progress-shell")),
          errorUiVisible: Boolean(errorUi)
            && getComputedStyle(errorUi).display !== "none",
          appHtml: (app?.innerHTML || "").slice(0, 1000)
        };
      });

      throw new Error(`${url}: timed out waiting for ${expectation.name}. State=${JSON.stringify(timeoutState)} BrowserErrors=${JSON.stringify(browserErrors)} Cause=${error.message}`);
    }

    const state = await page.evaluate(() => ({
      title: document.title,
      bodyText: document.body.innerText.slice(0, 500),
      loadingShellPresent: Boolean(document.querySelector("#app .loading-progress-shell")),
      errorUiVisible: getComputedStyle(document.querySelector("#blazor-error-ui") || document.body).display !== "none"
        && Boolean(document.querySelector("#blazor-error-ui"))
    }));

    if (state.loadingShellPresent) {
      throw new Error(`${url}: Blazor loading shell is still present.`);
    }

    if (state.errorUiVisible) {
      throw new Error(`${url}: Blazor error UI is visible.`);
    }

    const fatalErrors = browserErrors.filter((message) =>
      /MONO_WASM|mono_wasm_load_runtime|NIY encountered|Unhandled|Error:/i.test(message));
    if (fatalErrors.length > 0) {
      throw new Error(`${url}: browser reported startup errors: ${fatalErrors.join(" | ")}`);
    }

    return {
      route,
      url,
      expectation: expectation.name,
      title: state.title,
      checkedAt: new Date().toISOString()
    };
  } finally {
    await browser.close();
  }
}

async function assertRoutesRendered(routes) {
  const checks = [];
  for (const route of routes) {
    const check = await assertRouteRendered(route);
    checks.push(check);
    console.log(`${route}: rendered ${check.expectation} before Lighthouse audit.`);
  }

  return checks;
}

function safeFileToken(route) {
  return route
    .replace(/^https?:\/\//i, "")
    .replace(/[^a-z0-9]+/gi, "-")
    .replace(/^-+|-+$/g, "")
    || "root";
}

function lighthouseCommand(url, outputPath) {
  const lighthouseArgs = [
    url,
    "--quiet",
    "--output=json",
    `--output-path=${outputPath}`,
    "--max-wait-for-load=45000",
    "--chrome-flags=--headless=new --no-sandbox --disable-gpu"
  ];

  if (preset) {
    lighthouseArgs.push(`--preset=${preset}`);
  }

  if (fs.existsSync(localBin)) {
    return { command: localBin, args: lighthouseArgs };
  }

  return {
    command: process.platform === "win32" ? "npx.cmd" : "npx",
    args: ["--yes", "lighthouse", ...lighthouseArgs]
  };
}

function isKnownWindowsCleanupFailure(output) {
  return process.platform === "win32"
    && /EPERM|EBUSY/i.test(output)
    && /rmdir|unlink|rm|cleanup|temporary|chrome|lighthouse/i.test(output);
}

function auditNumericValue(report, id) {
  const value = report.audits?.[id]?.numericValue;
  return typeof value === "number" ? value : null;
}

function auditDisplayValue(report, id) {
  return report.audits?.[id]?.displayValue || null;
}

function extractLcpElement(report) {
  const item = report.audits?.["largest-contentful-paint-element"]?.details?.items?.[0];
  if (item) {
    return {
      sourceAudit: "largest-contentful-paint-element",
      type: item.type || null,
      url: item.url || null,
      selector: item.node?.selector || null,
      nodeLabel: item.node?.nodeLabel || null,
      snippet: item.node?.snippet || null
    };
  }

  const insightIds = ["lcp-breakdown-insight", "lcp-discovery-insight"];
  for (const auditId of insightIds) {
    const insightItems = report.audits?.[auditId]?.details?.items || [];
    const nodeItem = insightItems.find((detail) => detail?.type === "node");
    if (nodeItem) {
      return {
        sourceAudit: auditId,
        type: nodeItem.type || null,
        url: nodeItem.url || null,
        selector: nodeItem.selector || null,
        nodeLabel: nodeItem.nodeLabel || null,
        snippet: nodeItem.snippet || null,
        boundingRect: nodeItem.boundingRect || null
      };
    }
  }

  return null;
}

function extractNetworkMetrics(report) {
  const items = report.audits?.["network-requests"]?.details?.items || [];
  const requests = items.map((item) => ({
    url: item.url,
    resourceType: item.resourceType || item.mimeType || "Other",
    transferSize: item.transferSize || 0,
    resourceSize: item.resourceSize || 0
  }));

  return {
    totalTransferSize: requests.reduce((total, item) => total + item.transferSize, 0),
    totalResourceSize: requests.reduce((total, item) => total + item.resourceSize, 0),
    largestAssets: requests
      .filter((item) => item.url)
      .sort((a, b) => b.transferSize - a.transferSize)
      .slice(0, 12)
  };
}

function extractMetrics(report) {
  return {
    firstContentfulPaintMs: auditNumericValue(report, "first-contentful-paint"),
    firstContentfulPaint: auditDisplayValue(report, "first-contentful-paint"),
    largestContentfulPaintMs: auditNumericValue(report, "largest-contentful-paint"),
    largestContentfulPaint: auditDisplayValue(report, "largest-contentful-paint"),
    totalBlockingTimeMs: auditNumericValue(report, "total-blocking-time"),
    totalBlockingTime: auditDisplayValue(report, "total-blocking-time"),
    speedIndexMs: auditNumericValue(report, "speed-index"),
    speedIndex: auditDisplayValue(report, "speed-index"),
    cumulativeLayoutShift: auditNumericValue(report, "cumulative-layout-shift"),
    largestContentfulPaintElement: extractLcpElement(report),
    network: extractNetworkMetrics(report)
  };
}

async function fetchHeaderEntry(route) {
  const response = await fetch(`${baseUrl}${route}`, {
    headers: {
      "Accept-Encoding": "br, gzip"
    }
  });

  await response.arrayBuffer();
  return {
    route,
    status: response.status,
    cacheControl: response.headers.get("cache-control"),
    contentEncoding: response.headers.get("content-encoding"),
    contentType: response.headers.get("content-type"),
    contentLength: response.headers.get("content-length"),
    etag: response.headers.get("etag"),
    lastModified: response.headers.get("last-modified"),
    xContentTypeOptions: response.headers.get("x-content-type-options")
  };
}

async function resolveFrameworkHeaderRoutes() {
  const routes = new Set([
    "/_framework/blazor.webassembly.js",
    "/_framework/blazor.boot.json"
  ]);

  try {
    const response = await fetch(`${baseUrl}/_framework/blazor.boot.json`);
    if (!response.ok) {
      return [...routes];
    }

    const boot = await response.json();
    const resources = Object.values(boot.resources || {});
    const candidates = resources.flatMap((group) => Object.keys(group || {}));
    const byExtension = [".wasm", ".js", ".dat"];
    for (const extension of byExtension) {
      const match = candidates.find((candidate) => candidate.endsWith(extension));
      if (match) {
        routes.add(`/_framework/${match}`);
      }
    }
  } catch {
  }

  return [...routes];
}

function listPublishedStaticPdbFiles() {
  if (!ownsServer || !fs.existsSync(publishDir)) {
    return [];
  }

  const root = path.join(publishDir, "wwwroot");
  if (!fs.existsSync(root)) {
    return [];
  }

  const matches = [];
  const pending = [root];
  while (pending.length > 0) {
    const current = pending.pop();
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      const fullPath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        pending.push(fullPath);
        continue;
      }

      if (entry.name.toLowerCase().endsWith(".pdb")) {
        matches.push(path.relative(root, fullPath).replace(/\\/g, "/"));
      }
    }
  }

  return matches.sort();
}

async function collectStaticHeaderReport(postRoute) {
  const frameworkRoutes = await resolveFrameworkHeaderRoutes();
  const postApiRoute = /^\/posts\/\d+\/?$/.test(postRoute || "")
    ? postRoute.replace(/^\/posts\//, "/api/posts/")
    : null;
  const routes = [
    "/",
    "/index.html",
    "/service-worker.js",
    "/service-worker-assets.js",
    "/css/app.css",
    "/css/bootstrap/bootstrap.min.css",
    "/css/fonts.css",
    "/css/bootstrap-icons-subset.css",
    "/MiniPainterHub.WebApp.styles.css",
    "/JSHelpers/domHelpers.js",
    "/JSHelpers/runtimeWarmup.js",
    "/favicon.png",
    "/api/posts?page=1&pageSize=1&includeDeleted=False&deletedOnly=False",
    ...(postApiRoute ? [postApiRoute] : []),
    ...frameworkRoutes
  ];

  const uniqueRoutes = [...new Set(routes)];
  const entries = [];
  for (const route of uniqueRoutes) {
    const entry = await fetchHeaderEntry(route);
    entries.push(entry);
  }

  const report = {
    checkedAt: new Date().toISOString(),
    pdbFilesInPublishedWwwroot: listPublishedStaticPdbFiles(),
    entries
  };

  report.failures = staticHeaderFailures(report);
  return report;
}

function staticHeaderFailures(report) {
  const failures = [];
  if (report.pdbFilesInPublishedWwwroot.length > 0) {
    failures.push(`Published wwwroot contains .pdb files: ${report.pdbFilesInPublishedWwwroot.join(", ")}`);
  }

  const byRoute = new Map(report.entries.map((entry) => [entry.route, entry]));
  for (const route of ["/", "/index.html", "/service-worker.js", "/service-worker-assets.js", "/_framework/blazor.boot.json"]) {
    const entry = byRoute.get(route);
    if (entry?.status === 200 && entry.cacheControl !== "no-cache") {
      failures.push(`${route}: expected cache-control no-cache, got ${entry.cacheControl || "missing"}.`);
    }
  }

  for (const entry of report.entries) {
    if (entry.status !== 200) {
      continue;
    }

    if (isCompressibleStaticRoute(entry.route) && !isCompressed(entry)) {
      failures.push(`${entry.route}: expected Brotli or gzip content-encoding, got ${entry.contentEncoding || "none"}.`);
    }

    if (entry.route.startsWith("/api/")) {
      if (/^public\b/i.test(entry.cacheControl || "")) {
        failures.push(`${entry.route}: API response must not be publicly HTTP-cacheable, got ${entry.cacheControl}.`);
      }

      if (entry.cacheControl !== "no-store") {
        failures.push(`${entry.route}: expected cache-control no-store, got ${entry.cacheControl || "missing"}.`);
      }

      if (!/application\/json/i.test(entry.contentType || "")) {
        failures.push(`${entry.route}: expected application/json content-type, got ${entry.contentType || "missing"}.`);
      }
    }

    if (entry.route.startsWith("/_framework/")
      && !entry.route.endsWith("blazor.boot.json")
      && !/^public, max-age=\d+/.test(entry.cacheControl || "")) {
      failures.push(`${entry.route}: expected public cache-control, got ${entry.cacheControl || "missing"}.`);
    }

    if ((entry.route.startsWith("/css/") || entry.route.startsWith("/JSHelpers/") || entry.route.endsWith(".styles.css"))
      && !/^public, max-age=\d+/.test(entry.cacheControl || "")) {
      failures.push(`${entry.route}: expected public cache-control, got ${entry.cacheControl || "missing"}.`);
    }

    if (entry.xContentTypeOptions !== "nosniff") {
      failures.push(`${entry.route}: expected x-content-type-options nosniff, got ${entry.xContentTypeOptions || "missing"}.`);
    }
  }

  return failures;
}

function isCompressed(entry) {
  return /^(br|gzip)$/i.test(entry.contentEncoding || "");
}

function isCompressibleStaticRoute(route) {
  if (route.startsWith("/api/")) {
    return false;
  }

  return route === "/"
    || route.endsWith(".html")
    || route.endsWith(".css")
    || route.endsWith(".js")
    || route.endsWith(".json")
    || route.endsWith(".wasm")
    || route.endsWith(".dat")
    || route.endsWith(".svg")
    || route.startsWith("/_framework/");
}

async function measureRepeatLoads(routes) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: preset === "desktop" ? 1350 : 390, height: preset === "desktop" ? 940 : 844 }
  });

  await context.addInitScript(() => {
    window.__miniPainterHubLongTasks = [];
    if ("PerformanceObserver" in window) {
      try {
        const observer = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            window.__miniPainterHubLongTasks.push({
              duration: entry.duration,
              startTime: entry.startTime
            });
          }
        });
        observer.observe({ type: "longtask", buffered: true });
      } catch {
      }
    }
  });

  try {
    const measurements = [];
    for (const route of routes) {
      const page = await context.newPage();
      const cold = await navigateAndSnapshot(page, route, "cold");
      const serviceWorker = await waitForServiceWorker(page);
      await page.evaluate(() => {
        window.__miniPainterHubLongTasks = [];
      });
      const cached = await reloadAndSnapshot(page, route, "cached");
      await page.evaluate(() => {
        window.__miniPainterHubLongTasks = [];
      });
      await page.waitForTimeout(2000);
      const postStartupLongTasks = await page.evaluate(() =>
        (window.__miniPainterHubLongTasks || []).filter((entry) => entry.duration > 50));

      measurements.push({
        route,
        url: toAuditUrl(route),
        serviceWorker,
        cold,
        cached,
        postStartupLongTasks
      });

      await page.close();
    }

    return measurements;
  } finally {
    await browser.close();
  }
}

async function navigateAndSnapshot(page, route, label) {
  const startedAt = Date.now();
  await page.goto(toAuditUrl(route), { waitUntil: "domcontentloaded", timeout: 60_000 });
  const expectation = renderExpectationFor(route);
  await page.waitForFunction(expectation.predicate, undefined, { timeout: 60_000 });
  await page.waitForLoadState("load", { timeout: 60_000 }).catch(() => {});
  await page.evaluate(async () => {
    await document.fonts?.ready;
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  const routeRenderMs = Date.now() - startedAt;
  const snapshot = await performanceSnapshot(page);
  return { label, routeRenderMs, ...snapshot };
}

async function reloadAndSnapshot(page, route, label) {
  const startedAt = Date.now();
  await page.reload({ waitUntil: "domcontentloaded", timeout: 60_000 });
  const expectation = renderExpectationFor(route);
  await page.waitForFunction(expectation.predicate, undefined, { timeout: 60_000 });
  await page.waitForLoadState("load", { timeout: 60_000 }).catch(() => {});
  await page.evaluate(async () => {
    await document.fonts?.ready;
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  const routeRenderMs = Date.now() - startedAt;
  const snapshot = await performanceSnapshot(page);
  return { label, routeRenderMs, ...snapshot };
}

async function waitForServiceWorker(page) {
  return page.evaluate(async () => {
    if (!("serviceWorker" in navigator)) {
      return { supported: false, ready: false, controlled: false };
    }

    try {
      const registration = await navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" });
      await navigator.serviceWorker.ready;
      if (!navigator.serviceWorker.controller) {
        await Promise.race([
          new Promise((resolve) => navigator.serviceWorker.addEventListener("controllerchange", resolve, { once: true })),
          new Promise((resolve) => setTimeout(resolve, 2000))
        ]);
      }

      return {
        supported: true,
        ready: Boolean(registration?.active),
        controlled: Boolean(navigator.serviceWorker.controller),
        scope: registration?.scope || null
      };
    } catch (error) {
      return {
        supported: true,
        ready: false,
        controlled: Boolean(navigator.serviceWorker.controller),
        error: error?.message || String(error)
      };
    }
  });
}

async function performanceSnapshot(page) {
  return page.evaluate(() => {
    const navigation = performance.getEntriesByType("navigation")[0];
    const resources = performance.getEntriesByType("resource") || [];
    const frameworkResources = resources.filter((entry) => entry.name.includes("/_framework/"));
    const imageResources = resources.filter((entry) => entry.initiatorType === "img");
    const cssResources = resources.filter((entry) => entry.name.endsWith(".css"));
    const jsResources = resources.filter((entry) => entry.name.endsWith(".js"));
    const transferSize = (entry) => entry?.transferSize || 0;

    return {
      navigationDurationMs: navigation?.duration || null,
      domContentLoadedMs: navigation?.domContentLoadedEventEnd || null,
      loadEventMs: navigation?.loadEventEnd || null,
      navigationTransferBytes: transferSize(navigation),
      totalResourceTransferBytes: resources.reduce((total, entry) => total + transferSize(entry), 0),
      frameworkTransferBytes: frameworkResources.reduce((total, entry) => total + transferSize(entry), 0),
      imageTransferBytes: imageResources.reduce((total, entry) => total + transferSize(entry), 0),
      cssTransferBytes: cssResources.reduce((total, entry) => total + transferSize(entry), 0),
      jsTransferBytes: jsResources.reduce((total, entry) => total + transferSize(entry), 0),
      frameworkRequestCount: frameworkResources.length,
      imageRequestCount: imageResources.length,
      longTasksOver50ms: (window.__miniPainterHubLongTasks || []).filter((entry) => entry.duration > 50)
    };
  });
}

function repeatLoadFailures(repeatLoads) {
  const failures = [];
  const cachedRouteRenderMs = repeatLoadBudgets.cachedRouteRenderMs;
  const cachedFrameworkTransferBytes = repeatLoadBudgets.cachedFrameworkTransferBytes;
  const postStartupMaxLongTasks = repeatLoadBudgets.postStartupMaxLongTasks;

  for (const result of repeatLoads) {
    if (typeof cachedRouteRenderMs === "number" && result.cached.routeRenderMs > cachedRouteRenderMs) {
      failures.push(`${result.url}: cached route render ${result.cached.routeRenderMs}ms is above ${cachedRouteRenderMs}ms.`);
    }

    if (typeof cachedFrameworkTransferBytes === "number" && result.cached.frameworkTransferBytes > cachedFrameworkTransferBytes) {
      failures.push(`${result.url}: cached framework transfer ${result.cached.frameworkTransferBytes} bytes is above ${cachedFrameworkTransferBytes} bytes.`);
    }

    if (typeof postStartupMaxLongTasks === "number" && result.postStartupLongTasks.length > postStartupMaxLongTasks) {
      failures.push(`${result.url}: post-startup long tasks ${result.postStartupLongTasks.length} is above ${postStartupMaxLongTasks}.`);
    }
  }

  return failures;
}

function runLighthouse(routes, renderChecks) {
  const summary = [];
  const failures = [];
  const warnings = [];

  for (const route of routes) {
    const url = toAuditUrl(route);
    const outputPath = path.join(outputDir, `${safeFileToken(route)}.json`);
    const { command, args } = lighthouseCommand(url, outputPath);
    const result = spawnSync(command, args, {
      cwd: e2eRoot,
      env: process.env,
      shell: process.platform === "win32",
      encoding: "utf8"
    });

    if (result.stdout) {
      process.stdout.write(result.stdout);
    }

    if (result.stderr) {
      process.stderr.write(result.stderr);
    }

    const combinedOutput = `${result.stdout || ""}\n${result.stderr || ""}`;
    if (result.error || result.status !== 0) {
      if (!fs.existsSync(outputPath) || !isKnownWindowsCleanupFailure(combinedOutput)) {
        failures.push(`${url}: Lighthouse command failed${result.error ? ` (${result.error.message})` : ` (exit ${result.status})`}.`);
        continue;
      }

      warnings.push(`${url}: Lighthouse hit a known Windows cleanup failure after writing JSON output; continuing with parsed scores.`);
    }

    if (!fs.existsSync(outputPath)) {
      failures.push(`${url}: Lighthouse did not write ${outputPath}.`);
      continue;
    }

    const report = JSON.parse(fs.readFileSync(outputPath, "utf8"));
    const categories = Object.fromEntries(
      Object.entries(report.categories || {}).map(([name, category]) => [name, category.score])
    );
    const metrics = extractMetrics(report);

    summary.push({
      route,
      url,
      categories,
      metrics,
      renderCheck: renderChecks.find((check) => check.route === route) || null
    });

    for (const [category, minScore] of Object.entries(categoryBudgets)) {
      const actual = categories[category];
      if (typeof actual !== "number" || actual < minScore) {
        const message = `${url}: ${category} ${actual ?? "n/a"} is below ${minScore}.`;
        if (advisoryCategories.has(category)) {
          warnings.push(`${message} Advisory only for the Runtime UX hardening phase.`);
        } else {
          failures.push(message);
        }
      }
    }
  }

  return { summary, failures, warnings };
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
    const postRoute = await resolvePostRoute();
    const routes = resolveRoutes(postRoute);
    const staticHeaders = await collectStaticHeaderReport(postRoute);
    const renderChecks = await assertRoutesRendered(routes);
    const { summary, warnings, failures } = runLighthouse(routes, renderChecks);
    const repeatLoads = await measureRepeatLoads(routes);
    failures.push(...staticHeaders.failures);
    failures.push(...repeatLoadFailures(repeatLoads));
    const summaryPath = path.join(outputDir, "summary.json");
    fs.writeFileSync(
      summaryPath,
      `${JSON.stringify({ baseUrl, preset, serverEnvironment, routes, staticHeaders, renderChecks, repeatLoads, summary, warnings, failures }, null, 2)}\n`,
      "utf8");

    console.log(`Lighthouse summary written to ${summaryPath}`);
    console.log("Static header report:");
    for (const entry of staticHeaders.entries) {
      console.log(`${entry.route}: status=${entry.status}, cache-control=${entry.cacheControl || "missing"}, content-encoding=${entry.contentEncoding || "none"}, content-type=${entry.contentType || "missing"}`);
    }
    console.log(`Published static .pdb files: ${staticHeaders.pdbFilesInPublishedWwwroot.length}`);
    for (const result of summary) {
      console.log(`${result.route}: ${Object.entries(result.categories).map(([name, score]) => `${name}=${Math.round(score * 100)}`).join(", ")}`);
      console.log(`${result.route}: FCP=${Math.round(result.metrics.firstContentfulPaintMs ?? 0)}ms, LCP=${Math.round(result.metrics.largestContentfulPaintMs ?? 0)}ms, TBT=${Math.round(result.metrics.totalBlockingTimeMs ?? 0)}ms, SI=${Math.round(result.metrics.speedIndexMs ?? 0)}ms, CLS=${result.metrics.cumulativeLayoutShift ?? "n/a"}`);
      if (result.metrics.largestContentfulPaintElement) {
        console.log(`${result.route}: LCP element=${result.metrics.largestContentfulPaintElement.nodeLabel || result.metrics.largestContentfulPaintElement.selector || result.metrics.largestContentfulPaintElement.url || "unknown"}`);
      }
    }
    for (const result of repeatLoads) {
      console.log(`${result.route}: cold render=${result.cold.routeRenderMs}ms, cached render=${result.cached.routeRenderMs}ms, cached framework transfer=${result.cached.frameworkTransferBytes} bytes, post-startup long tasks=${result.postStartupLongTasks.length}, serviceWorkerReady=${result.serviceWorker.ready}`);
    }

    if (warnings.length > 0) {
      console.warn("Lighthouse warnings:");
      for (const warning of warnings) {
        console.warn(`- ${warning}`);
      }
    }

    if (failures.length > 0) {
      console.error("Lighthouse budget failures:");
      for (const failure of failures) {
        console.error(`- ${failure}`);
      }

      process.exitCode = 1;
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
