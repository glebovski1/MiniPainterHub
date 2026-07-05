const fs = require("fs");
const path = require("path");
const { test, expect } = require("@playwright/test");
const { createRichViewerPost } = require("./helpers/viewer-scenario");

const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const REPO_ROOT = path.resolve(__dirname, "../..");
const BUDGET = require("../lighthouse/budget.json").viewer;
const VIEWPORTS = [
  { name: "1280x720", width: 1280, height: 720 },
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "2560x1440", width: 2560, height: 1440 }
];

async function resetAppState(request) {
  const response = await request.post("/api/test-support/reset", {
    headers: {
      "X-Test-Support-Token": RESET_TOKEN
    }
  });

  expect(response.ok(), `Reset failed with HTTP ${response.status()}`).toBeTruthy();
}

async function loginAsSeedUser(page, request) {
  const response = await request.post("/api/auth/login", {
    data: {
      userName: "user",
      password: "User123!"
    }
  });

  expect(response.ok(), `Login API failed with HTTP ${response.status()}`).toBeTruthy();
  const payload = await response.json();
  expect(payload?.token, "Login API did not return a token.").toBeTruthy();

  await page.goto("/");
  await page.evaluate((token) => {
    localStorage.setItem("authToken", token);
  }, payload.token);
  await page.reload();
  await expect(page.getByTestId("nav-logout")).toBeVisible();
}

async function waitForDecodedViewerImage(page) {
  await page.waitForFunction(() => {
    const image = document.querySelector("[data-testid='viewer-stage-image']");
    return image instanceof HTMLImageElement
      && image.complete
      && image.naturalWidth > 0
      && image.naturalHeight > 0;
  });
}

async function waitForViewerFit(page) {
  await page.waitForFunction((tolerancePx) => {
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

    return Math.abs(fitboxWidth - expectedWidth) <= tolerancePx
      && Math.abs(fitboxHeight - expectedHeight) <= tolerancePx;
  }, BUDGET.fitTolerancePx);
}

async function collectViewerMetrics(page) {
  return page.evaluate(() => {
    const rectOf = (selector) => {
      const element = document.querySelector(selector);
      if (!(element instanceof HTMLElement)) {
        return null;
      }

      const rect = element.getBoundingClientRect();
      return {
        width: Math.round(rect.width),
        height: Math.round(rect.height)
      };
    };

    const fitbox = document.querySelector("[data-testid='viewer-stage-fitbox']");
    const fitboxStyle = fitbox instanceof HTMLElement
      ? {
          width: Number.parseFloat(fitbox.style.width) || 0,
          height: Number.parseFloat(fitbox.style.height) || 0
        }
      : null;

    const backdrop = document.querySelector(".viewer-shell__backdrop");
    const backdropStyle = backdrop instanceof HTMLElement ? getComputedStyle(backdrop) : null;
    const thumbnailBoxes = Array.from(document.querySelectorAll("[data-testid='viewer-thumbnail']"))
      .map((thumbnail) => {
        const rect = thumbnail.getBoundingClientRect();
        return {
          width: rect.width,
          height: rect.height,
          aspectSkew: Math.max(rect.width / Math.max(rect.height, 1), rect.height / Math.max(rect.width, 1))
        };
      });

    return {
      domElements: document.querySelectorAll("*").length,
      imageElements: document.querySelectorAll("img").length,
      shell: rectOf("[data-testid='rich-image-viewer']"),
      stage: rectOf("[data-testid='viewer-stage']"),
      activeImage: rectOf("[data-testid='viewer-stage-image']"),
      backdrop: rectOf(".viewer-shell__backdrop"),
      fitbox: fitboxStyle,
      backdropBackground: backdropStyle?.backgroundImage || "",
      backdropFilter: backdropStyle?.filter || "",
      thumbnailCount: thumbnailBoxes.length,
      maxThumbnailAspectSkew: thumbnailBoxes.reduce((max, box) => Math.max(max, box.aspectSkew), 0)
    };
  });
}

async function switchViewerImage(page, controlTestId = "viewer-stage-next") {
  const image = page.getByTestId("viewer-stage-image");
  const previousSrc = await image.getAttribute("src");
  const start = performance.now();

  await page.getByTestId(controlTestId).click();
  await page.waitForFunction((src) => {
    const current = document.querySelector("[data-testid='viewer-stage-image']");
    return current instanceof HTMLImageElement && current.getAttribute("src") !== src;
  }, previousSrc);
  await waitForDecodedViewerImage(page);
  await waitForViewerFit(page);

  return Math.round(performance.now() - start);
}

async function beginDragFrameMetrics(page) {
  await page.evaluate(() => {
    const state = {
      done: false,
      frames: [],
      longTasks: [],
      observer: null,
      rafId: 0,
      startedAt: performance.now(),
      lastFrameAt: 0
    };

    if ("PerformanceObserver" in window) {
      try {
        state.observer = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            state.longTasks.push({
              duration: entry.duration,
              startTime: entry.startTime
            });
          }
        });
        state.observer.observe({ type: "longtask", buffered: false });
      } catch {
        state.observer = null;
      }
    }

    const tick = (timestamp) => {
      if (state.done) {
        return;
      }

      if (state.lastFrameAt > 0) {
        state.frames.push(timestamp - state.lastFrameAt);
      }

      state.lastFrameAt = timestamp;
      state.rafId = requestAnimationFrame(tick);
    };

    window.__mphViewerDragMetrics = state;
    state.rafId = requestAnimationFrame(tick);
  });
}

async function finishDragFrameMetrics(page) {
  return page.evaluate(async () => {
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

    const state = window.__mphViewerDragMetrics;
    if (!state) {
      return {
        durationMs: 0,
        frameCount: 0,
        p95FrameMs: 0,
        maxFrameMs: 0,
        droppedFramesOver34Ms: 0,
        longTasksOver50Ms: []
      };
    }

    state.done = true;
    cancelAnimationFrame(state.rafId);
    state.observer?.disconnect();

    const frames = state.frames
      .filter((value) => Number.isFinite(value) && value > 0)
      .map((value) => Math.round(value * 10) / 10);
    const sortedFrames = [...frames].sort((left, right) => left - right);
    const p95Index = sortedFrames.length === 0
      ? -1
      : Math.min(sortedFrames.length - 1, Math.ceil(sortedFrames.length * 0.95) - 1);
    const longTasksOver50Ms = state.longTasks
      .filter((entry) => entry.duration > 50)
      .map((entry) => ({
        duration: Math.round(entry.duration),
        startTime: Math.round(entry.startTime)
      }));

    delete window.__mphViewerDragMetrics;

    return {
      durationMs: Math.round(performance.now() - state.startedAt),
      frameCount: frames.length,
      p95FrameMs: p95Index >= 0 ? sortedFrames[p95Index] : 0,
      maxFrameMs: sortedFrames.length > 0 ? sortedFrames[sortedFrames.length - 1] : 0,
      droppedFramesOver34Ms: frames.filter((value) => value > 34).length,
      longTasksOver50Ms
    };
  });
}

async function waitForViewerActualSizePan(page) {
  await page.waitForFunction(() => {
    const stage = document.querySelector("[data-testid='viewer-stage']");
    const transform = document.querySelector("[data-testid='viewer-stage-transform']");
    if (!(stage instanceof HTMLElement) || !(transform instanceof HTMLElement)) {
      return false;
    }

    const stageRect = stage.getBoundingClientRect();
    const transformRect = transform.getBoundingClientRect();
    return transformRect.width > stageRect.width + 1
      || transformRect.height > stageRect.height + 1;
  }, undefined, { timeout: 10_000 });
}

async function measureViewerDragPan(page) {
  const stage = page.getByTestId("viewer-stage");
  await stage.hover();
  await page.getByTestId("viewer-view-actual").evaluate((button) => button.click());
  await expect(page.getByTestId("viewer-view-actual")).toHaveClass(/is-active/);
  await waitForViewerActualSizePan(page);

  const transform = page.getByTestId("viewer-stage-transform");
  const transformBeforePan = await transform.getAttribute("style");
  const stageBox = await stage.boundingBox();
  expect(stageBox, "viewer stage box before drag-pan measurement").toBeTruthy();

  const startX = stageBox.x + stageBox.width * 0.58;
  const startY = stageBox.y + stageBox.height * 0.52;
  const endX = stageBox.x + stageBox.width * 0.32;
  const endY = stageBox.y + stageBox.height * 0.34;
  const steps = 48;

  await page.mouse.move(startX, startY);
  await beginDragFrameMetrics(page);
  await page.mouse.down();

  for (let step = 1; step <= steps; step += 1) {
    const progress = step / steps;
    await page.mouse.move(
      startX + ((endX - startX) * progress),
      startY + ((endY - startY) * progress)
    );
    await page.waitForTimeout(8);
  }

  await page.mouse.up();
  const dragMetrics = await finishDragFrameMetrics(page);
  await expect.poll(() => transform.getAttribute("style")).not.toBe(transformBeforePan);

  await page.getByTestId("viewer-reset").evaluate((button) => button.click());
  await expect(page.getByTestId("viewer-reset")).toHaveClass(/is-active/);
  await waitForViewerFit(page);

  return {
    ...dragMetrics,
    transformChanged: true
  };
}

async function openViewerWithPhaseTimings(page) {
  const timings = {};
  const trigger = page.getByTestId("post-details-open-viewer-hero");
  await expect(trigger).toBeVisible();
  await trigger.scrollIntoViewIfNeeded();

  const start = performance.now();

  await trigger.evaluate((button) => button.click());
  timings.clickDispatchedMs = Math.round(performance.now() - start);
  await expect(page.getByTestId("rich-image-viewer-modal")).toBeVisible();
  timings.modalVisibleMs = Math.round(performance.now() - start);

  await expect(page.getByTestId("viewer-close")).toBeVisible();
  await expect(page.getByTestId("viewer-stage")).toBeVisible();
  await expect(page.getByTestId("viewer-side-panel")).toBeVisible();
  timings.chromeVisibleMs = Math.round(performance.now() - start);

  await expect(page.getByTestId("viewer-stage-image")).toBeVisible();
  timings.imageVisibleMs = Math.round(performance.now() - start);

  await waitForDecodedViewerImage(page);
  timings.imageDecodedMs = Math.round(performance.now() - start);

  await waitForViewerFit(page);
  timings.fitReadyMs = Math.round(performance.now() - start);
  timings.openMs = Math.round(performance.now() - start);

  return timings;
}

test("rich viewer interaction performance budgets", async ({ page, request }, testInfo) => {
  test.setTimeout(180_000);

  await resetAppState(request);
  await loginAsSeedUser(page, request);
  const viewerPost = await createRichViewerPost(page, request, "perf", { extraPlainCommentsCount: 10 });
  const results = [];

  for (const viewport of VIEWPORTS) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await page.goto(`/posts/${viewerPost.postId}`);
    await page.waitForLoadState("networkidle");

    const openTimings = await openViewerWithPhaseTimings(page);
    const openMs = openTimings.openMs;

    const metricsAfterOpen = await collectViewerMetrics(page);
    console.log(`${viewport.name} viewer open timings: ${JSON.stringify(openTimings)}`);
    expect(openMs, `${viewport.name} viewer open time`).toBeLessThanOrEqual(BUDGET.openMs);
    expect(metricsAfterOpen.backdropBackground, `${viewport.name} backdrop must not bind image URLs`).not.toContain("url(");
    expect(metricsAfterOpen.backdropFilter, `${viewport.name} backdrop filter`).toBe("none");
    expect(metricsAfterOpen.maxThumbnailAspectSkew, `${viewport.name} thumbnail aspect skew`).toBeLessThanOrEqual(BUDGET.thumbnailMaxAspectSkew);

    const warmSwitchMs = await switchViewerImage(page);
    const cachedSwitchMs = await switchViewerImage(page, "viewer-stage-prev");
    expect(warmSwitchMs, `${viewport.name} warm image switch time`).toBeLessThanOrEqual(BUDGET.warmSwitchMs);
    expect(cachedSwitchMs, `${viewport.name} cached image switch time`).toBeLessThanOrEqual(BUDGET.cachedSwitchMs);

    const dragPan = await measureViewerDragPan(page);
    console.log(`${viewport.name} viewer drag-pan metrics: ${JSON.stringify(dragPan)}`);
    expect(dragPan.p95FrameMs, `${viewport.name} drag-pan p95 frame time`).toBeLessThanOrEqual(BUDGET.dragPanP95FrameMs);
    expect(dragPan.maxFrameMs, `${viewport.name} drag-pan max frame time`).toBeLessThanOrEqual(BUDGET.dragPanMaxFrameMs);
    expect(dragPan.droppedFramesOver34Ms, `${viewport.name} drag-pan dropped frames >34ms`).toBeLessThanOrEqual(BUDGET.dragPanMaxDroppedFrames);
    expect(dragPan.longTasksOver50Ms.length, `${viewport.name} drag-pan long tasks`).toBeLessThanOrEqual(BUDGET.dragPanMaxLongTasks);

    const screenshotStart = performance.now();
    await page.getByTestId("rich-image-viewer").screenshot({
      animations: "disabled",
      timeout: BUDGET.screenshotMs
    });
    const screenshotMs = Math.round(performance.now() - screenshotStart);

    await page.getByTestId("viewer-side-tab-comments").click();
    await expect(page.getByTestId("viewer-comments-thread")).toBeVisible();

    await page.setViewportSize({ width: 390, height: 844 });
    await waitForDecodedViewerImage(page);
    await waitForViewerFit(page);
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await waitForDecodedViewerImage(page);
    await waitForViewerFit(page);

    const metricsAfterResize = await collectViewerMetrics(page);
    await page.getByTestId("viewer-close").click();
    await expect(page.getByTestId("rich-image-viewer-modal")).toBeHidden();

    results.push({
      viewport: viewport.name,
      openMs,
      openTimings,
      warmSwitchMs,
      cachedSwitchMs,
      dragPan,
      screenshotMs,
      afterOpen: metricsAfterOpen,
      afterResize: metricsAfterResize
    });
  }

  const metricsPath = testInfo.outputPath("viewer-performance-metrics.json");
  fs.writeFileSync(metricsPath, `${JSON.stringify(results, null, 2)}\n`, "utf8");
  console.log(`Viewer performance metrics written to ${metricsPath}`);
  console.log(JSON.stringify(results, null, 2));
});
