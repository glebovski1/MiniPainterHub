const fs = require("fs");
const { test, expect } = require("@playwright/test");

const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const BUDGET = require("../lighthouse/budget.json").scroll;
const SAMPLE_IMAGE = "/uploads/images/9_minis1.jpg";
const SAMPLE_PREVIEW = "/uploads/images/1021_3_download.jpeg";

async function resetAppState(request) {
  const response = await request.post("/api/test-support/reset", {
    headers: {
      "X-Test-Support-Token": RESET_TOKEN
    }
  });

  expect(response.ok(), `Reset failed with HTTP ${response.status()}`).toBeTruthy();
}

async function loginToken(request) {
  const response = await request.post("/api/auth/login", {
    data: {
      userName: "user",
      password: "User123!"
    }
  });

  expect(response.ok(), `Login API failed with HTTP ${response.status()}`).toBeTruthy();
  const payload = await response.json();
  expect(payload?.token, "Login API did not return a token.").toBeTruthy();
  return payload.token;
}

async function createPost(request, token, index, options = {}) {
  const content = options.content
    || `Scroll fixture ${index} includes enough text to keep cards realistic without adding artificial JavaScript work.`;
  const response = await request.post("/api/posts", {
    headers: {
      Authorization: `Bearer ${token}`
    },
    data: {
      title: options.title || `Scroll fixture ${index}`,
      content,
      tags: ["scroll", "performance"],
      images: [
        {
          imageUrl: SAMPLE_IMAGE,
          previewUrl: SAMPLE_PREVIEW,
          thumbnailUrl: SAMPLE_PREVIEW,
          width: 1200,
          height: 900
        }
      ]
    }
  });

  expect(response.ok(), `Create post failed with HTTP ${response.status()}`).toBeTruthy();
  return response.json();
}

async function seedScrollPosts(request) {
  const token = await loginToken(request);
  const created = [];

  for (let index = 0; index < 18; index += 1) {
    created.push(await createPost(request, token, index + 1));
  }

  const longContent = Array.from({ length: 18 }, (_, index) =>
    `Weathering note ${index + 1}: layered scratches, matte dust, and small edge highlights should remain readable while the page scrolls.`
  ).join("\n\n");

  const detailPost = await createPost(request, token, 99, {
    title: "Long scroll detail fixture",
    content: longContent
  });

  return { created, detailPost };
}

async function measureFrameCadence(page) {
  return page.evaluate(async () => {
    const frames = [];
    let last = performance.now();

    await new Promise((resolve) => {
      const tick = (now) => {
        frames.push(now - last);
        last = now;

        if (frames.length >= 40) {
          resolve();
          return;
        }

        requestAnimationFrame(tick);
      };

      requestAnimationFrame(tick);
    });

    const sorted = frames.slice(1).sort((left, right) => left - right);
    const p95Index = Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95));

    return {
      p95FrameMs: sorted[p95Index] || 0,
      maxFrameMs: Math.max(...sorted, 0)
    };
  });
}

async function measureWindowScroll(page, label) {
  await page.waitForFunction(() =>
    Array.from(document.images).every((image) =>
      image instanceof HTMLImageElement
        && image.complete
        && image.naturalWidth > 0
        && image.naturalHeight > 0
    )
  );
  await page.evaluate(async () => {
    await document.fonts?.ready;
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.waitForTimeout(250);
  const idleFrameCadence = await measureFrameCadence(page);

  const measurement = page.evaluate(async ({ durationMs, minScrollDelta }) => {
    const root = document.scrollingElement || document.documentElement;
    const startTop = root.scrollTop;
    const frames = [];
    const longTasks = [];
    let observer;

    if ("PerformanceObserver" in window) {
      try {
        observer = new PerformanceObserver((list) => {
          for (const entry of list.getEntries()) {
            longTasks.push({ duration: entry.duration, startTime: entry.startTime });
          }
        });
        observer.observe({ type: "longtask" });
      } catch {
      }
    }

    let last = performance.now();
    const end = last + durationMs;
    const done = new Promise((resolve) => {
      const tick = (now) => {
        frames.push(now - last);
        last = now;
        if (now < end) {
          requestAnimationFrame(tick);
          return;
        }

        observer?.disconnect();
        const sorted = frames.slice().sort((left, right) => left - right);
        const p95Index = Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95));
        resolve({
          frameCount: frames.length,
          p95FrameMs: sorted[p95Index] || 0,
          maxFrameMs: Math.max(...frames, 0),
          framesOver20Ms: frames.filter((frame) => frame > 20).length,
          framesOver34Ms: frames.filter((frame) => frame > 34).length,
          slowestFrames: sorted.slice(-8),
          droppedFrames: frames.filter((frame) => frame > 34).length,
          longTasks: longTasks.filter((entry) => entry.duration > 50),
          scrollDelta: Math.abs(root.scrollTop - startTop),
          minScrollDelta,
          domElements: document.querySelectorAll("*").length,
          imageElements: document.images.length,
          scrollHeight: root.scrollHeight,
          clientHeight: root.clientHeight
        });
      };

      requestAnimationFrame(tick);
    });

    return done;
  }, { durationMs: 2200, minScrollDelta: BUDGET.minScrollDelta });

  for (let index = 0; index < 90; index += 1) {
    await page.mouse.wheel(0, 95);
    await page.waitForTimeout(16);
  }

  const metrics = await measurement;
  return { label, idleFrameCadence, ...metrics };
}

function assertScrollBudget(metrics) {
  const p95FrameBudget = Math.max(
    BUDGET.p95FrameMs,
    metrics.idleFrameCadence.p95FrameMs + BUDGET.frameCadenceToleranceMs
  );

  expect(metrics.scrollHeight, `${metrics.label} should be scrollable`).toBeGreaterThan(metrics.clientHeight);
  expect(metrics.scrollDelta, `${metrics.label} scroll delta`).toBeGreaterThanOrEqual(metrics.minScrollDelta);
  expect(metrics.p95FrameMs, `${metrics.label} p95 frame`).toBeLessThanOrEqual(p95FrameBudget);
  expect(metrics.maxFrameMs, `${metrics.label} max frame`).toBeLessThanOrEqual(BUDGET.maxFrameMs);
  expect(metrics.droppedFrames, `${metrics.label} dropped frames`).toBeLessThanOrEqual(BUDGET.maxDroppedFrames);
  expect(metrics.longTasks.length, `${metrics.label} long tasks`).toBeLessThanOrEqual(BUDGET.maxLongTasks);
}

test("public feed and details scroll within frame budgets", async ({ page, request }, testInfo) => {
  test.setTimeout(180_000);

  await resetAppState(request);
  const { detailPost } = await seedScrollPosts(request);
  await page.setViewportSize({ width: 2560, height: 1440 });

  await page.goto("/");
  await expect(page.locator(".post-card").first()).toBeVisible();
  const homeMetrics = await measureWindowScroll(page, "wasm-home");
  console.log(JSON.stringify(homeMetrics, null, 2));
  assertScrollBudget(homeMetrics);

  await page.goto(`/posts/${detailPost.id}`);
  await expect(page.getByTestId("post-title")).toHaveText("Long scroll detail fixture");
  const detailsMetrics = await measureWindowScroll(page, "wasm-post-details");
  console.log(JSON.stringify(detailsMetrics, null, 2));
  assertScrollBudget(detailsMetrics);

  const metrics = [homeMetrics, detailsMetrics];
  const metricsPath = testInfo.outputPath("scroll-performance-metrics.json");
  fs.writeFileSync(metricsPath, `${JSON.stringify(metrics, null, 2)}\n`, "utf8");
  console.log(`Scroll performance metrics written to ${metricsPath}`);
  console.log(JSON.stringify(metrics, null, 2));
});
