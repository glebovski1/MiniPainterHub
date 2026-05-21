self.importScripts("./service-worker-assets.js");

const appShellCacheName = `minipainterhub-app-shell-${self.assetsManifest.version}`;
const runtimeCacheName = `minipainterhub-runtime-${self.assetsManifest.version}`;
const apiCacheMaxAgeMs = 5 * 60 * 1000;
const apiNetworkTimeoutMs = 3500;
const assetManifest = new Map(self.assetsManifest.assets.map((asset) => [asset.url, asset.hash]));

const appShellAssets = self.assetsManifest.assets
  .filter((asset) => shouldCacheAppShellAsset(asset.url))
  .map((asset) => new Request(asset.url, {
    integrity: asset.hash,
    cache: "no-cache"
  }));

self.addEventListener("install", (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(appShellCacheName);
    await cache.addAll(appShellAssets);
    await self.skipWaiting();
  })());
});

self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    const expectedCaches = new Set([appShellCacheName, runtimeCacheName]);
    const keys = await caches.keys();
    await Promise.all(keys
      .filter((key) => key.startsWith("minipainterhub-") && !expectedCaches.has(key))
      .map((key) => caches.delete(key)));
    await self.clients.claim();
  })());
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") {
    return;
  }

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(networkFirstNavigation(request));
    return;
  }

  if (isAppShellAsset(url)) {
    event.respondWith(cacheFirst(request));
    return;
  }

  if (isAnonymousPublicPostRead(request, url)) {
    event.respondWith(networkFirstRuntime(request));
  }
});

function shouldCacheAppShellAsset(url) {
  if (url.endsWith(".pdb")
    || url.startsWith("service-worker")
    || url.includes("/uploads/")) {
    return false;
  }

  return url === "index.html"
    || url.startsWith("_framework/")
    || url.startsWith("css/")
    || url.startsWith("JSHelpers/")
    || url.startsWith("fonts/")
    || url.endsWith(".webmanifest")
    || url.endsWith(".png")
    || url.endsWith(".ico")
    || url.endsWith(".svg")
    || url.endsWith(".woff")
    || url.endsWith(".woff2")
    || url.endsWith(".ttf");
}

function isAppShellAsset(url) {
  const relativeUrl = url.pathname.replace(/^\//, "");
  return assetManifest.has(relativeUrl) && shouldCacheAppShellAsset(relativeUrl);
}

function isAnonymousPublicPostRead(request, url) {
  if (request.headers.has("Authorization")) {
    return false;
  }

  if (url.pathname === "/api/posts") {
    return hasOnlySafePublicPostListParams(url.searchParams);
  }

  return /^\/api\/posts\/\d+$/.test(url.pathname)
    && Array.from(url.searchParams.keys()).length === 0;
}

function hasOnlySafePublicPostListParams(searchParams) {
  const allowedParams = new Set(["page", "pageSize", "includeDeleted", "deletedOnly"]);
  for (const key of searchParams.keys()) {
    if (!allowedParams.has(key)) {
      return false;
    }
  }

  if (!isFalseQueryValue(searchParams.get("includeDeleted"))
    || !isFalseQueryValue(searchParams.get("deletedOnly"))) {
    return false;
  }

  return isPositiveIntegerQueryValue(searchParams.get("page"))
    && isPositiveIntegerQueryValue(searchParams.get("pageSize"));
}

function isFalseQueryValue(value) {
  return value === null || value.toLowerCase() === "false";
}

function isPositiveIntegerQueryValue(value) {
  if (value === null) {
    return true;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isInteger(parsed)
    && parsed >= 1
    && String(parsed) === value;
}

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) {
    return cached;
  }

  const response = await fetch(request);
  if (response.ok) {
    const cache = await caches.open(appShellCacheName);
    await cache.put(request, response.clone());
  }

  return response;
}

async function networkFirstNavigation(request) {
  try {
    return await fetch(request);
  } catch {
    return await caches.match("index.html") || Response.error();
  }
}

async function networkFirstRuntime(request) {
  const cache = await caches.open(runtimeCacheName);
  try {
    const response = await promiseWithTimeout(fetch(request), apiNetworkTimeoutMs);
    if (response.ok) {
      await cache.put(request, await withCacheTimestamp(response.clone()));
    }

    return response;
  } catch (error) {
    const cached = await cache.match(request);
    if (cached && cachedAgeMs(cached) <= apiCacheMaxAgeMs) {
      return cached;
    }

    throw error;
  }
}

async function withCacheTimestamp(response) {
  const headers = new Headers(response.headers);
  headers.set("X-MiniPainterHub-SW-Cached-At", new Date().toISOString());
  return new Response(await response.blob(), {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

function cachedAgeMs(response) {
  const cachedAt = response.headers.get("X-MiniPainterHub-SW-Cached-At");
  const timestamp = cachedAt ? Date.parse(cachedAt) : Number.NaN;
  return Number.isFinite(timestamp) ? Date.now() - timestamp : Number.POSITIVE_INFINITY;
}

function promiseWithTimeout(promise, timeoutMs) {
  let timeoutId;
  const timeout = new Promise((_, reject) => {
    timeoutId = setTimeout(() => reject(new Error("Network timeout")), timeoutMs);
  });

  return Promise.race([promise, timeout])
    .finally(() => clearTimeout(timeoutId));
}
