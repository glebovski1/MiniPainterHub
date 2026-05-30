const state = {
  observersStarted: false,
  lcp: 0,
  cls: 0,
  longTaskCount: 0,
  longTaskDuration: 0,
  resourceTotals: {
    frameworkBytes: 0,
    imageBytes: 0,
    imageCount: 0
  }
};

function bootStart() {
  return window.miniPainterHubPerf?.bootStart ?? 0;
}

function pushMetric(metrics, name, value, unit, path) {
  if (!Number.isFinite(value) || value < 0) {
    return;
  }

  metrics.push({
    name,
    value,
    unit,
    path,
    collectedAtUtc: new Date().toISOString()
  });
}

export function startObservers() {
  if (state.observersStarted || typeof PerformanceObserver === "undefined") {
    return;
  }

  state.observersStarted = true;

  try {
    new PerformanceObserver((list) => {
      const entries = list.getEntries();
      const latest = entries[entries.length - 1];
      if (latest) {
        state.lcp = latest.startTime;
      }
    }).observe({ type: "largest-contentful-paint", buffered: true });
  } catch {
  }

  try {
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        if (!entry.hadRecentInput) {
          state.cls += entry.value;
        }
      }
    }).observe({ type: "layout-shift", buffered: true });
  } catch {
  }

  try {
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        state.longTaskCount += 1;
        state.longTaskDuration += entry.duration;
      }
    }).observe({ type: "longtask", buffered: true });
  } catch {
  }
}

export function getBootToFirstRenderMs() {
  const start = bootStart();
  return start > 0 ? performance.now() - start : performance.now();
}

export function drainMetrics(path) {
  const metrics = [];

  if (state.lcp > 0) {
    pushMetric(metrics, "browser.lcp.ms", state.lcp, "ms", path);
  }

  if (state.cls > 0) {
    pushMetric(metrics, "browser.cls.score", state.cls, "score", path);
  }

  if (state.longTaskCount > 0) {
    pushMetric(metrics, "browser.long_task.count", state.longTaskCount, "count", path);
    pushMetric(metrics, "browser.long_task.duration.ms", state.longTaskDuration, "ms", path);
  }

  state.lcp = 0;
  state.cls = 0;
  state.longTaskCount = 0;
  state.longTaskDuration = 0;

  return metrics;
}

export async function drainResourceMetrics(path, timeoutMs = 2500) {
  await waitForDocumentImages(timeoutMs);

  const metrics = [];
  const totals = resourceTotals();
  const frameworkDelta = Math.max(0, totals.frameworkBytes - state.resourceTotals.frameworkBytes);
  const imageBytesDelta = Math.max(0, totals.imageBytes - state.resourceTotals.imageBytes);
  const imageCountDelta = Math.max(0, totals.imageCount - state.resourceTotals.imageCount);

  pushMetric(metrics, "resource.framework.bytes", frameworkDelta, "bytes", path);
  pushMetric(metrics, "resource.image.bytes", imageBytesDelta, "bytes", path);
  pushMetric(metrics, "resource.image.count", imageCountDelta, "count", path);

  state.resourceTotals = totals;
  return metrics;
}

function resourceTotals() {
  const resources = performance.getEntriesByType("resource") || [];
  const frameworkBytes = resources
    .filter((entry) => entry.name.includes("/_framework/"))
    .reduce((total, entry) => total + resourceSize(entry), 0);
  const imageResources = resources.filter((entry) => entry.initiatorType === "img");
  const imageBytes = imageResources.reduce((total, entry) => total + resourceSize(entry), 0);

  return {
    frameworkBytes,
    imageBytes,
    imageCount: imageResources.length
  };
}

function resourceSize(entry) {
  return entry.transferSize || entry.encodedBodySize || 0;
}

function waitForDocumentImages(timeoutMs) {
  const images = Array.from(document.images || []);
  const pending = images.filter((image) => !image.complete);

  if (pending.length === 0 || timeoutMs <= 0) {
    return Promise.resolve();
  }

  let timeoutId;
  const timeout = new Promise((resolve) => {
    timeoutId = window.setTimeout(resolve, timeoutMs);
  });
  const imageLoads = Promise.all(pending.map((image) => new Promise((resolve) => {
    image.addEventListener("load", resolve, { once: true });
    image.addEventListener("error", resolve, { once: true });
  })));

  return Promise.race([imageLoads, timeout])
    .finally(() => window.clearTimeout(timeoutId));
}
