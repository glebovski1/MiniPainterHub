const { test, expect } = require("@playwright/test");
const fs = require("fs");
const path = require("path");
const { createRichViewerPost, getPathSegment, openViewerFromDetails } = require("./helpers/viewer-scenario");

test.describe.configure({ mode: "serial" });
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const developmentConfig = JSON.parse(
  fs.readFileSync(
    path.resolve(__dirname, "../../MiniPainterHub.Server/appsettings.Development.json"),
    "utf8",
  ),
);
const seedCredentials = developmentConfig.DevelopmentSeedCredentials || {};
const USER_PASSWORD = requireSeedCredential("UserPassword", process.env.E2E_USER_PASSWORD || seedCredentials.UserPassword);
const ADMIN_PASSWORD = requireSeedCredential("AdminPassword", process.env.E2E_ADMIN_PASSWORD || seedCredentials.AdminPassword);
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

function requireSeedCredential(name, value) {
  if (!value) {
    throw new Error(`Missing e2e seed credential: ${name}`);
  }

  return value;
}

function expectWithinTolerance(actual, expected, tolerance = 4) {
  expect(Math.abs(actual - expected)).toBeLessThanOrEqual(tolerance);
}

function expectBoxContainedWithin(childBox, containerBox, label, tolerance = 2) {
  expect(childBox, `Expected ${label} to have a layout box.`).toBeTruthy();
  expect(containerBox, `Expected the ${label} container to have a layout box.`).toBeTruthy();
  expect(childBox.x, `Expected ${label} to stay inside its container horizontally.`).toBeGreaterThanOrEqual(containerBox.x - tolerance);
  expect(childBox.y, `Expected ${label} to stay inside its container vertically.`).toBeGreaterThanOrEqual(containerBox.y - tolerance);
  expect(childBox.x + childBox.width, `Expected ${label} to stay inside its container horizontally.`).toBeLessThanOrEqual(containerBox.x + containerBox.width + tolerance);
  expect(childBox.y + childBox.height, `Expected ${label} to stay inside its container vertically.`).toBeLessThanOrEqual(containerBox.y + containerBox.height + tolerance);
}

function expectBoxesNotOverlapping(firstBox, secondBox, firstLabel, secondLabel, tolerance = 2) {
  const overlaps = firstBox.x < (secondBox.x + secondBox.width - tolerance)
    && secondBox.x < (firstBox.x + firstBox.width - tolerance)
    && firstBox.y < (secondBox.y + secondBox.height - tolerance)
    && secondBox.y < (firstBox.y + firstBox.height - tolerance);

  expect(overlaps, `Expected ${firstLabel} and ${secondLabel} not to overlap.`).toBeFalsy();
}

async function expectViewerComposerLayout(page) {
  const sidePanel = page.getByTestId("viewer-side-panel");
  const composer = sidePanel.getByTestId("viewer-comment-composer");
  const input = composer.getByTestId("comment-input");
  const actions = composer.getByTestId("comment-actions");
  const attachMark = actions.getByTestId("comment-attach-mark");
  const submit = actions.getByTestId("comment-submit");

  await expect(composer).toBeVisible();
  await expect(input).toBeVisible();
  await expect(actions).toBeVisible();
  await expect(attachMark).toBeVisible();
  await expect(submit).toBeVisible();

  const [sidePanelBox, composerBox, inputBox, actionsBox, attachMarkBox, submitBox] = await Promise.all([
    sidePanel.boundingBox(),
    composer.boundingBox(),
    input.boundingBox(),
    actions.boundingBox(),
    attachMark.boundingBox(),
    submit.boundingBox(),
  ]);

  expectBoxContainedWithin(composerBox, sidePanelBox, "viewer comment composer");
  expectBoxContainedWithin(inputBox, composerBox, "viewer comment input");
  expectBoxContainedWithin(actionsBox, composerBox, "viewer comment actions");
  expectBoxContainedWithin(attachMarkBox, actionsBox, "attach mark control");
  expectBoxContainedWithin(submitBox, actionsBox, "post control");
  expectBoxesNotOverlapping(inputBox, actionsBox, "viewer comment input", "viewer comment actions");
  expectBoxesNotOverlapping(attachMarkBox, submitBox, "attach mark control", "post control");
  expect(actionsBox.y).toBeGreaterThanOrEqual(inputBox.y + inputBox.height - 2);
  expect(actionsBox.y - (inputBox.y + inputBox.height)).toBeLessThanOrEqual(24);
  const actionLayout = await actions.evaluate((element) => {
    const style = window.getComputedStyle(element);
    return {
      display: style.display,
      gridTemplateColumns: style.gridTemplateColumns,
      width: style.width,
    };
  });
  expect(Math.abs(attachMarkBox.y - submitBox.y), `Expected compact actions to share one row. Layout: ${JSON.stringify(actionLayout)}`).toBeLessThanOrEqual(2);
}

async function expectPageCommentComposerLayout(page) {
  const section = page.getByTestId("post-details-comments");
  const composer = page.getByTestId("post-details-comment-composer");
  const input = composer.getByTestId("comment-input");
  const actions = composer.getByTestId("comment-actions");
  const attachMark = actions.getByTestId("comment-attach-mark");
  const submit = actions.getByTestId("comment-submit");

  await expect(section).toBeVisible();
  await expect(composer).toBeVisible();
  await expect(input).toBeVisible();
  await expect(actions).toBeVisible();
  await expect(attachMark).toBeVisible();
  await expect(submit).toBeVisible();

  const [sectionBox, composerBox, inputBox, actionsBox, attachMarkBox, submitBox] = await Promise.all([
    section.boundingBox(),
    composer.boundingBox(),
    input.boundingBox(),
    actions.boundingBox(),
    attachMark.boundingBox(),
    submit.boundingBox(),
  ]);

  expectBoxContainedWithin(composerBox, sectionBox, "page comment composer");
  expectBoxContainedWithin(inputBox, composerBox, "page comment input");
  expectBoxContainedWithin(actionsBox, composerBox, "page comment actions");
  expectBoxContainedWithin(attachMarkBox, actionsBox, "page attach point control");
  expectBoxContainedWithin(submitBox, actionsBox, "page post comment control");
  expectBoxesNotOverlapping(inputBox, actionsBox, "page comment input", "page comment actions");
  expectBoxesNotOverlapping(attachMarkBox, submitBox, "page attach point control", "page post comment control");
}

async function expectViewerControlsHidden(page) {
  const stageSurface = page.locator(".viewer-shell__stage-surface");
  await expect(stageSurface).not.toHaveClass(/is-controls-visible/, { timeout: 2_400 });
  await expect(page.locator(".viewer-shell__stage-page-count")).toHaveCSS("opacity", "0");
  await expect(page.locator(".viewer-shell__stage-chrome .viewer-toolbar__top")).toHaveCSS("opacity", "0");
  await expect(page.getByTestId("viewer-rail-zoom")).toHaveCSS("opacity", "0");
  await expect(page.getByTestId("viewer-rail-utility")).toHaveCSS("opacity", "0");
}

async function expectAdminViewerFilterLayout(page) {
  const sidePanel = page.getByTestId("viewer-side-panel");
  const commentsScroll = sidePanel.getByTestId("viewer-comments-scroll");
  const composerSticky = sidePanel.getByTestId("viewer-composer-sticky");
  const visibilityFilter = sidePanel.getByTestId("comment-visibility-filter");
  const visibilitySelect = visibilityFilter.getByTestId("comment-visibility-select");

  await expect(commentsScroll).toBeVisible();
  await expect(visibilityFilter).toBeVisible();
  await expect(visibilitySelect).toBeVisible();
  await expect(visibilitySelect).toHaveValue("active");
  await commentsScroll.evaluate((element) => {
    element.scrollTop = 0;
  });

  const [sidePanelBox, composerStickyBox, filterBox, selectBox] = await Promise.all([
    sidePanel.boundingBox(),
    composerSticky.boundingBox(),
    visibilityFilter.boundingBox(),
    visibilitySelect.boundingBox(),
  ]);

  expectBoxContainedWithin(filterBox, sidePanelBox, "viewer visibility filter");
  expectBoxContainedWithin(selectBox, filterBox, "viewer visibility selector");
  expectBoxContainedWithin(composerStickyBox, sidePanelBox, "viewer comment composer footer");
  expectBoxesNotOverlapping(filterBox, composerStickyBox, "viewer visibility filter", "viewer comment composer footer");
}

function cssRgb(value) {
  const match = value.match(/rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)/i);
  if (!match) {
    throw new Error(`Unable to parse CSS color '${value}'.`);
  }

  return match.slice(1, 4).map(Number);
}

function contrastRatio(foreground, background) {
  const luminance = (color) => {
    const channels = cssRgb(color).map((channel) => {
      const normalized = channel / 255;
      return normalized <= 0.04045
        ? normalized / 12.92
        : ((normalized + 0.055) / 1.055) ** 2.4;
    });

    return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
  };

  const lighter = Math.max(luminance(foreground), luminance(background));
  const darker = Math.min(luminance(foreground), luminance(background));
  return (lighter + 0.05) / (darker + 0.05);
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

async function expectStageNavControlsFit(page, stageSurfaceBox) {
  const controls = [
    page.getByTestId("viewer-stage-prev"),
    page.getByTestId("viewer-stage-next"),
  ];

  for (const control of controls) {
    const box = await control.boundingBox();
    expect(box).toBeTruthy();
    expect(await control.getAttribute("title")).toBeNull();
    expect(box.width).toBeLessThanOrEqual(68);
    expect(box.height).toBeLessThanOrEqual(68);
    expect(box.width / box.height).toBeGreaterThan(0.82);
    expect(box.width / box.height).toBeLessThan(1.22);
    expect(box.x).toBeGreaterThanOrEqual(stageSurfaceBox.x - 1);
    expect(box.y).toBeGreaterThanOrEqual(stageSurfaceBox.y - 1);
    expect(box.x + box.width).toBeLessThanOrEqual(stageSurfaceBox.x + stageSurfaceBox.width + 1);
    expect(box.y + box.height).toBeLessThanOrEqual(stageSurfaceBox.y + stageSurfaceBox.height + 1);
  }
}

async function expectRightStageControlRailAligned(page) {
  const close = page.getByTestId("viewer-close");
  const fullscreen = page.getByTestId("viewer-fullscreen");
  const closeBox = await close.boundingBox();

  expect(closeBox).toBeTruthy();

  if (await fullscreen.isVisible()) {
    const fullscreenBox = await fullscreen.boundingBox();
    expect(fullscreenBox).toBeTruthy();
    expectWithinTolerance(closeBox.x + (closeBox.width / 2), fullscreenBox.x + (fullscreenBox.width / 2), 2);
    expectWithinTolerance(closeBox.width, fullscreenBox.width, 2);
    expect(closeBox.y + closeBox.height).toBeLessThan(fullscreenBox.y);
  }
}

async function expectStagePaginationPlaced(page, stageSurfaceBox) {
  const pager = page.getByTestId("viewer-stage-pager");
  const [pagerBox, previousBox, countBox, nextBox] = await Promise.all([
    pager.boundingBox(),
    page.getByTestId("viewer-stage-prev").boundingBox(),
    page.getByTestId("viewer-stage-page-count").boundingBox(),
    page.getByTestId("viewer-stage-next").boundingBox(),
  ]);

  expect(pagerBox).toBeTruthy();
  expect(previousBox).toBeTruthy();
  expect(countBox).toBeTruthy();
  expect(nextBox).toBeTruthy();
  expectWithinTolerance(pagerBox.x, stageSurfaceBox.x, 2);
  expectWithinTolerance(pagerBox.width, stageSurfaceBox.width, 2);
  expect(previousBox.x).toBeGreaterThanOrEqual(stageSurfaceBox.x + 6);
  expect(nextBox.x + nextBox.width).toBeLessThanOrEqual(stageSurfaceBox.x + stageSurfaceBox.width - 6);
  expect(countBox.y).toBeGreaterThanOrEqual(stageSurfaceBox.y + 6);
  expectWithinTolerance(
    countBox.x + (countBox.width / 2),
    stageSurfaceBox.x + (stageSurfaceBox.width / 2),
    3,
  );
  expectWithinTolerance(previousBox.y + (previousBox.height / 2), nextBox.y + (nextBox.height / 2), 2);
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
  await page.getByTestId("login-password").fill(USER_PASSWORD);
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("nav-logout")).toBeVisible();
}

async function loginAsAdmin(page) {
  await page.goto("/login");
  await page.getByTestId("login-username").fill("admin");
  await page.getByTestId("login-password").fill(ADMIN_PASSWORD);
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

async function setAdminControl(request, token, key, enabled) {
  const response = await request.put(`/api/admin/controls/${encodeURIComponent(key)}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data: {
      enabled,
      reason: enabled ? "Smoke test reset" : "Smoke test pause",
      message: enabled ? null : "Temporarily paused by smoke test",
    },
  });

  expect(response.ok(), `PUT control ${key} failed with HTTP ${response.status()}`).toBeTruthy();
  return response.json();
}

async function createHobbyProjectViaApi(request, token, suffix) {
  const response = await sendAuthedRequest(request, token, "POST", "/api/projects", {
    title: `Smoke project ${suffix}`,
    description: `A complete hobby project lifecycle created by the ${suffix} smoke scenario.`,
    kind: "Army",
    gameSystem: "Smokehammer",
    factionTheme: "Integration guard",
    goal: "Verify every v1 lifecycle transition",
  });
  return response.json();
}

async function createImagePostViaApi(request, token, suffix, options = {}) {
  const response = await sendAuthedRequest(request, token, "POST", "/api/posts", {
    title: `Project image ${suffix}`,
    content: `Image-backed progress update for ${suffix}.`,
    tags: ["project-smoke"],
    images: [{
      imageUrl: "/uploads/images/9_minis1.jpg",
      previewUrl: "/uploads/images/9_minis1.jpg",
      thumbnailUrl: "/uploads/images/9_minis1.jpg",
      width: 1600,
      height: 1200,
    }],
    projectId: options.projectId ?? null,
    milestoneLabel: options.milestoneLabel ?? null,
  });
  return response.json();
}

test.beforeEach(async ({ page, request }) => {
  await resetAppState(request);
  await clearAuth(page);
});

test("login with seeded user succeeds", async ({ page }) => {
  await loginAsSeedUser(page);
  await expect(page).toHaveURL(/\/$/);
});

test("new and returning Google user completes the full fake-provider flow", async ({ page }) => {
  await page.goto("/register?returnUrl=%2Fprofile");
  await expect(page.getByTestId("google-signin-link")).toBeVisible();
  await page.getByTestId("google-signin-link").click();
  await expect(page).toHaveURL(/\/auth\/external\/complete-registration$/);
  await expect(page.getByText("google-user@example.test")).toBeVisible();

  await page.getByTestId("external-registration-username").fill("googlepainter");
  const registrationResponsePromise = page.waitForResponse((response) =>
    response.request().method() === "POST" && response.url().endsWith("/api/auth/external/register"));
  await page.getByTestId("external-registration-submit").click();
  const registrationResponse = await registrationResponsePromise;
  if (!registrationResponse.ok()) {
    const responseBody = await registrationResponse.text().catch(() => "<response body unavailable>");
    throw new Error(`External registration failed with HTTP ${registrationResponse.status()}: ${responseBody}`);
  }
  await expect(page).toHaveURL(/\/profile$/);
  await expect(page.getByTestId("nav-logout")).toBeVisible();

  await page.getByTestId("nav-logout").click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/api/auth/google/start?returnUrl=https%3A%2F%2Fevil.example%2Fphish");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("nav-logout")).toBeVisible();
});

test("Google email conflict never merges and can be linked after password sign-in", async ({ page }) => {
  await page.goto("/api/auth/google/start?fake=conflict");
  await expect(page.getByTestId("external-auth-result-title")).toContainText("Sign in before connecting Google");
  await expect(page.getByTestId("external-auth-result-message")).toContainText("never merged automatically");

  await loginAsSeedUser(page);
  await page.goto("/account/sign-in-methods");
  await expect(page.getByTestId("connect-google")).toBeVisible();
  const token = await page.evaluate(() => localStorage.getItem("authToken"));
  const intent = await page.context().request.post("/api/auth/google/link-intent", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(intent.ok(), `Link intent failed with HTTP ${intent.status()}`).toBeTruthy();
  const intentBody = await intent.json();
  await page.goto(`${intentBody.startUrl}&fake=conflict`);
  await expect(page).toHaveURL(/\/account\/sign-in-methods\?linked=true$/);
  await expect(page.getByTestId("google-link-success")).toContainText("Google is connected");

  await page.getByTestId("nav-logout").click();
  await page.goto("/api/auth/google/start?fake=conflict");
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("nav-logout")).toBeVisible();
});

test("Google-only user can set a password, disconnect Google, and retain local access", async ({ page }) => {
  await page.goto("/api/auth/google/start");
  await expect(page).toHaveURL(/\/auth\/external\/complete-registration$/);
  await page.getByTestId("external-registration-username").fill("googleonly");
  await page.getByTestId("external-registration-submit").click();
  await expect(page.getByTestId("nav-logout")).toBeVisible();

  await page.goto("/account/sign-in-methods");
  await expect(page.getByTestId("google-disconnect-blocked")).toBeVisible();
  await page.getByTestId("set-password-new").fill("GoogleLocal123!");
  await page.getByTestId("set-password-confirm").fill("GoogleLocal123!");
  await page.getByTestId("set-password-submit").click();
  await expect(page.getByTestId("sign-in-methods-status")).toContainText("local password is ready");

  await page.getByTestId("disconnect-google").click();
  await page.getByTestId("disconnect-google-confirm").click();
  await expect(page.getByTestId("sign-in-methods-status")).toContainText("Google was disconnected");

  await page.getByTestId("nav-logout").click();
  await page.goto("/api/auth/google/start");
  await expect(page.getByTestId("external-auth-result-title")).toContainText("Sign in before connecting Google");

  await page.goto("/login");
  await page.getByTestId("login-username").fill("googleonly");
  await page.getByTestId("login-password").fill("GoogleLocal123!");
  await page.getByTestId("login-submit").click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByTestId("nav-logout")).toBeVisible();
});

test("Google callback exposes cancellation and expiry without changing authentication", async ({ page }) => {
  await page.goto("/api/auth/google/start?fake=cancel");
  await expect(page.getByTestId("external-auth-result-title")).toContainText("No changes were made");
  await expect(page.getByTestId("nav-login")).toBeVisible();

  await page.goto("/api/auth/google/start?fake=expired");
  await expect(page.getByTestId("external-auth-result-title")).toContainText("Start Google sign-in again");
  await expect(page.getByTestId("nav-login")).toBeVisible();
});

test("Google controls stay hidden when the provider is disabled", async ({ page }) => {
  await page.route("**/api/auth/providers", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        google: { name: "Google", displayName: "Google", enabled: false },
        supportEmail: null,
      }),
    });
  });

  await page.goto("/login");
  await expect(page.getByTestId("google-signin-link")).toHaveCount(0);
  await expect(page.getByTestId("login-form")).toBeVisible();

  await page.goto("/register");
  await expect(page.getByTestId("google-signin-link")).toHaveCount(0);
  await expect(page.getByTestId("register-form")).toBeVisible();
});

test("Google registration respects the registration control", async ({ page, request }) => {
  const adminToken = await loginViaApi(request, "admin", ADMIN_PASSWORD);
  await setAdminControl(request, adminToken, "new-registrations", false);

  await page.goto("/api/auth/google/start");
  await expect(page).toHaveURL(/\/auth\/external\/complete-registration$/);
  await page.getByTestId("external-registration-username").fill("blockedgoogle");
  await page.getByTestId("external-registration-submit").click();

  await expect(page.getByTestId("external-registration-error")).toContainText("registrations are not available");
  await expect(page.getByTestId("nav-login")).toBeVisible();
});

test("a suspended Google account cannot create a new application session", async ({ page, request }) => {
  const fakeIdentity = "fakeSubject=suspended-google-subject&fakeEmail=suspended-google%40example.test&fakeName=Suspended%20Painter";
  await page.goto(`/api/auth/google/start?${fakeIdentity}`);
  await expect(page).toHaveURL(/\/auth\/external\/complete-registration$/);
  await page.getByTestId("external-registration-username").fill("suspendedgoogle");
  await page.getByTestId("external-registration-submit").click();
  await expect(page.getByTestId("nav-logout")).toBeVisible();

  const googleToken = await page.evaluate(() => localStorage.getItem("authToken"));
  const googleClaims = JSON.parse(Buffer.from(googleToken.split(".")[1], "base64url").toString("utf8"));
  const adminToken = await loginViaApi(request, "admin", ADMIN_PASSWORD);
  await sendAuthedRequest(
    request,
    adminToken,
    "POST",
    `/api/moderation/users/${encodeURIComponent(googleClaims.sub)}/suspend`,
    {
      suspendedUntilUtc: new Date(Date.now() + (60 * 60 * 1000)).toISOString(),
      reason: "Google authentication suspension smoke test",
    },
  );

  await page.getByTestId("nav-logout").click();
  await page.goto(`/api/auth/google/start?${fakeIdentity}`);
  await expect(page.getByTestId("external-auth-result-title")).toContainText("cannot sign in right now");
  await expect(page.getByTestId("nav-login")).toBeVisible();
});

test("invalid login shows user-facing error", async ({ page }) => {
  await page.goto("/login");
  await page.getByTestId("login-username").fill("user");
  await page.getByTestId("login-password").fill("wrong-password");
  await page.getByTestId("login-submit").click();

  await expect(page.getByTestId("login-error")).toContainText("Invalid username or password.");
  await expect(page.locator("#toast-container .toast")).toHaveCount(0);
  await expect(page.getByTestId("login-error")).toBeFocused();
});

test("empty login is validated locally with one announcement", async ({ page }) => {
  await page.goto("/login");
  let loginRequests = 0;
  page.on("request", (request) => {
    if (request.method() === "POST" && request.url().endsWith("/api/auth/login")) {
      loginRequests += 1;
    }
  });

  await page.getByTestId("login-submit").click();

  await expect(page.getByTestId("login-error")).toContainText("Enter your username and password.");
  await expect(page.locator("#toast-container .toast")).toHaveCount(0);
  expect(loginRequests).toBe(0);
});

test("login reports rate limiting and service outages inline", async ({ page }) => {
  for (const scenario of [
    { status: 429, message: "Too many sign-in attempts" },
    { status: 503, message: "Sign-in is unavailable" },
  ]) {
    await page.route("**/api/auth/login", async (route) => {
      await route.fulfill({
        status: scenario.status,
        contentType: "application/problem+json",
        body: JSON.stringify({ title: "Sign-in failed", status: scenario.status }),
      });
    });

    await page.goto("/login");
    await page.getByTestId("login-username").fill("user");
    await page.getByTestId("login-password").fill(USER_PASSWORD);
    await page.getByTestId("login-submit").click();
    await expect(page.getByTestId("login-error")).toContainText(scenario.message);
    await expect(page.locator("#toast-container .toast")).toHaveCount(0);
    await page.unroute("**/api/auth/login");
  }
});

test("mobile navigation, More menu, and personal panel work", async ({ page }) => {
  await loginAsSeedUser(page);
  await page.setViewportSize({ width: 390, height: 844 });

  const toggle = page.getByTestId("nav-toggle");
  const collapse = page.locator("#navbarNav");
  await toggle.focus();
  await toggle.press("Enter");
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(collapse).toHaveClass(/show/);

  await page.getByTestId("nav-more").click();
  await expect(page.getByTestId("nav-more-highlights")).toBeVisible();
  await expect(page.getByTestId("nav-more-about")).toBeVisible();

  await page.getByTestId("nav-search-link").click();
  await expect(page).toHaveURL(/\/search$/);
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(collapse).not.toHaveClass(/show/);
  await expect(toggle).toBeFocused();

  await page.goto("/");
  const panelToggle = page.getByRole("button", { name: "Toggle personal panel" });
  await panelToggle.click();
  await expect(page.locator("#userPanelOffcanvas")).toHaveClass(/show/);
  await expect(page.locator("#userPanelOffcanvas")).toBeVisible();
});

test("anonymous header stays collision-free at desktop widths", async ({ page }) => {
  for (const width of [1280, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.goto("/");
    await expect(page.locator(".atelier-brand")).toBeVisible();
    const layout = await page.evaluate(() => {
      const rect = (selector) => document.querySelector(selector)?.getBoundingClientRect().toJSON();
      return {
        bodyScrollWidth: document.body.scrollWidth,
        viewportWidth: window.innerWidth,
        brand: rect(".atelier-brand"),
        search: rect(".atelier-nav__search-link"),
        links: rect(".atelier-nav__links"),
        session: rect(".atelier-nav__session"),
      };
    });

    expect(layout.bodyScrollWidth).toBeLessThanOrEqual(layout.viewportWidth);
    expect(layout.brand).toBeTruthy();
    expect(layout.search).toBeTruthy();
    expect(layout.links).toBeTruthy();
    expect(layout.session).toBeTruthy();
    expect(layout.brand.right).toBeLessThanOrEqual(layout.search.left + 1);
    expect(layout.search.right).toBeLessThanOrEqual(layout.links.left + 1);
    expect(layout.links.right).toBeLessThanOrEqual(layout.session.left + 1);
  }
});

test("create post flow redirects to details and renders content", async ({ page }) => {
  await loginAsSeedUser(page);
  await createPost(page, "create");
});

test("seeded post with image and tags renders on feed and details", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/");

  const seededCard = page.locator(".post-card", { hasText: "Seeded: glazing check" }).first();
  const weatheringCard = page.locator(".post-card", { hasText: "Seeded: weathering notes" }).first();
  await expect(seededCard).toBeVisible();
  await expect(weatheringCard).toBeVisible();
  await expect(seededCard.getByTestId("post-card-image")).toBeVisible();
  await expect(seededCard.getByTestId("post-card-image")).toHaveAttribute("alt", /Seeded: glazing check by /);
  await expect(seededCard.getByTestId("post-card-tags")).toContainText("#glazing");
  await expect(seededCard.getByTestId("post-card-tags")).toContainText("#nmm");
  await expect(weatheringCard.getByTestId("post-card-image")).toBeVisible();
  await expect(weatheringCard.getByTestId("post-card-image")).toHaveAttribute("alt", /Seeded: weathering notes by /);
  const seededImageSource = await seededCard.getByTestId("post-card-image").getAttribute("src");
  const weatheringImageSource = await weatheringCard.getByTestId("post-card-image").getAttribute("src");
  expect(seededImageSource).not.toBe(weatheringImageSource);
  const firstCardBox = await page.locator(".post-card").first().boundingBox();
  expect(firstCardBox).toBeTruthy();
  expect(firstCardBox.y).toBeLessThanOrEqual(430);
  await expect(weatheringCard.getByTestId("post-card-tags")).toContainText("#weathering");
  await expect(weatheringCard.getByTestId("post-card-tags")).toContainText("#battle-damage");

  await seededCard.getByRole("link", { name: "Seeded: glazing check", exact: true }).click();

  await expect(page).toHaveURL(/\/posts\/\d+$/);
  await expect(page.getByTestId("post-title")).toHaveText("Seeded: glazing check");
  await expect(page.getByTestId("post-details-image")).toBeVisible();
  await expect(page.getByTestId("post-details-image")).toHaveAttribute("alt", "Seeded: glazing check");
  await expect(page.getByTestId("post-details-tags")).toContainText("#glazing");
  await expect(page.getByTestId("post-details-tags")).toContainText("#nmm");
  const galleryBox = await page.getByTestId("post-details-gallery").boundingBox();
  expect(galleryBox).toBeTruthy();
  expect(galleryBox.y).toBeLessThanOrEqual(650);

  await openViewerFromDetails(page);
  await expect(page.getByTestId("rich-image-viewer")).toHaveClass(/is-single-image/);
  await expect(page.getByTestId("viewer-thumbnail-rail")).toHaveCount(0);
  await expect(page.getByTestId("viewer-stage-prev")).toHaveCount(0);
  await expect(page.getByTestId("viewer-stage-next")).toHaveCount(0);
  await expect(page.getByTestId("viewer-stage-image")).toHaveAttribute("alt", "Seeded: glazing check");

  const [stageColumnBox, stageSurfaceBox] = await Promise.all([
    page.locator(".viewer-shell__stage-column").boundingBox(),
    page.locator(".viewer-shell__stage-surface").boundingBox(),
  ]);
  expect(stageColumnBox).toBeTruthy();
  expect(stageSurfaceBox).toBeTruthy();
  expectWithinTolerance(stageSurfaceBox.height, stageColumnBox.height, 4);

  const panelColors = await page.evaluate(() => {
    const panel = document.querySelector(".viewer-shell__side-panel");
    const title = document.querySelector(".viewer-panel__title");
    const body = document.querySelector(".viewer-panel__copy");
    const author = document.querySelector(".viewer-panel__author");
    const activeTab = document.querySelector(".viewer-panel__tab.is-active");
    const gradient = getComputedStyle(activeTab).backgroundImage;
    const gradientColors = [...gradient.matchAll(/rgba?\([^)]*\)/g)].map((match) => match[0]);
    return {
      background: getComputedStyle(panel).backgroundColor,
      title: getComputedStyle(title).color,
      body: getComputedStyle(body).color,
      author: getComputedStyle(author).color,
      activeText: getComputedStyle(activeTab).color,
      gradientColors,
    };
  });
  expect(contrastRatio(panelColors.title, panelColors.background)).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatio(panelColors.body, panelColors.background)).toBeGreaterThanOrEqual(4.5);
  expect(contrastRatio(panelColors.author, panelColors.background)).toBeGreaterThanOrEqual(4.5);
  expect(panelColors.gradientColors).toHaveLength(2);
  for (const color of panelColors.gradientColors) {
    expect(contrastRatio(panelColors.activeText, color)).toBeGreaterThanOrEqual(4.5);
  }

  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(250);
  const mobileStageBox = await page.getByTestId("viewer-stage").boundingBox();
  expect(mobileStageBox).toBeTruthy();
  expect(mobileStageBox.height).toBeGreaterThanOrEqual(384);
  expect(mobileStageBox.height).toBeLessThanOrEqual(496);
  await expect(page.getByTestId("viewer-side-panel")).toBeVisible();
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

  const createdCard = page.locator(".post-card", { hasText: title }).first();
  await expect(createdCard).toBeVisible();
  await expect(createdCard.getByTestId("post-card-image")).toBeVisible();
  await expect(createdCard.getByTestId("post-card-tags")).toContainText("#glazing");
  await expect(createdCard.getByTestId("post-card-tags")).toContainText("#showcase");
});

test("comment and like flow works on post details", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await loginAsSeedUser(page);
  await createPost(page, "engagement", { imagePath: SAMPLE_IMAGE_PATH });

  await expectPageCommentComposerLayout(page);

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

  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(180);
  await expectPageCommentComposerLayout(page);
});

test("rich viewer overlay keeps post details intact and supports refined layout modes", async ({ page, request }) => {
  await loginAsSeedUser(page);
  const viewerPost = await createRichViewerPost(page, request, "desktop-flow", { extraPlainCommentsCount: 16 });
  const primaryImagePath = getPathSegment(new URL(viewerPost.primaryImage.previewUrl, page.url()).toString(), "/uploads/images/");
  const squareImagePath = getPathSegment(new URL(viewerPost.squareImage.previewUrl, page.url()).toString(), "/uploads/images/");
  const secondaryImagePath = getPathSegment(new URL(viewerPost.secondaryImage.previewUrl, page.url()).toString(), "/uploads/images/");
  const panoramaImagePath = getPathSegment(new URL(viewerPost.panoramaImage.previewUrl, page.url()).toString(), "/uploads/images/");
  const primaryIndex = viewerPost.viewer.images.findIndex((image) => image.id === viewerPost.primaryImage.id);
  const secondaryIndex = viewerPost.viewer.images.findIndex((image) => image.id === viewerPost.secondaryImage.id);
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

  const detailThumbnails = page.getByTestId("post-details-thumbnail");
  await expect(detailThumbnails).toHaveCount(VIEWER_RATIO_EXPECTATIONS.length);
  await detailThumbnails.nth(secondaryIndex).click();
  await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
  await expect(page.getByTestId("post-details-image")).toHaveAttribute("src", new RegExp(secondaryImagePath));
  await expect(detailThumbnails.nth(secondaryIndex)).toHaveAttribute("aria-current", "true");

  await openViewerFromDetails(page);
  await expect(page.getByTestId("viewer-stage-image")).toHaveAttribute("src", new RegExp(secondaryImagePath));
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(0);
  await page.getByTestId("viewer-thumbnail").nth(primaryIndex).click({ force: true });
  await expect(page.getByTestId("viewer-stage-image")).toHaveAttribute("src", new RegExp(primaryImagePath));
  await expect(page.getByTestId("viewer-author-mark")).toHaveCount(1);
  await expect(page.getByTestId("viewer-comment-mark")).toHaveCount(0);
  await expect(page.getByTestId("viewer-side-tab-info")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-panel-info")).toContainText("About this piece");
  await expect(page.getByTestId("viewer-close")).toBeVisible();
  await expect(page.getByTestId("viewer-stage")).toBeVisible();
  await expect(page.getByTestId("viewer-thumbnail-rail")).toBeVisible();
  await expect(page.getByTestId("viewer-close")).toBeVisible();
  await expectViewerScrollLocked(page);

  const stage = page.getByTestId("viewer-stage");
  const stageImage = page.getByTestId("viewer-stage-image");
  const toolbarZoom = page.getByTestId("viewer-rail-zoom");
  const closeButton = page.getByTestId("viewer-close");
  const sidePanel = page.getByTestId("viewer-side-panel");
  const modal = page.getByTestId("rich-image-viewer");

  const [closeButtonBox, panelBox, modalBox] = await Promise.all([
    closeButton.boundingBox(),
    sidePanel.boundingBox(),
    modal.boundingBox(),
  ]);
  expect(closeButtonBox).toBeTruthy();
  expect(panelBox).toBeTruthy();
  expect(modalBox).toBeTruthy();
  expect(panelBox.x + panelBox.width).toBeLessThanOrEqual(modalBox.x + modalBox.width + 1);

  let { stageSurfaceBox, stageBox, fitBox, imageBox } = await getViewerBoxes(page);
  expect(stageBox.width).toBeGreaterThan(panelBox.width);
  expect(stageBox.x + stageBox.width).toBeLessThanOrEqual(panelBox.x + 1);
  expect(closeButtonBox.x + closeButtonBox.width).toBeLessThanOrEqual(stageSurfaceBox.x + stageSurfaceBox.width + 1);
  expect(closeButtonBox.y).toBeGreaterThanOrEqual(stageSurfaceBox.y - 1);
  await expectStageNavControlsFit(page, stageSurfaceBox);
  await expectStagePaginationPlaced(page, stageSurfaceBox);
  await expectRightStageControlRailAligned(page);
  expectWithinTolerance(fitBox.x - stageBox.x, (stageBox.width - fitBox.width) / 2, 4);
  expectWithinTolerance(fitBox.y - stageBox.y, (stageBox.height - fitBox.height) / 2, 4);
  expectWithinTolerance(imageBox.x - stageBox.x, (stageBox.width - imageBox.width) / 2, 4);
  expectWithinTolerance(imageBox.y - stageBox.y, (stageBox.height - imageBox.height) / 2, 4);
  expectWithinTolerance(fitBox.height, stageBox.height, 4);
  const fitArea = fitBox.width * fitBox.height;

  await expectViewerControlsHidden(page);

  await stage.hover();
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

  await stage.hover();
  await page.getByTestId("viewer-view-actual").click();
  await expect(page.getByTestId("viewer-view-actual")).toHaveClass(/is-active/);
  await expect(toolbarZoom).toContainText("100%");
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

  const expandedCloseButtonBox = await closeButton.boundingBox();
  const expandedStageBox = await stage.boundingBox();
  expect(expandedCloseButtonBox).toBeTruthy();
  expect(expandedCloseButtonBox.x + expandedCloseButtonBox.width).toBeLessThanOrEqual(stageSurfaceBox.x + stageSurfaceBox.width + 1);
  expect(expandedCloseButtonBox.y).toBeGreaterThanOrEqual(stageSurfaceBox.y - 1);
  expect(expandedStageBox.width).toBeGreaterThan(panelBox.width);

  await page.getByTestId("viewer-side-tab-comments").click();
  await expect(page.getByTestId("viewer-side-tab-comments")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-comments-scroll")).toBeVisible();
  await expect(page.locator("[data-testid='viewer-comments-thread'] [data-testid='comment-item']").first()).toBeVisible();
  await expectViewerComposerLayout(page);
  const commentsScroll = page.getByTestId("viewer-comments-scroll");
  const composerSticky = page.getByTestId("viewer-composer-sticky");
  const composerBoxBeforeScroll = await composerSticky.boundingBox();
  const scrollMetrics = await commentsScroll.evaluate((element) => {
    const styles = window.getComputedStyle(element);
    return {
      clientHeight: element.clientHeight,
      scrollHeight: element.scrollHeight,
      clientWidth: element.clientWidth,
      offsetWidth: element.offsetWidth,
      overflowY: styles.overflowY,
      scrollbarGutter: styles.scrollbarGutter,
      supportsStableScrollbarGutter: typeof CSS !== "undefined"
        && typeof CSS.supports === "function"
        && CSS.supports("scrollbar-gutter: stable"),
    };
  });
  expect(scrollMetrics.overflowY).toBe("scroll");
  expect(scrollMetrics.scrollHeight).toBeGreaterThan(scrollMetrics.clientHeight);
  if (scrollMetrics.supportsStableScrollbarGutter) {
    expect(scrollMetrics.scrollbarGutter).toMatch(/\bstable\b/);
  }
  await commentsScroll.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
  });
  await page.waitForTimeout(160);
  const scrollMetricsAfterScroll = await commentsScroll.evaluate((element) => ({
    scrollTop: element.scrollTop,
    clientWidth: element.clientWidth,
    offsetWidth: element.offsetWidth,
  }));
  const composerBoxAfterScroll = await composerSticky.boundingBox();
  expect(scrollMetricsAfterScroll.scrollTop).toBeGreaterThan(0);
  expect(scrollMetricsAfterScroll.clientWidth).toBe(scrollMetrics.clientWidth);
  expect(scrollMetricsAfterScroll.offsetWidth).toBe(scrollMetrics.offsetWidth);
  expect(composerBoxBeforeScroll).toBeTruthy();
  expect(composerBoxAfterScroll).toBeTruthy();
  expectWithinTolerance(composerBoxAfterScroll.y, composerBoxBeforeScroll.y, 2);
  await page.getByTestId("viewer-side-tab-info").click();
  await expect(page.getByTestId("viewer-side-tab-info")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-panel-info")).toBeVisible();

  await stage.hover();
  await page.getByTestId("viewer-close").click();
  await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
  await expectViewerScrollReleased(page);

  const pageComment = page
    .locator("[data-testid='comment-list-container']")
    .first()
    .getByTestId("comment-item")
    .filter({ hasText: "Portrait follow-up anchor sits on the second portrait image and should switch the viewer there cleanly." })
    .first();
  const pageCommentMarkActions = pageComment.getByTestId("comment-mark-actions");
  const pageCommentMarkBadge = pageComment.getByTestId("comment-mark-badge");
  const pageCommentShowMark = pageComment.getByTestId("comment-show-mark");
  await expect(pageCommentMarkActions).toBeVisible();
  await expect(pageCommentMarkBadge).toBeVisible();
  await expect(pageCommentShowMark).toBeVisible();
  const [markActionsBox, markBadgeBox, showMarkBox] = await Promise.all([
    pageCommentMarkActions.boundingBox(),
    pageCommentMarkBadge.boundingBox(),
    pageCommentShowMark.boundingBox(),
  ]);
  expect(markActionsBox).toBeTruthy();
  expect(markBadgeBox).toBeTruthy();
  expect(showMarkBox).toBeTruthy();
  expect(markBadgeBox.y).toBeGreaterThanOrEqual(markActionsBox.y - 1);
  expect(showMarkBox.y).toBeGreaterThanOrEqual(markActionsBox.y - 1);
  expect(markBadgeBox.y + markBadgeBox.height).toBeLessThanOrEqual(markActionsBox.y + markActionsBox.height + 1);
  expect(showMarkBox.y + showMarkBox.height).toBeLessThanOrEqual(markActionsBox.y + markActionsBox.height + 1);
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

  await page.getByTestId("viewer-stage").hover();
  await stage.hover();
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
    await expectViewerControlsHidden(page);
    await stage.hover();
    await expect(page.locator(".viewer-shell__stage-surface")).toHaveClass(/is-controls-visible/);
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

test("admin viewer comment controls stay contained across compact layouts", async ({ page, request }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await loginAsAdmin(page);
  await createRichViewerPost(page, request, "admin-comment-layout", { extraPlainCommentsCount: 6 });

  await openViewerFromDetails(page);
  await page.getByTestId("viewer-side-tab-comments").click();
  await expect(page.getByTestId("viewer-side-tab-comments")).toHaveClass(/is-active/);
  await expect(page.getByTestId("viewer-side-panel").getByTestId("comment-item").first()).toBeVisible();
  await expectViewerComposerLayout(page);
  await expectAdminViewerFilterLayout(page);

  const visibilitySelect = page
    .getByTestId("viewer-side-panel")
    .getByTestId("comment-visibility-filter")
    .getByTestId("comment-visibility-select");
  await visibilitySelect.selectOption("all");
  await expect(visibilitySelect).toHaveValue("all");
  await expect(page.getByTestId("viewer-side-panel").getByTestId("comment-item").first()).toBeVisible();
  await visibilitySelect.selectOption("active");
  await expect(visibilitySelect).toHaveValue("active");

  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(250);
  await expectViewerComposerLayout(page);
  await expectAdminViewerFilterLayout(page);
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
      await page.getByTestId("viewer-stage-next").click();
      await expect.poll(() => page.getByTestId("viewer-stage-image").getAttribute("src")).not.toBe(previousSrc);
    }

    await expect(page.getByTestId("viewer-comment-mark")).toHaveCount(0);

    const currentSrc = await page.getByTestId("viewer-stage-image").getAttribute("src");
    observedSources.add(currentSrc);

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
    await expectStageNavControlsFit(page, stageSurfaceBox);
    await expectStagePaginationPlaced(page, stageSurfaceBox);
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

  const delayedImagePattern = new RegExp(viewerPost.primaryImage.previewUrl.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
  let releaseDelayedImage;
  const delayedImageRelease = new Promise((resolve) => {
    releaseDelayedImage = resolve;
  });
  let markDelayedImageRequested;
  const delayedImageRequested = new Promise((resolve) => {
    markDelayedImageRequested = resolve;
  });

  await page.route(delayedImagePattern, async (route) => {
    markDelayedImageRequested();
    await delayedImageRelease;
    await route.continue();
  });

  await openViewerFromDetails(page, { waitForImage: false });
  await delayedImageRequested;
  await expect(page.getByTestId("viewer-skeleton")).toHaveCount(1);
  releaseDelayedImage();
  await expect(page.getByTestId("viewer-skeleton")).toHaveCount(0);
  await page.unroute(delayedImagePattern);

  const failingImagePattern = /\/uploads\/images\/.*_preview\.(webp|jpg|png)$/;
  await page.route(failingImagePattern, async (route) => {
    if (route.request().url().includes(viewerPost.primaryImage.previewUrl)) {
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
  await expect(page.getByTestId("viewer-close")).toBeVisible();
  const [closeButtonBox, stageBox, panelBox] = await Promise.all([
    page.getByTestId("viewer-close").boundingBox(),
    page.getByTestId("viewer-stage").boundingBox(),
    page.getByTestId("viewer-side-panel").boundingBox(),
  ]);
  expect(closeButtonBox).toBeTruthy();
  expect(stageBox).toBeTruthy();
  expect(panelBox).toBeTruthy();
  expect(closeButtonBox.x + closeButtonBox.width).toBeLessThanOrEqual(stageBox.x + stageBox.width + 1);
  expect(closeButtonBox.y).toBeGreaterThanOrEqual(stageBox.y - 1);
  expect(stageBox.height).toBeGreaterThan(panelBox.height * 0.8);
  expectWithinTolerance(stageBox.x, panelBox.x, 6);
  expectWithinTolerance(stageBox.width, panelBox.width, 6);
});

test("search by title and tag works, and public profiles open from search", async ({ page }) => {
  await loginAsSeedUser(page);
  const { title } = await createPost(page, "search", { tags: "glazing" });

  await page.getByTestId("nav-search-link").click();
  await expect(page).toHaveURL(/\/search$/);
  await page.getByTestId("search-query-input").fill(title);
  await page.getByTestId("search-query-submit").click();

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

  await page.goto("/search?q=weathering&tab=posts");
  const weatheringResult = page
    .getByTestId("search-post-result")
    .filter({ hasText: "Seeded: weathering notes" })
    .first();
  const weatheringImage = weatheringResult.getByTestId("post-card-image");
  await expect(weatheringResult).toBeVisible();
  await expect(weatheringImage).toHaveAttribute("alt", /Seeded: weathering notes by /);
  const weatheringSource = await weatheringImage.getAttribute("src");

  await page.goto("/search?q=Seeded%3A%20glazing%20check&tab=posts");
  const glazingImage = page
    .getByTestId("search-post-result")
    .filter({ hasText: "Seeded: glazing check" })
    .first()
    .getByTestId("post-card-image");
  await expect(glazingImage).toHaveAttribute("alt", /Seeded: glazing check by /);
  expect(await glazingImage.getAttribute("src")).not.toBe(weatheringSource);

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
  const adminToken = await loginViaApi(request, "admin", ADMIN_PASSWORD);
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

test("support request flows from user to admin and can be reopened", async ({ page }) => {
  const subject = `Smoke support ${Date.now()}`;
  const initialMessage = "The image upload stops before the preview appears.";
  const adminReply = "Please retry with an image smaller than the upload limit.";
  const reopenReply = "I retried with a smaller image and still need help.";

  await loginAsSeedUser(page);
  await page.goto("/support/new");
  await page.getByTestId("support-category").selectOption("Bug");
  await page.getByTestId("support-subject").fill(subject);
  await page.getByTestId("support-message-input").fill(initialMessage);
  await page.getByTestId("support-create-submit").click();
  await expect(page).toHaveURL(/\/support\/\d+$/);
  await expect(page.getByTestId("support-ticket-detail")).toContainText(subject);

  await clearAuth(page);
  await loginAsAdmin(page);
  await page.goto("/admin/support");
  await page.getByTestId("admin-support-search").fill(subject);
  await page.getByTestId("admin-support-apply").click();
  await expect(page.getByTestId("admin-support-row").first()).toContainText(subject);
  await page.getByTestId("admin-support-select").first().click();
  await expect(page.getByTestId("admin-support-inspector")).toContainText(initialMessage);
  await page.getByTestId("admin-support-reply-input").fill(adminReply);
  await page.getByTestId("admin-support-reply-submit").click();
  await expect(page.getByTestId("admin-support-inspector")).toContainText(adminReply);
  await page.getByTestId("admin-support-resolve").click();
  await expect(page.getByTestId("admin-support-inspector")).toContainText("Resolved");

  await clearAuth(page);
  await loginAsSeedUser(page);
  await page.goto("/support");
  const ticketCard = page.getByTestId("support-ticket-card").filter({ hasText: subject }).first();
  await expect(ticketCard).toContainText("New reply");
  await ticketCard.click();
  await expect(page.getByTestId("support-resolved-state")).toBeVisible();
  await expect(page.getByTestId("support-thread")).toContainText(adminReply);
  await page.getByTestId("support-reply-input").fill(reopenReply);
  await page.getByTestId("support-reply-submit").click();
  await expect(page.getByTestId("support-ticket-detail")).toContainText("Waiting for admin");
  await expect(page.getByTestId("support-resolved-state")).toHaveCount(0);
});

test("admin can hide and restore post from the admin inbox", async ({ page }) => {
  await loginAsAdmin(page);
  const { title } = await createPost(page, "admin-post-moderation");

  await page.goto("/admin");
  await page.getByTestId("admin-inbox-search").fill(title);
  await page.getByTestId("admin-inbox-apply").click();

  await expect(page.getByTestId("admin-inbox-row").first()).toContainText(title);
  await page.getByTestId("admin-inbox-hide").first().click();
  await expect(page.getByTestId("admin-inbox-inspector")).toContainText("Hidden");

  await page.getByTestId("admin-inbox-detail-restore").click();
  await expect(page.getByTestId("admin-inbox-inspector")).toContainText("Active");
});

test("admin can hide and restore comment using comment visibility filter", async ({ page }) => {
  await loginAsAdmin(page);
  const { title } = await createPost(page, "admin-comment-moderation");
  const commentText = "Admin moderation smoke comment";

  await page.getByTestId("comment-input").fill(commentText);
  await page.getByTestId("comment-submit").click();
  await expect(page.getByTestId("comment-item").first()).toContainText(commentText);

  const adminComment = page.getByTestId("comment-item").first();
  const adminActions = adminComment.getByTestId("comment-admin-actions");
  const moderationControls = adminComment.getByTestId("comment-inline-moderation-controls");
  await expect(adminActions).toBeVisible();
  await expect(moderationControls).toBeVisible();
  const [adminActionsBox, moderationControlsBox] = await Promise.all([
    adminActions.boundingBox(),
    moderationControls.boundingBox(),
  ]);
  expect(adminActionsBox).toBeTruthy();
  expect(moderationControlsBox).toBeTruthy();
  expect(moderationControlsBox.y).toBeGreaterThanOrEqual(adminActionsBox.y - 1);
  expect(moderationControlsBox.y + moderationControlsBox.height).toBeLessThanOrEqual(adminActionsBox.y + adminActionsBox.height + 1);

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
  await page.goto("/admin");

  await expect(page.getByTestId("admin-inbox-row").first()).toContainText(title);
  await page.getByTestId("admin-inbox-reason").fill("Handled in smoke test.");
  await page.getByTestId("admin-inbox-review").click();
  await expect(page.getByTestId("admin-inbox-inspector")).toContainText("Reviewed");
});

test("admin can pause comments and creation is blocked until re-enabled", async ({ page, request }) => {
  await loginAsAdmin(page);
  await createPost(page, "comment-control");
  const postId = page.url().match(/\/posts\/(\d+)$/)?.[1];
  expect(postId, "Expected created post URL to contain an id.").toBeTruthy();

  const adminToken = await loginViaApi(request, "admin", ADMIN_PASSWORD);
  await setAdminControl(request, adminToken, "new-comments", false);

  const userToken = await loginViaApi(request, "user", USER_PASSWORD);
  const blocked = await request.post(`/api/posts/${postId}/comments`, {
    headers: {
      Authorization: `Bearer ${userToken}`,
    },
    data: {
      postId: Number(postId),
      text: "Blocked smoke comment",
    },
  });
  expect(blocked.status()).toBe(403);

  await setAdminControl(request, adminToken, "new-comments", true);
  const allowed = await request.post(`/api/posts/${postId}/comments`, {
    headers: {
      Authorization: `Bearer ${userToken}`,
    },
    data: {
      postId: Number(postId),
      text: "Allowed smoke comment",
    },
  });
  expect(allowed.ok(), `Comment should be allowed after re-enable, got HTTP ${allowed.status()}`).toBeTruthy();
});

test("hobby project lifecycle stays connected across diary, showcase, discovery, moves, and moderation", async ({ page, request }) => {
  test.setTimeout(120_000);

  await loginAsSeedUser(page);
  const userToken = await page.evaluate(() => localStorage.getItem("authToken"));
  expect(userToken, "Expected the seeded user session to expose its JWT.").toBeTruthy();
  const userProfile = await ensureProfileForToken(request, userToken, {
    displayName: "Project Smoke Painter",
    bio: "Builds projects for lifecycle verification.",
  });

  const projectTitle = "Smoke Alpine Expedition";
  await page.goto("/projects/new");
  await page.getByTestId("project-create-title").fill(projectTitle);
  await page.getByTestId("project-create-description").fill("A winter army diary moving from first basecoat to a curated finished showcase.");
  await page.getByTestId("project-create-kind").selectOption("Army");
  await page.getByTestId("project-create-game-system").fill("Smokehammer");
  await page.getByTestId("project-create-faction-theme").fill("Alpine expedition");
  await page.getByTestId("project-create-goal").fill("Finish and document two image-backed checkpoints");
  await page.getByTestId("project-create-submit").click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  await expect(page.getByTestId("project-details-hero")).toContainText(projectTitle);
  await expect(page.getByTestId("project-entries-empty")).toBeVisible();

  const projectId = Number(new URL(page.url()).pathname.split("/").pop());
  expect(projectId).toBeGreaterThan(0);
  const emptyPublicRead = await request.get(`/api/projects/${projectId}`);
  expect(emptyPublicRead.status(), "Empty projects must remain owner-only.").toBe(404);

  const attachedPost = await createImagePostViaApi(request, userToken, "existing-post");
  await sendAuthedRequest(request, userToken, "POST", `/api/projects/${projectId}/posts`, {
    postId: attachedPost.id,
    milestoneLabel: "Existing post attached",
  });

  await page.goto(`/projects/${projectId}?view=diary`);
  await expect(page.getByTestId("project-diary")).toContainText(attachedPost.title);
  await expect(page.getByTestId("project-diary")).toContainText("Existing post attached");
  expect((await request.get(`/api/projects/${projectId}`)).ok(), "The first visible entry should make the project public.").toBeTruthy();

  await page.goto(`/posts/new?projectId=${projectId}`);
  await expect(page.getByTestId("create-post-project")).toHaveValue(String(projectId));
  await page.getByTestId("create-post-title").fill("Alpine command checkpoint");
  await page.getByTestId("create-post-content").fill("The command group now has its final winter palette and snow texture.");
  await page.getByTestId("create-post-milestone").fill("Command group complete");
  await page.getByTestId("create-post-images").setInputFiles(SAMPLE_IMAGE_PATH);
  const publishResponsePromise = page.waitForResponse((response) =>
    response.request().method() === "POST" && response.url().endsWith("/api/posts/with-image"));
  await page.getByTestId("create-post-submit").click();
  const publishResponse = await publishResponsePromise;
  expect(publishResponse.ok(), `Project-aware image publish failed with HTTP ${publishResponse.status()}.`).toBeTruthy();
  const milestonePost = await publishResponse.json();
  await expect(page).toHaveURL(new RegExp(`/projects/${projectId}\\?view=diary#post-${milestonePost.id}$`));
  await expect(page.getByTestId("project-diary")).toContainText("Command group complete");

  await sendAuthedRequest(request, userToken, "PUT", `/api/projects/${projectId}/showcase`, {
    postIds: [attachedPost.id, milestonePost.id],
  });
  let showcaseResponse = await request.get(`/api/projects/${projectId}/showcase`);
  let showcase = await showcaseResponse.json();
  expect(showcase.items.map((entry) => entry.postId)).toEqual([attachedPost.id, milestonePost.id]);

  await sendAuthedRequest(request, userToken, "PUT", `/api/projects/${projectId}/showcase`, {
    postIds: [milestonePost.id, attachedPost.id],
  });
  showcaseResponse = await request.get(`/api/projects/${projectId}/showcase`);
  showcase = await showcaseResponse.json();
  expect(showcase.items.map((entry) => entry.postId)).toEqual([milestonePost.id, attachedPost.id]);

  await sendAuthedRequest(request, userToken, "PUT", `/api/projects/${projectId}/cover`, {
    postId: attachedPost.id,
  });
  const completedResponse = await sendAuthedRequest(request, userToken, "PUT", `/api/projects/${projectId}/status`, {
    status: "Completed",
  });
  const completed = await completedResponse.json();
  expect(completed.status).toBe("Completed");
  expect(completed.coverPostId).toBe(attachedPost.id);

  await clearAuth(page);
  await page.goto("/projects");
  await expect(page.getByTestId("project-list")).toContainText(projectTitle);
  await page.goto(`/projects/${projectId}`);
  await expect(page.getByTestId("project-showcase")).toBeVisible();
  await expect(page.getByTestId("project-showcase")).toContainText("Alpine command checkpoint");
  await page.goto(`/projects/${projectId}?view=diary`);
  await expect(page.getByTestId("project-diary")).toContainText("Command group complete");
  await page.goto(`/users/${encodeURIComponent(userProfile.userId)}`);
  await expect(page.getByTestId("profile-projects")).toContainText(projectTitle);
  await page.goto(`/search?q=${encodeURIComponent("Smoke Alpine")}&tab=projects`);
  await expect(page.getByTestId("search-projects-results")).toContainText(projectTitle);

  const reopeningPost = await createImagePostViaApi(request, userToken, "reopen", {
    projectId,
    milestoneLabel: "Reinforcements arrive",
  });
  let ownerRead = await request.get(`/api/projects/${projectId}`, {
    headers: { Authorization: `Bearer ${userToken}` },
  });
  let project = await ownerRead.json();
  expect(project.status).toBe("InProgress");
  expect(project.completedUtc).toBeNull();

  const destination = await createHobbyProjectViaApi(request, userToken, "move-target");
  await sendAuthedRequest(request, userToken, "POST", `/api/projects/${destination.id}/posts`, {
    postId: attachedPost.id,
    sourceProjectId: projectId,
    milestoneLabel: "Transferred detachment",
  });
  ownerRead = await request.get(`/api/projects/${projectId}`, {
    headers: { Authorization: `Bearer ${userToken}` },
  });
  project = await ownerRead.json();
  expect(project.selectedCoverPostId, "Moving the selected cover must clear the explicit source selection.").toBeNull();
  expect(project.coverPostId, "Moving the selected cover must remove the moved post from the effective cover.").not.toBe(attachedPost.id);
  expect(project.coverPostId, "The source project should fall back to its remaining showcase image.").toBe(milestonePost.id);
  expect(project.entryCount).toBe(2);

  let lifecycleResponse = await sendAuthedRequest(request, userToken, "POST", `/api/projects/${projectId}/archive`);
  project = await lifecycleResponse.json();
  expect(project.isArchived).toBeTruthy();
  expect((await request.get(`/api/projects/${projectId}`)).status()).toBe(404);
  lifecycleResponse = await sendAuthedRequest(request, userToken, "POST", `/api/projects/${projectId}/restore`);
  project = await lifecycleResponse.json();
  expect(project.isArchived).toBeFalsy();
  expect((await request.get(`/api/projects/${projectId}`)).ok()).toBeTruthy();

  const adminToken = await loginViaApi(request, "admin", ADMIN_PASSWORD);
  await sendAuthedRequest(request, adminToken, "POST", `/api/reports/projects/${projectId}`, {
    reasonCode: "Other",
    details: "Project lifecycle moderation smoke report.",
  });
  await sendAuthedRequest(request, adminToken, "POST", `/api/moderation/projects/${projectId}/hide`, {
    reason: "Project lifecycle moderation smoke hide.",
  });
  expect((await request.get(`/api/projects/${projectId}`)).status()).toBe(404);
  expect((await request.get(`/api/posts/${milestonePost.id}`)).ok(), "Project moderation must not hide linked posts.").toBeTruthy();
  const hiddenOwnerRead = await request.get(`/api/projects/${projectId}`, {
    headers: { Authorization: `Bearer ${userToken}` },
  });
  expect(hiddenOwnerRead.ok()).toBeTruthy();
  expect((await hiddenOwnerRead.json()).isHidden).toBeTruthy();

  await sendAuthedRequest(request, adminToken, "POST", `/api/moderation/projects/${projectId}/restore`, {
    reason: "Project lifecycle moderation smoke restore.",
  });
  expect((await request.get(`/api/projects/${projectId}`)).ok()).toBeTruthy();
  expect(reopeningPost.project?.id).toBe(projectId);
});
