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
    expect(cachedSwitchMs, `${viewport.name} cached image switch time`).toBeLessThanOrEqual(BUDGET.cachedSwitchMs);

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
