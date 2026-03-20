const { test, expect } = require("@playwright/test");
const path = require("path");
const { createRichViewerPost, getPathSegment, openViewerFromDetails } = require("./helpers/viewer-scenario");

test.describe.configure({ mode: "serial" });
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const SAMPLE_IMAGE_PATH = path.resolve(
  __dirname,
  "../../MiniPainterHub.Server/wwwroot/uploads/images/9_minis1.jpg",
);
const VIEWER_RATIO_EXPECTATIONS = [
  { key: "portrait916", ratio: 9 / 16 },
  { key: "portrait23", ratio: 2 / 3 },
  { key: "square", ratio: 1 },
  { key: "landscape43", ratio: 4 / 3 },
  { key: "wide169", ratio: 16 / 9 },
  { key: "panorama219", ratio: 21 / 9 },
];

function expectWithinTolerance(actual, expected, tolerance = 4) {
  expect(Math.abs(actual - expected)).toBeLessThanOrEqual(tolerance);
}

async function getViewerBoxes(page) {
  const [stageSurfaceBox, stageBox, fitBox, imageBox] = await Promise.all([
    page.locator(".viewer-shell__stage-surface").boundingBox(),
    page.getByTestId("viewer-stage").boundingBox(),
    page.getByTestId("viewer-stage-fitbox").boundingBox(),
    page.getByTestId("viewer-stage-image").boundingBox(),
  ]);

  expect(stageSurfaceBox).toBeTruthy();
  expect(stageBox).toBeTruthy();
  expect(fitBox).toBeTruthy();
  expect(imageBox).toBeTruthy();

  return { stageSurfaceBox, stageBox, fitBox, imageBox };
}

async function getScrollLockState(page) {
  return page.evaluate(() => ({
    bodyOverflow: document.body.style.overflow,
    bodyPaddingRight: document.body.style.paddingRight,
    documentOverflow: document.documentElement.style.overflow,
  }));
}

async function expectViewerScrollLocked(page) {
  await expect.poll(async () => (await getScrollLockState(page)).bodyOverflow).toBe("hidden");
  await expect.poll(async () => (await getScrollLockState(page)).documentOverflow).toBe("hidden");
}

async function expectViewerScrollReleased(page) {
  await expect.poll(async () => (await getScrollLockState(page)).bodyOverflow).toBe("");
  await expect.poll(async () => (await getScrollLockState(page)).bodyPaddingRight).toBe("");
  await expect.poll(async () => (await getScrollLockState(page)).documentOverflow).toBe("");
}

async function resetAppState(request) {
  const response = await request.post("/api/test-support/reset", {
    headers: {
      "X-Test-Support-Token": RESET_TOKEN,
    },
  });
  expect(response.ok(), `Reset failed with HTTP ${response.status()}`).toBeTruthy();
}

async function clearAuth(page) {
  await page.goto("/");
  await page.evaluate(() => localStorage.removeItem("authToken"));
}

async function loginAsSeedUser(page) {
  await page.goto("/login");
  await page.getByTestId("login-username").fill("user");
  await page.getByTestId("login-password").fill("User123!");
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("nav-logout")).toBeVisible();
}

async function loginAsAdmin(page) {
  await page.goto("/login");
  await page.getByTestId("login-username").fill("admin");
  await page.getByTestId("login-password").fill("P@ssw0rd!");
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("nav-logout")).toBeVisible();
}

async function loginViaApi(request, userName, password) {
  const response = await request.post("/api/auth/login", {
    data: {
      userName,
      password,
    },
  });

  expect(response.ok(), `Login API failed with HTTP ${response.status()}`).toBeTruthy();
  const payload = await response.json();
  expect(payload?.token, "Login API did not return a token.").toBeTruthy();
  return payload.token;
}

async function sendAuthedRequest(request, token, method, url, data) {
  const response = await request.fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data,
  });

  expect(response.ok(), `${method} ${url} failed with HTTP ${response.status()}`).toBeTruthy();
  return response;
}

async function ensureProfileForToken(request, token, details) {
  let response = await request.fetch("/api/profiles/me", {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  if (response.status() === 404) {
    await sendAuthedRequest(request, token, "POST", "/api/profiles/me", details);
    response = await request.fetch("/api/profiles/me", {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });
  }

  expect(response.ok(), `GET /api/profiles/me failed with HTTP ${response.status()}`).toBeTruthy();
  return response.json();
}

async function createPost(page, suffix, options = {}) {
  const title = `Smoke title ${suffix}`;
  const content = `Smoke content ${suffix}`;
  const tags = options.tags || "";

  await page.goto("/posts/new");
  await page.getByTestId("create-post-title").fill(title);
  await page.getByTestId("create-post-content").fill(content);
  if (tags) {
    await page.getByTestId("create-post-tags").fill(tags);
  }
  if (options.imagePath) {
    await page.getByTestId("create-post-images").setInputFiles(options.imagePath);
  }
  await page.getByTestId("create-post-submit").click();

  await expect(page).toHaveURL(/\/posts\/\d+$/);
  await expect(page.getByTestId("post-title")).toHaveText(title);

  return { title, content, tags };
}

test.beforeEach(async ({ page, request }) => {
  await resetAppState(request);
  await clearAuth(page);
});

test("login with seeded user succeeds", async ({ page }) => {
  await loginAsSeedUser(page);
  await expect(page).toHaveURL(/\/$/);
});

test("invalid login shows user-facing error", async ({ page }) => {
  await page.goto("/login");
  await page.getByTestId("login-username").fill("user");
  await page.getByTestId("login-password").fill("wrong-password");
  await page.getByTestId("login-submit").click();

  await expect(page.getByTestId("login-error")).toContainText("Invalid username or password.");
});

test("create post flow redirects to details and renders content", async ({ page }) => {
  await loginAsSeedUser(page);
  await createPost(page, "create");
});

test("seeded post with image and tags renders on feed and details", async ({ page }) => {
  await page.goto("/");

  const seededCard = page.locator(".card", { hasText: "Seeded: glazing check" }).first();
  const weatheringCard = page.locator(".card", { hasText: "Seeded: weathering notes" }).first();
  await expect(seededCard).toBeVisible();
  await expect(weatheringCard).toBeVisible();
  await expect(seededCard.getByTestId("post-card-image")).toBeVisible();
  await expect(seededCard.getByTestId("post-card-tags")).toContainText("#glazing");
  await expect(seededCard.getByTestId("post-card-tags")).toContainText("#nmm");
  await expect(weatheringCard.getByTestId("post-card-image")).toBeVisible();
  await expect(weatheringCard.getByTestId("post-card-tags")).toContainText("#weathering");
  await expect(weatheringCard.getByTestId("post-card-tags")).toContainText("#battle-damage");

  await seededCard.getByRole("link", { name: "Seeded: glazing check" }).click();

  await expect(page).toHaveURL(/\/posts\/\d+$/);
  await expect(page.getByTestId("post-title")).toHaveText("Seeded: glazing check");
  await expect(page.getByTestId("post-details-image")).toBeVisible();
  await expect(page.getByTestId("post-details-tags")).toContainText("#glazing");
  await expect(page.getByTestId("post-details-tags")).toContainText("#nmm");
});

test("create post with image and tags renders on details and latest feed", async ({ page }) => {
  await loginAsSeedUser(page);
  const { title } = await createPost(page, "image-tags", {
    tags: "glazing, showcase",
    imagePath: SAMPLE_IMAGE_PATH,
  });

  await expect(page.getByTestId("post-details-image")).toBeVisible();
  await expect(page.getByTestId("post-details-tags")).toContainText("#glazing");
  await expect(page.getByTestId("post-details-tags")).toContainText("#showcase");

  await page.goto("/");

  const createdCard = page.locator(".card", { hasText: title }).first();
  await expect(createdCard).toBeVisible();
  await expect(createdCard.getByTestId("post-card-image")).toBeVisible();
  await expect(createdCard.getByTestId("post-card-tags")).toContainText("#glazing");
  await expect(createdCard.getByTestId("post-card-tags")).toContainText("#showcase");
});

test("comment and like flow works on post details", async ({ page }) => {
  await loginAsSeedUser(page);
  await createPost(page, "engagement");

  const commentText = "This is a smoke comment.";
  await page.getByTestId("comment-input").fill(commentText);
  await page.getByTestId("comment-submit").click();
  await expect(page.getByTestId("comment-item").first()).toContainText(commentText);

  const likeButton = page.getByTestId("post-like-toggle");
  const likeCount = page.getByTestId("post-like-toggle-count");

  const initial = Number.parseInt((await likeCount.innerText()).trim(), 10);

  await likeButton.click();
  await expect(likeCount).toHaveText(String(initial + 1));

  await likeButton.click();
  await expect(likeCount).toHaveText(String(initial));
});

test("rich viewer overlay keeps post details intact and supports refined layout modes", async ({ page, request }) => {
  await loginAsSeedUser(page);
  const viewerPost = await createRichViewerPost(page, request, "desktop-flow", { extraPlainCommentsCount: 8 });
  const squareImagePath = getPathSegment(new URL(viewerPost.squareImage.imageUrl, page.url()).toString(), "/uploads/images/");
  const secondaryImagePath = getPathSegment(new URL(viewerPost.secondaryImage.imageUrl, page.url()).toString(), "/uploads/images/");
  const panoramaImagePath = getPathSegment(new URL(viewerPost.panoramaImage.imageUrl, page.url()).toString(), "/uploads/images/");
  const panoramaIndex = viewerPost.viewer.images.findIndex((image) => image.id === viewerPost.panoramaImage.id);

  const commentMarkRequests = [];
  page.on("request", (requestInfo) => {
    if (requestInfo.method() === "GET" && /\/api\/comments\/\d+\/mark/.test(requestInfo.url())) {
      commentMarkRequests.push(requestInfo.url());
    }
  });

  await expect(page.getByTestId("post-title")).toHaveText(viewerPost.title);
  await expect(page.getByTestId("post-details-image")).toBeVisible();
  await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);

  await openViewerFromDetails(page);
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(1);
  await expect(page.getByTestId("viewer-comment-mark")).toHaveCount(0);
  await expect(page.getByTestId("viewer-side-tab-info")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-panel-info")).toContainText("About this piece");
  await expect(page.getByTestId("viewer-control-rail")).toBeVisible();
  await expect(page.getByTestId("viewer-stage")).toBeVisible();
  await expect(page.getByTestId("viewer-thumbnail-rail")).toBeVisible();
  await expect(page.getByTestId("viewer-close")).toBeVisible();
  await expectViewerScrollLocked(page);

  const stage = page.getByTestId("viewer-stage");
  const stageImage = page.getByTestId("viewer-stage-image");
  const toolbarStatus = page.locator(".viewer-toolbar__status");
  const controlRail = page.getByTestId("viewer-control-rail");
  const sidePanel = page.getByTestId("viewer-side-panel");
  const modal = page.getByTestId("rich-image-viewer");

  const [railBox, panelBox, modalBox] = await Promise.all([
    controlRail.boundingBox(),
    sidePanel.boundingBox(),
    modal.boundingBox(),
  ]);
  expect(railBox).toBeTruthy();
  expect(panelBox).toBeTruthy();
  expect(modalBox).toBeTruthy();
  expect(panelBox.x + panelBox.width).toBeLessThanOrEqual(modalBox.x + modalBox.width + 1);

  let { stageSurfaceBox, stageBox, fitBox, imageBox } = await getViewerBoxes(page);
  expect(stageBox.width).toBeGreaterThan(panelBox.width);
  expect(railBox.x + railBox.width).toBeLessThanOrEqual(stageBox.x + 1);
  expect(stageBox.x + stageBox.width).toBeLessThanOrEqual(panelBox.x + 1);
  expectWithinTolerance(fitBox.x - stageBox.x, (stageBox.width - fitBox.width) / 2, 4);
  expectWithinTolerance(fitBox.y - stageBox.y, (stageBox.height - fitBox.height) / 2, 4);
  expectWithinTolerance(imageBox.x - stageBox.x, (stageBox.width - imageBox.width) / 2, 4);
  expectWithinTolerance(imageBox.y - stageBox.y, (stageBox.height - imageBox.height) / 2, 4);
  expectWithinTolerance(fitBox.height, stageBox.height, 4);
  const fitArea = fitBox.width * fitBox.height;

  await page.getByTestId("viewer-view-fill").click();
  await expect(page.getByTestId("viewer-view-fill")).toHaveClass(/is-active/);
  await page.waitForTimeout(220);
  ({ stageSurfaceBox, stageBox, fitBox, imageBox } = await getViewerBoxes(page));
  expect(fitBox.width * fitBox.height).toBeGreaterThan(fitArea * 1.2);
  expect(fitBox.x).toBeLessThanOrEqual(stageBox.x + 4);
  expect(fitBox.y).toBeLessThanOrEqual(stageBox.y + 4);
  expect(fitBox.x + fitBox.width).toBeGreaterThanOrEqual(stageBox.x + stageBox.width - 4);
  expect(fitBox.y + fitBox.height).toBeGreaterThanOrEqual(stageBox.y + stageBox.height - 4);
  expect(imageBox.height).toBeGreaterThanOrEqual(stageBox.height - 4);

  await page.getByTestId("viewer-view-actual").click();
  await expect(page.getByTestId("viewer-view-actual")).toHaveClass(/is-active/);
  await expect(toolbarStatus).toContainText("100%");
  await page.waitForTimeout(220);
  const transform = page.locator(".viewer-stage__transform");
  const transformBeforePan = await transform.getAttribute("style");
  ({ stageSurfaceBox, stageBox } = await getViewerBoxes(page));
  await page.mouse.move(stageBox.x + stageBox.width * 0.55, stageBox.y + stageBox.height * 0.45);
  await page.mouse.down();
  await page.mouse.move(stageBox.x + stageBox.width * 0.4, stageBox.y + stageBox.height * 0.3, { steps: 8 });
  await page.mouse.up();
  await expect.poll(() => transform.getAttribute("style")).not.toBe(transformBeforePan);

  await page.getByTestId("viewer-reset").click();
  await expect(page.getByTestId("viewer-reset")).toHaveClass(/is-active/);
  await page.waitForTimeout(220);
  ({ stageSurfaceBox, stageBox, fitBox, imageBox } = await getViewerBoxes(page));
  expectWithinTolerance(fitBox.x - stageBox.x, (stageBox.width - fitBox.width) / 2, 4);
  expectWithinTolerance(fitBox.y - stageBox.y, (stageBox.height - fitBox.height) / 2, 4);
  expectWithinTolerance(imageBox.x - stageBox.x, (stageBox.width - imageBox.width) / 2, 4);
  expectWithinTolerance(imageBox.y - stageBox.y, (stageBox.height - imageBox.height) / 2, 4);

  const expandedRailBox = await controlRail.boundingBox();
  const expandedStageBox = await stage.boundingBox();
  await page.getByTestId("viewer-rail-toggle").click();
  await expect(page.getByTestId("viewer-thumbnail-rail")).toHaveCount(0);
  await expect.poll(async () => {
    const box = await stage.boundingBox();
    return box ? box.width : 0;
  }).toBeGreaterThan(expandedStageBox.width + 20);
  const compactRailBox = await controlRail.boundingBox();
  const collapsedStageBox = await stage.boundingBox();
  expect(compactRailBox.width).toBeLessThan(expandedRailBox.width);
  expect(collapsedStageBox.width).toBeGreaterThan(expandedStageBox.width + 20);

  await page.getByTestId("viewer-side-tab-comments").click();
  await expect(page.getByTestId("viewer-side-tab-comments")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-comments-scroll")).toBeVisible();
  const commentsScroll = page.getByTestId("viewer-comments-scroll");
  const composerSticky = page.getByTestId("viewer-composer-sticky");
  const composerBoxBeforeScroll = await composerSticky.boundingBox();
  const scrollMetrics = await commentsScroll.evaluate((element) => ({
    clientHeight: element.clientHeight,
    scrollHeight: element.scrollHeight,
  }));
  expect(scrollMetrics.scrollHeight).toBeGreaterThan(scrollMetrics.clientHeight);
  await commentsScroll.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
  });
  await page.waitForTimeout(160);
  const scrollTop = await commentsScroll.evaluate((element) => element.scrollTop);
  const composerBoxAfterScroll = await composerSticky.boundingBox();
  expect(scrollTop).toBeGreaterThan(0);
  expect(composerBoxBeforeScroll).toBeTruthy();
  expect(composerBoxAfterScroll).toBeTruthy();
  expectWithinTolerance(composerBoxAfterScroll.y, composerBoxBeforeScroll.y, 2);
  await page.getByTestId("viewer-side-tab-info").click();
  await expect(page.getByTestId("viewer-side-tab-info")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-panel-info")).toBeVisible();

  await page.getByTestId("viewer-close").click();
  await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
  await expectViewerScrollReleased(page);

  const pageComment = page
    .locator("[data-testid='comment-list-container']")
    .first()
    .getByTestId("comment-item")
    .filter({ hasText: "Portrait follow-up anchor sits on the second portrait image and should switch the viewer there cleanly." })
    .first();
  await pageComment.getByTestId("comment-show-mark").click();
  await expect(page.getByTestId("viewer-side-tab-comments")).toHaveClass(/is-active/);
  await expect.poll(() => commentMarkRequests.length).toBe(1);
  await expect.poll(() => stageImage.getAttribute("src")).toContain(secondaryImagePath);
  await expect(page.getByTestId("viewer-comment-mark")).toBeVisible();
  await expect(page.getByTestId("viewer-comment-state")).toContainText(`#${viewerPost.markedCommentOne.id}`);

  const squareComment = page
    .getByTestId("viewer-side-panel")
    .getByTestId("comment-item")
    .filter({ hasText: "Square image anchor should stay centered while the comment panel highlights this thread." })
    .first();
  await squareComment.click();
  await expect.poll(() => commentMarkRequests.length).toBe(2);
  await expect.poll(() => stageImage.getAttribute("src")).toContain(squareImagePath);
  await expect(page.getByTestId("viewer-comment-state")).toContainText(`#${viewerPost.markedCommentTwo.id}`);

  await page.getByTestId("viewer-thumbnail").nth(panoramaIndex).click({ force: true });
  await expect.poll(() => stageImage.getAttribute("src")).toContain(panoramaImagePath);
  await expect(page.getByTestId("viewer-comment-mark")).toHaveCount(0);
  await expect(page.getByTestId("viewer-comment-state")).toHaveCount(0);

  await page.getByTestId("viewer-reset").click();
  await page.setViewportSize({ width: 1320, height: 760 });
  await page.waitForTimeout(260);
  ({ stageSurfaceBox, stageBox, fitBox, imageBox } = await getViewerBoxes(page));
  expectWithinTolerance(fitBox.x - stageBox.x, (stageBox.width - fitBox.width) / 2, 4);
  expectWithinTolerance(fitBox.y - stageBox.y, (stageBox.height - fitBox.height) / 2, 4);
  expectWithinTolerance(imageBox.x - stageBox.x, (stageBox.width - imageBox.width) / 2, 4);
  expectWithinTolerance(imageBox.y - stageBox.y, (stageBox.height - imageBox.height) / 2, 4);

  const fullscreenButton = page.getByTestId("viewer-fullscreen");
  if (await fullscreenButton.isVisible()) {
    await fullscreenButton.click();
    await expect.poll(() => page.evaluate(() => Boolean(document.fullscreenElement))).toBeTruthy();
    const fullscreenShellBox = await modal.boundingBox();
    const windowSize = await page.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight }));
    expect(fullscreenShellBox).toBeTruthy();
    expectWithinTolerance(fullscreenShellBox.width, windowSize.width, 3);
    expectWithinTolerance(fullscreenShellBox.height, windowSize.height, 3);
    await page.getByTestId("viewer-close").click();
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
    await expect.poll(() => page.evaluate(() => Boolean(document.fullscreenElement))).toBeFalsy();
    await expectViewerScrollReleased(page);
    await expect(page.getByTestId("post-details-image")).toBeVisible();
    return;
  }

  await page.getByTestId("viewer-close").click();
  await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
  await expectViewerScrollReleased(page);
  await expect(page.getByTestId("post-details-image")).toBeVisible();
});

test("rich viewer preserves image fit across the aspect-ratio matrix", async ({ page, request }) => {
  await loginAsSeedUser(page);
  const viewerPost = await createRichViewerPost(page, request, "ratio-matrix");

  await openViewerFromDetails(page);
  const expectedRatios = VIEWER_RATIO_EXPECTATIONS
    .map((expectation) => Number(expectation.ratio.toFixed(3)))
    .sort((left, right) => left - right);
  const observedRatios = [];
  const observedSources = new Set();

  for (let index = 0; index < viewerPost.viewer.images.length; index += 1) {
    if (index > 0) {
      const previousSrc = await page.getByTestId("viewer-stage-image").getAttribute("src");
      await page.getByTestId("viewer-next").click();
      await expect.poll(() => page.getByTestId("viewer-stage-image").getAttribute("src")).not.toBe(previousSrc);
    }

    await expect(page.getByTestId("viewer-comment-mark")).toHaveCount(0);

    const currentSrc = await page.getByTestId("viewer-stage-image").getAttribute("src");
    observedSources.add(currentSrc);

    const [stageBox, fitBox, imageBox] = await Promise.all([
      page.getByTestId("viewer-stage").boundingBox(),
      page.getByTestId("viewer-stage-fitbox").boundingBox(),
      page.getByTestId("viewer-stage-image").boundingBox(),
    ]);
    expect(stageBox).toBeTruthy();
    expect(fitBox).toBeTruthy();
    expect(imageBox).toBeTruthy();
    expect(fitBox.x).toBeGreaterThanOrEqual(stageBox.x - 1);
    expect(fitBox.y).toBeGreaterThanOrEqual(stageBox.y - 1);
    expect(fitBox.x + fitBox.width).toBeLessThanOrEqual(stageBox.x + stageBox.width + 1);
    expect(fitBox.y + fitBox.height).toBeLessThanOrEqual(stageBox.y + stageBox.height + 1);
    expectWithinTolerance(imageBox.width, fitBox.width, 2);
    expectWithinTolerance(imageBox.height, fitBox.height, 2);
    expectWithinTolerance(
      fitBox.x - stageBox.x,
      (stageBox.width - fitBox.width) / 2,
      4,
    );
    expectWithinTolerance(
      fitBox.y - stageBox.y,
      (stageBox.height - fitBox.height) / 2,
      4,
    );
    expect(fitBox.width).toBeGreaterThan(0);
    expect(fitBox.height).toBeGreaterThan(0);
    observedRatios.push(Number((fitBox.width / fitBox.height).toFixed(3)));
  }

  expect(observedSources.size).toBe(VIEWER_RATIO_EXPECTATIONS.length);
  expect(observedRatios.sort((left, right) => left - right)).toEqual(expectedRatios);
});

test("author note controls are author-only and author marks can be created then deleted", async ({ page, request }) => {
  await loginAsSeedUser(page);
  const viewerPost = await createRichViewerPost(page, request, "author-controls");

  await openViewerFromDetails(page);
  await expect(page.getByTestId("viewer-add-note")).toBeVisible();

  await page.getByTestId("viewer-add-note").click();
  const [stageBox, fitBox] = await Promise.all([
    page.getByTestId("viewer-stage").boundingBox(),
    page.getByTestId("viewer-stage-fitbox").boundingBox(),
  ]);
  expect(stageBox).toBeTruthy();
  expect(fitBox).toBeTruthy();
  await page.getByTestId("viewer-stage").click({
    position: {
      x: Math.round((fitBox.x - stageBox.x) + (fitBox.width * 0.52)),
      y: Math.round((fitBox.y - stageBox.y) + (fitBox.height * 0.42)),
    },
  });

  await expect(page.getByTestId("viewer-mark-composer")).toBeVisible();
  await page.getByTestId("viewer-mark-tag").fill("edge glow");
  await page.getByTestId("viewer-mark-message").fill("Secondary edge light is here to verify author-only note mutation from the overlay.");
  await page.getByTestId("viewer-mark-composer").locator("form").evaluate((form) => form.requestSubmit());
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(2);
  await expect(page.getByTestId("viewer-mark-delete")).toBeVisible();
  await page.getByTestId("viewer-mark-delete").evaluate((button) => button.click());
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(1);

  await clearAuth(page);
  await loginAsAdmin(page);
  await page.goto(`/posts/${viewerPost.postId}`);
  await openViewerFromDetails(page);
  await expect(page.getByTestId("viewer-add-note")).toHaveCount(0);
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(1);
});

test("rich viewer mobile layout plus loading and error states work", async ({ page, request }) => {
  await loginAsSeedUser(page);
  const viewerPost = await createRichViewerPost(page, request, "mobile-flow");

  const delayedImagePattern = new RegExp(viewerPost.primaryImage.imageUrl.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
  await page.route(delayedImagePattern, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 900));
    await route.continue();
  });

  await openViewerFromDetails(page, { waitForImage: false });
  await expect(page.getByTestId("viewer-skeleton")).toHaveCount(1);
  await expect(page.getByTestId("viewer-skeleton")).toHaveCount(0);
  await page.unroute(delayedImagePattern);

  const failingImagePattern = /\/uploads\/images\/.*_max\.(webp|jpg|png)$/;
  await page.route(failingImagePattern, async (route) => {
    if (route.request().url().includes(viewerPost.primaryImage.imageUrl)) {
      await route.continue();
      return;
    }

    await route.abort("failed");
  });

  await page.getByTestId("viewer-thumbnail").nth(1).click({ force: true });
  await expect(page.getByTestId("viewer-image-error")).toBeVisible();

  await page.unroute(failingImagePattern);
  await page.getByTestId("viewer-thumbnail").nth(0).click({ force: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(250);
  await expect(page.getByTestId("viewer-side-panel")).toBeVisible();
  await expect(page.getByTestId("viewer-control-rail")).toBeVisible();
  const [railBox, stageBox, panelBox] = await Promise.all([
    page.getByTestId("viewer-control-rail").boundingBox(),
    page.getByTestId("viewer-stage").boundingBox(),
    page.getByTestId("viewer-side-panel").boundingBox(),
  ]);
  expect(railBox).toBeTruthy();
  expect(stageBox).toBeTruthy();
  expect(panelBox).toBeTruthy();
  expect(stageBox.y).toBeGreaterThanOrEqual(railBox.y + railBox.height - 1);
  expect(stageBox.height).toBeGreaterThan(railBox.height);
  expect(stageBox.height).toBeGreaterThan(panelBox.height * 0.85);
  expectWithinTolerance(stageBox.x, panelBox.x, 6);
  expectWithinTolerance(stageBox.width, panelBox.width, 6);
});

test("search by title and tag works, and public profiles open from search", async ({ page }) => {
  await loginAsSeedUser(page);
  const { title } = await createPost(page, "search", { tags: "glazing" });

  await page.getByTestId("nav-search-input").fill(title);
  await page.getByTestId("nav-search-submit").click();

  await expect(page).toHaveURL(/\/search\?q=/);
  await expect(page.getByTestId("search-post-result").first()).toContainText(title);

  await page.getByTestId("tag-link").first().click();
  await expect(page).toHaveURL(/tab=posts/);
  await expect(page).toHaveURL(/tag=glazing/);
  await expect(
    page
      .getByTestId("search-post-result")
      .filter({ hasText: title })
      .first(),
  ).toBeVisible();

  await page.getByTestId("search-query-input").fill("user");
  await page.getByTestId("search-query-submit").click();
  await page.getByTestId("search-tab-users").click();
  await expect(page.getByTestId("search-user-result").first()).toBeVisible();
  await page.getByTestId("search-user-result").first().click();
  await expect(page).toHaveURL(/\/users\//);
});

test("profile create and update flow persists values", async ({ page }) => {
  await loginAsSeedUser(page);
  await page.goto("/profile");

  await expect
    .poll(async () => {
      if (await page.getByTestId("profile-edit").isVisible()) {
        return "edit";
      }

      if (await page.getByTestId("profile-create-submit").isVisible()) {
        return "create";
      }

      return "pending";
    })
    .not.toBe("pending");

  const createButton = page.getByTestId("profile-create-submit");
  if (await createButton.isVisible()) {
    await page.getByTestId("profile-create-display-name").fill("Smoke User");
    await page.getByTestId("profile-create-bio").fill("Created by smoke suite.");
    await createButton.click();
  }

  await expect(page.getByTestId("profile-edit")).toBeVisible();
  await page.getByTestId("profile-edit").click();

  const updatedName = "Smoke Updated";
  const updatedBio = "Updated profile by Playwright smoke.";

  await page.getByTestId("profile-display-name").fill(updatedName);
  await page.getByTestId("profile-bio").fill(updatedBio);
  await page.getByTestId("profile-save").click();

  await page.reload();
  await expect(page.getByTestId("profile-display-name")).toHaveValue(updatedName);
  await expect(page.getByTestId("profile-bio")).toHaveValue(updatedBio);
});

test("unauthenticated user cannot access create-post form", async ({ page }) => {
  await clearAuth(page);
  await page.goto("/posts/new");

  await expect(page.getByTestId("nav-login")).toBeVisible();
  await expect(page.getByTestId("create-post-form")).toHaveCount(0);
});

test("create post form shows validation messages when required fields are empty", async ({ page }) => {
  await loginAsSeedUser(page);
  await page.goto("/posts/new");
  await page.getByTestId("create-post-submit").click();

  await expect(page.getByText(/title field is required/i)).toBeVisible();
  await expect(page.getByText(/content field is required/i)).toBeVisible();
});

test("post details prompt sign-in for comments when session is cleared", async ({ page }) => {
  await loginAsSeedUser(page);
  await createPost(page, "comment-auth");
  const detailsUrl = page.url();

  await clearAuth(page);
  await page.goto(detailsUrl);

  await expect(page.getByText("Sign in to join the conversation.")).toBeVisible();
});

test("messages thread loads and sending works", async ({ page, request }) => {
  const adminToken = await loginViaApi(request, "admin", "P@ssw0rd!");
  const adminProfile = await ensureProfileForToken(request, adminToken, {
    displayName: "Admin Painter",
    bio: "Seeded for smoke coverage.",
  });

  await loginAsSeedUser(page);
  const userToken = await page.evaluate(() => localStorage.getItem("authToken"));
  expect(userToken, "Expected the page login flow to store an auth token.").toBeTruthy();

  const conversationResponse = await sendAuthedRequest(
    request,
    userToken,
    "POST",
    `/api/conversations/direct/${encodeURIComponent(adminProfile.userId)}`,
  );

  const conversation = await conversationResponse.json();
  expect(conversation?.id, "Expected the conversation API to return an id.").toBeTruthy();

  await page.goto(`/messages/${conversation.id}`);
  await expect(page.getByRole("heading", { name: "Admin Painter" })).toBeVisible();

  const messageBody = "Smoke DM from Playwright";
  await page.getByPlaceholder("Write a message...").fill(messageBody);
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.locator(".message-thread .message-bubble").filter({ hasText: messageBody }).last()).toBeVisible();
});

test("admin can hide and restore post using visibility filter and inline actions", async ({ page }) => {
  await loginAsAdmin(page);
  const { title } = await createPost(page, "admin-post-moderation");

  await page.getByTestId("post-inline-hide").click();

  await page.goto("/");
  await page.getByTestId("post-visibility-select").selectOption("hidden");

  const hiddenCard = page.locator(".card", { hasText: title }).first();
  await expect(hiddenCard.getByTestId("post-hidden-badge")).toBeVisible();
  await hiddenCard.getByTestId("post-card-restore").click();
  await expect(page.locator(".card", { hasText: title })).toHaveCount(0);
  await page.getByTestId("post-visibility-select").selectOption("active");
  await expect(page.locator(".card", { hasText: title }).first()).toBeVisible();
});

test("admin can hide and restore comment using comment visibility filter", async ({ page }) => {
  await loginAsAdmin(page);
  const { title } = await createPost(page, "admin-comment-moderation");
  const commentText = "Admin moderation smoke comment";

  await page.getByTestId("comment-input").fill(commentText);
  await page.getByTestId("comment-submit").click();
  await expect(page.getByTestId("comment-item").first()).toContainText(commentText);

  await page.getByTestId("comment-inline-hide").first().click();
  await page.getByTestId("comment-visibility-select").selectOption("hidden");

  const hiddenComment = page.getByTestId("comment-item").first();
  await expect(hiddenComment).toContainText(commentText);
  await expect(hiddenComment.getByTestId("comment-hidden-badge")).toBeVisible();
  await hiddenComment.getByTestId("comment-inline-restore").click();

  await page.getByTestId("comment-visibility-select").selectOption("active");
  await expect(page.getByTestId("comment-item").first()).toContainText(commentText);
});

test("user can submit a report and admin can resolve it", async ({ page }) => {
  await loginAsAdmin(page);
  const { title } = await createPost(page, "report-target");
  const postUrl = page.url();

  await clearAuth(page);
  await loginAsSeedUser(page);
  await page.goto(postUrl);

  await page.getByTestId("post-report-toggle").click();
  await page.getByTestId("post-report-submit").click();
  await expect(page.getByTestId("post-report-result")).toContainText("Report submitted");

  await clearAuth(page);
  await loginAsAdmin(page);
  await page.goto("/admin/reports");

  await expect(page.getByTestId("reports-row").first()).toContainText(title);
  await page.getByTestId("report-resolution-note").first().fill("Handled in smoke test.");
  await page.getByTestId("report-resolve-reviewed").first().click();
  await expect(page.getByTestId("reports-empty")).toBeVisible();
});
