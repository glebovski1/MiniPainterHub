const fs = require("fs");
const path = require("path");
const { expect } = require("@playwright/test");
const { loadMatrix } = require("../../scripts/resolve-ui-review-scope");
const { createRichViewerPost, openViewerFromDetails } = require("./viewer-scenario");

const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const REPO_ROOT = path.resolve(__dirname, "../../..");
const OUTPUT_DIR = path.resolve(REPO_ROOT, "output/playwright/ui-review");
const SCOPE_FILE = process.env.UI_REVIEW_SCOPE_FILE || path.join(OUTPUT_DIR, "scope.json");
const MATRIX = loadMatrix(path.resolve(__dirname, "../../ui-review.matrix.json"));

const VIEWPORTS = {
  desktop: { width: 1440, height: 1000, name: "desktop" },
  desktopCompact: { width: 1280, height: 1000, name: "desktop-1280" },
  mobile: { width: 390, height: 844, name: "mobile" }
};

function sanitizeFileToken(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function ensureOutputDir() {
  fs.rmSync(OUTPUT_DIR, { recursive: true, force: true });
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

function loadScope() {
  if (fs.existsSync(SCOPE_FILE)) {
    return JSON.parse(fs.readFileSync(SCOPE_FILE, "utf8"));
  }

  return {
    scope: "targeted",
    groups: MATRIX.defaultGroups,
    reasons: ["No scope file was provided. Falling back to the matrix default groups."],
    reviewCommand: "npm --prefix e2e run test:ui-review"
  };
}

function createManifest(scope) {
  return {
    generatedAt: new Date().toISOString(),
    scope: scope.scope,
    reviewCommand: scope.reviewCommand,
    groups: scope.groups,
    reasons: scope.reasons,
    captures: [],
    failures: []
  };
}

function writeArtifacts(scope, manifest) {
  const finalManifest = {
    ...manifest,
    completedAt: new Date().toISOString(),
    captureCount: manifest.captures.length
  };

  fs.writeFileSync(path.join(OUTPUT_DIR, "scope.json"), `${JSON.stringify(scope, null, 2)}\n`, "utf8");
  fs.writeFileSync(path.join(OUTPUT_DIR, "manifest.json"), `${JSON.stringify(finalManifest, null, 2)}\n`, "utf8");

  const lines = [
    "# UI Review Report",
    "",
    `- Scope: \`${scope.scope}\``,
    `- Groups: \`${scope.groups.join(", ")}\``,
    `- Review command: \`${scope.reviewCommand ?? "n/a"}\``,
    `- Captures: \`${finalManifest.captureCount}\``,
    "",
    "## Reasons",
    ...scope.reasons.map((reason) => `- ${reason}`),
    "",
    "## Captures"
  ];

  for (const capture of finalManifest.captures) {
    lines.push(
      `- ${capture.name}: ${capture.url} [${capture.viewport}] [${capture.authState}] [${capture.stateTags.join(", ")}] -> \`${capture.relativeFile}\``
    );
  }

  if (finalManifest.failures.length > 0) {
    lines.push("", "## Failures");
    for (const failure of finalManifest.failures) {
      lines.push(`- ${failure.group}: ${failure.message}`);
    }
  }

  fs.writeFileSync(path.join(OUTPUT_DIR, "report.md"), `${lines.join("\n")}\n`, "utf8");
}

async function settle(page, milliseconds = 450) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForTimeout(milliseconds);
}

function expectBoxContainedWithin(childBox, containerBox, label, tolerance = 2) {
  expect(childBox, `Expected ${label} to have a bounding box.`).toBeTruthy();
  expect(containerBox, `Expected ${label} container to have a bounding box.`).toBeTruthy();
  expect(childBox.x, `${label} should stay inside the container's left edge.`)
    .toBeGreaterThanOrEqual(containerBox.x - tolerance);
  expect(childBox.y, `${label} should stay inside the container's top edge.`)
    .toBeGreaterThanOrEqual(containerBox.y - tolerance);
  expect(childBox.x + childBox.width, `${label} should stay inside the container's right edge.`)
    .toBeLessThanOrEqual(containerBox.x + containerBox.width + tolerance);
  expect(childBox.y + childBox.height, `${label} should stay inside the container's bottom edge.`)
    .toBeLessThanOrEqual(containerBox.y + containerBox.height + tolerance);
}

async function expectImagesLoaded(images, expectedCount, label) {
  await expect(images, `Expected ${expectedCount} ${label}.`).toHaveCount(expectedCount);
  await images.evaluateAll((elements) => {
    for (const element of elements) {
      element.loading = "eager";
    }
  });
  await expect.poll(
    () => images.evaluateAll((elements) =>
      elements.every((element) => element.complete && element.naturalWidth > 0)),
    { message: `Expected every ${label} to load real image pixels.` },
  ).toBe(true);
}

async function hydrateLazyImages(page) {
  const lazyImages = page.locator('img[loading="lazy"]');
  if (await lazyImages.count() === 0) {
    return;
  }

  await lazyImages.evaluateAll((images) => {
    for (const image of images) {
      image.loading = "eager";
    }
  });

  await page.waitForFunction(
    () => Array.from(document.images).every((image) => image.complete),
    undefined,
    { timeout: 3_000 },
  ).catch(() => {});
  await page.waitForTimeout(100);
}

async function waitForViewerLayout(page) {
  await page.waitForFunction(() => {
    const image = document.querySelector("[data-testid='viewer-stage-image']");
    const stage = document.querySelector("[data-testid='viewer-stage']");
    const viewerClose = document.querySelector("[data-testid='viewer-close']");
    const panel = document.querySelector("[data-testid='viewer-side-panel']");
    if (!(image instanceof HTMLElement)
      || !(stage instanceof HTMLElement)
      || !(viewerClose instanceof HTMLElement)
      || !(panel instanceof HTMLElement)) {
      return false;
    }

    const imageRect = image.getBoundingClientRect();
    const stageRect = stage.getBoundingClientRect();
    const viewerCloseRect = viewerClose.getBoundingClientRect();
    const panelRect = panel.getBoundingClientRect();
    return stageRect.width > 300
      && stageRect.height > 220
      && imageRect.width > 150
      && imageRect.height > 150
      && viewerCloseRect.width > 30
      && panelRect.width > 220;
  });

  await expect.poll(() => page.getByTestId("viewer-close").boundingBox().then((box) => (box ? box.width : 0))).toBeGreaterThan(30);
}

async function getViewerFitRatio(page) {
  return page.locator(".viewer-stage__fitbox").evaluate((element) => {
    const width = Number.parseFloat(element.style.width);
    const height = Number.parseFloat(element.style.height);
    return width > 0 && height > 0 ? width / height : 0;
  });
}

async function cycleViewerToRatio(page, targetRatio) {
  const tolerance = 0.03;

  for (let attempt = 0; attempt < 8; attempt += 1) {
    const currentRatio = await getViewerFitRatio(page);
    if (Math.abs(currentRatio - targetRatio) <= tolerance) {
      return;
    }

    const previousSrc = await page.getByTestId("viewer-stage-image").getAttribute("src");
    await advanceViewer(page);
    await expect.poll(() => page.getByTestId("viewer-stage-image").getAttribute("src")).not.toBe(previousSrc);
    await settle(page, 200);
  }

  throw new Error(`Viewer never reached the expected ratio ${targetRatio.toFixed(3)}.`);
}

async function advanceViewer(page) {
  const viewport = page.viewportSize();
  const thumbnails = page.getByTestId("viewer-thumbnail");
  const thumbnailCount = await thumbnails.count();

  if (viewport && viewport.width < 992 && thumbnailCount > 1) {
    let activeIndex = 0;

    for (let index = 0; index < thumbnailCount; index += 1) {
      const thumbnail = thumbnails.nth(index);
      const selectedAttribute = await thumbnail.getAttribute("aria-selected");
      const isSelected = selectedAttribute?.toLowerCase();
      const className = await thumbnail.getAttribute("class");
      if (isSelected === "true" || className?.split(/\s+/).includes("is-active")) {
        activeIndex = index;
        break;
      }
    }

    await clickReliably(thumbnails.nth((activeIndex + 1) % thumbnailCount));
    return;
  }

  await clickReliably(page.getByTestId("viewer-stage-next"));
}

async function clickReliably(locator) {
  await locator.scrollIntoViewIfNeeded();

  try {
    await locator.click({ timeout: 5_000 });
  } catch {
    await locator.click({ force: true });
  }
}

async function getAuthToken(page) {
  const token = await page.evaluate(() => localStorage.getItem("authToken"));
  expect(token, "Expected an auth token in localStorage for the UI review setup.").toBeTruthy();
  return token;
}

async function sendAuthedRequest(page, request, method, url, data) {
  const response = await fetchAuthedRequest(page, request, method, url, data);
  expect(response.ok(), `${method} ${url} failed with HTTP ${response.status()}`).toBeTruthy();
  return response;
}

async function fetchAuthedRequest(page, request, method, url, data) {
  const token = await getAuthToken(page);
  return request.fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`
    },
    data
  });
}

function getPathSegment(url, prefix) {
  const pathname = new URL(url).pathname;
  if (!pathname.startsWith(prefix)) {
    throw new Error(`Expected URL path to start with '${prefix}' but received '${pathname}'.`);
  }

  return decodeURIComponent(pathname.slice(prefix.length));
}

async function resetAppState(request) {
  const response = await request.post("/api/test-support/reset", {
    headers: {
      "X-Test-Support-Token": RESET_TOKEN
    }
  });

  expect(response.ok(), `Reset failed with HTTP ${response.status()}`).toBeTruthy();
}

async function clearClientState(page) {
  await page.goto("/");
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.context().clearCookies();
}

async function startFreshState(page, request) {
  await clearClientState(page);
  await resetAppState(request);
  await clearClientState(page);
}

async function authenticateViaApi(page, request, userName, password) {
  const response = await request.post("/api/auth/login", {
    data: {
      userName,
      password
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
  await expect.poll(async () => page.evaluate(() => localStorage.getItem("authToken"))).toBeTruthy();

  const logoutLink = page.getByTestId("nav-logout");
  if (!(await logoutLink.isVisible().catch(() => false))) {
    const navToggle = page.getByRole("button", { name: /toggle navigation/i });
    if (await navToggle.isVisible().catch(() => false)) {
      await navToggle.click();
      await settle(page, 250);
    }
  }

  await expect(logoutLink).toBeAttached();
  await settle(page, 700);
}

async function requestAuthToken(request, userName, password) {
  const response = await request.post("/api/auth/login", {
    data: {
      userName,
      password
    }
  });

  expect(response.ok(), `Login API failed with HTTP ${response.status()}`).toBeTruthy();
  const payload = await response.json();
  expect(payload?.token, "Login API did not return a token.").toBeTruthy();
  return payload.token;
}

async function loginAsSeedUser(page, request) {
  await authenticateViaApi(page, request, "user", "User123!");
}

async function loginAsAdmin(page, request) {
  await authenticateViaApi(page, request, "admin", "P@ssw0rd!");
}

async function setViewport(page, viewport) {
  await page.setViewportSize({ width: viewport.width, height: viewport.height });
  await settle(page, 200);
}

async function capture(page, manifest, options) {
  const sequence = String(manifest.captures.length + 1).padStart(2, "0");
  const fileName = `${sequence}-${sanitizeFileToken(options.name)}-${options.viewport.name}.png`;
  const filePath = path.join(OUTPUT_DIR, fileName);

  await hydrateLazyImages(page);
  if (!options.preserveScroll) {
    await page.evaluate(() => window.scrollTo(0, 0));
  }
  await settle(page);
  if (options.elementTestId) {
    const target = page.getByTestId(options.elementTestId);
    await expect(target).toBeVisible();
    await target.screenshot({
      path: filePath,
      animations: "disabled"
    });
  } else {
    await page.screenshot({
      path: filePath,
      fullPage: options.fullPage ?? true,
      animations: "disabled"
    });
  }

  manifest.captures.push({
    name: options.name,
    group: options.group,
    url: page.url(),
    viewport: options.viewport.name,
    authState: options.authState,
    stateTags: options.stateTags,
    relativeFile: path.relative(REPO_ROOT, filePath).replace(/\\/g, "/")
  });
}

async function setDesktopPanelCollapsed(page, collapsed) {
  await page.goto("/");
  await settle(page, 600);
  const toggle = page.locator('button[aria-label="Toggle desktop panel"]');
  await expect(toggle).toBeVisible();
  const label = await toggle.textContent();
  const currentlyCollapsed = (label || "").includes("Show panel");

  if (currentlyCollapsed !== collapsed) {
    await clickReliably(toggle);
    await settle(page, 500);
  }
}

async function openMobilePanel(page) {
  await page.goto("/");
  await settle(page, 600);
  const button = page.locator('button[aria-controls="userPanelOffcanvas"]');
  const panel = page.locator("#userPanelOffcanvas");
  await expect(button).toBeVisible();
  await clickReliably(button);

  await expect(panel).toHaveClass(/show/);
  await expect(panel).toBeVisible({ timeout: 2_000 });

  await settle(page, 400);
}

async function ensureProfileExistsForCurrentUser(page, request, details) {
  let response = await fetchAuthedRequest(page, request, "GET", "/api/profiles/me");
  if (response.status() === 404) {
    await sendAuthedRequest(page, request, "POST", "/api/profiles/me", {
      displayName: details.displayName,
      bio: details.bio
    });
    response = await fetchAuthedRequest(page, request, "GET", "/api/profiles/me");
  }

  expect(response.ok(), `GET /api/profiles/me failed with HTTP ${response.status()}`).toBeTruthy();
  const profile = await response.json();
  expect(profile?.userId, "Expected the profile API to return a userId.").toBeTruthy();
  return profile;
}

async function ensureFollowedSeededAdmin(page, request) {
  await startFreshState(page, request);
  await loginAsAdmin(page, request);
  const adminProfile = await ensureProfileExistsForCurrentUser(page, request, {
    displayName: "Admin Painter",
    bio: "Hobby painter handling moderation, seeded showcases, fantasy armies, and coffee."
  });
  const adminUserId = adminProfile.userId;

  await clearClientState(page);
  await loginAsSeedUser(page, request);
  await sendAuthedRequest(page, request, "POST", `/api/follows/${encodeURIComponent(adminUserId)}`);
  return adminUserId;
}

async function ensureConversationWithSeededAdmin(page, request) {
  const adminUserId = await ensureFollowedSeededAdmin(page, request);
  const conversationResponse = await sendAuthedRequest(
    page,
    request,
    "POST",
    `/api/conversations/direct/${encodeURIComponent(adminUserId)}`,
  );
  const conversation = await conversationResponse.json();
  await sendAuthedRequest(page, request, "POST", `/api/conversations/${conversation.id}/messages`, {
    body: `UI review containment ${"unbroken-preview-content-".repeat(16)}`
  });
  await page.goto(`/messages/${conversation.id}`);
  await settle(page, 700);
}

async function ensureReportedSeededPost(page, request) {
  await startFreshState(page, request);
  await loginAsSeedUser(page, request);
  await page.goto("/");
  await settle(page, 700);
  const seededCard = page.locator(".post-card", { hasText: "Seeded: glazing check" }).first();
  await expect(seededCard).toBeVisible();
  await clickReliably(seededCard.getByRole("link", { name: /Seeded: glazing check/i }).first());
  await settle(page, 700);
  const postId = getPathSegment(page.url(), "/posts/");
  await sendAuthedRequest(page, request, "POST", `/api/reports/posts/${encodeURIComponent(postId)}`, {
    reasonCode: "Spam",
    details: "UI review seeded report."
  });
}

async function createProjectReviewPost(page, request, suffix, projectId, milestoneLabel) {
  const response = await sendAuthedRequest(page, request, "POST", "/api/posts", {
    title: `Project review ${suffix}`,
    content: `Image-backed progress update prepared for the ${suffix} project review state.`,
    tags: ["project-review"],
    images: [{
      imageUrl: "/uploads/images/9_minis1.jpg",
      previewUrl: "/uploads/images/9_minis1.jpg",
      thumbnailUrl: "/uploads/images/9_minis1.jpg",
      width: 1600,
      height: 1200
    }],
    projectId,
    milestoneLabel
  });
  return response.json();
}

const scenarioGroups = {
  async shell({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);

    await setViewport(page, VIEWPORTS.desktop);
    await setDesktopPanelCollapsed(page, false);
    await capture(page, manifest, {
      name: "shell-home-panel-open",
      group: "shell",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["shell", "community", "panel-open", "desktop"]
    });

    await setDesktopPanelCollapsed(page, true);
    await capture(page, manifest, {
      name: "shell-home-panel-collapsed",
      group: "shell",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["shell", "community", "panel-collapsed", "desktop"]
    });

    await setViewport(page, VIEWPORTS.desktopCompact);
    await page.goto("/");
    await capture(page, manifest, {
      name: "shell-home-compact-desktop",
      group: "shell",
      viewport: VIEWPORTS.desktopCompact,
      authState: "seed-user",
      stateTags: ["shell", "community", "desktop", "compact-width"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    const navToggle = page.getByTestId("nav-toggle");
    const navCollapse = page.locator("#navbarNav");
    await clickReliably(navToggle);
    await expect(navToggle).toHaveAttribute("aria-expanded", "true");
    await expect(navCollapse).toHaveClass(/show/);
    await clickReliably(page.getByTestId("nav-more"));
    await expect(page.getByTestId("nav-more-about")).toBeVisible();
    await capture(page, manifest, {
      name: "shell-home-mobile-navigation-open",
      group: "shell",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["shell", "community", "mobile", "navigation-open"]
    });

    await clickReliably(page.getByTestId("nav-search-link"));
    await page.waitForURL((url) => url.pathname === "/search");
    await expect(navToggle).toHaveAttribute("aria-expanded", "false");
    await expect(navCollapse).not.toHaveClass(/show/);

    await openMobilePanel(page);
    await capture(page, manifest, {
      name: "shell-home-mobile-panel-open",
      group: "shell",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["shell", "community", "mobile", "panel-open"]
    });
  },

  async auth({ page, request, manifest }) {
    await startFreshState(page, request);

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/login");
    await expect(page.getByTestId("google-signin-link")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-login-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "entry"]
    });

    await page.getByTestId("login-username").fill("user");
    await page.getByTestId("login-password").fill("wrong-password");
    await clickReliably(page.getByTestId("login-submit"));
    await expect(page.getByTestId("login-error")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-login-error-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "error"]
    });

    await setViewport(page, VIEWPORTS.desktopCompact);
    await capture(page, manifest, {
      name: "auth-login-error-compact-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktopCompact,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "compact-width", "error"]
    });

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/register");
    await capture(page, manifest, {
      name: "auth-register-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "entry"]
    });

    await page.route("**/api/auth/register", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          isSuccess: true,
          requiresEmailConfirmation: true,
          confirmationEmailSent: true
        })
      });
    });
    await page.getByTestId("register-username").fill("review-painter");
    await page.getByTestId("register-email").fill("review@example.test");
    await page.getByTestId("register-password").fill("ReviewPass123!");
    await clickReliably(page.getByTestId("register-submit"));
    await expect(page.getByTestId("register-confirmation-sent")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-register-confirmation-sent-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "registration", "confirmation-sent"]
    });
    await page.unroute("**/api/auth/register");

    await page.goto("/resend-confirmation");
    await expect(page.getByTestId("resend-confirmation-form")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-resend-confirmation-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "resend", "entry"]
    });

    await page.route("**/api/auth/email/resend", async (route) => {
      await route.fulfill({ status: 202, body: "" });
    });
    await page.getByTestId("resend-confirmation-email").fill("review@example.test");
    await clickReliably(page.getByTestId("resend-confirmation-submit"));
    await expect(page.getByTestId("resend-confirmation-success")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-resend-confirmation-success-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "resend", "accepted"]
    });
    await page.unroute("**/api/auth/email/resend");

    await page.route("**/api/auth/email/confirm", async (route) => {
      await route.fulfill({ status: 204, body: "" });
    });
    await page.goto("/confirm-email?userId=review-user&code=review-token&returnUrl=%2Fsupport");
    await expect(page.getByTestId("confirm-email-success")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-confirm-email-success-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "confirmation", "success"]
    });
    await page.unroute("**/api/auth/email/confirm");

    await page.goto("/confirm-email");
    await expect(page.getByTestId("confirm-email-invalid")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-confirm-email-invalid-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "confirmation", "invalid"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await page.goto("/login");
    await capture(page, manifest, {
      name: "auth-login-mobile",
      group: "auth",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["auth", "mobile", "entry"]
    });

    await startFreshState(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/api/auth/google/start?fake=conflict");
    await page.waitForURL((url) => url.pathname === "/auth/external/callback");
    await expect(page.getByTestId("external-auth-result-title")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-google-email-conflict-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "google", "desktop", "conflict"]
    });

    await startFreshState(page, request);
    await page.goto("/api/auth/google/start");
    await page.waitForURL((url) => url.pathname === "/auth/external/complete-registration");
    await expect(page.getByTestId("external-registration-form")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-google-registration-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "google", "desktop", "onboarding"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "auth-google-registration-mobile",
      group: "auth",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["auth", "google", "mobile", "onboarding"]
    });

    await startFreshState(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/api/auth/discord/start");
    await page.waitForURL((url) => url.pathname === "/auth/external/complete-registration");
    await expect(page.getByText(/Discord verified/)).toBeVisible();
    await capture(page, manifest, {
      name: "auth-discord-registration-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "discord", "desktop", "onboarding"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "auth-discord-registration-mobile",
      group: "auth",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["auth", "discord", "mobile", "onboarding"]
    });

    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/account/sign-in-methods");
    await expect(page.getByTestId("sign-in-methods-content")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-sign-in-methods-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["auth", "account", "desktop", "google-available"]
    });

    await page.route("**/api/auth/sign-in-methods", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          hasPassword: false,
          googleConnected: true,
          canDisconnectGoogle: false,
          discordConnected: false,
          canDisconnectDiscord: false
        })
      });
    });
    await page.goto("/account/sign-in-methods");
    await expect(page.getByTestId("google-disconnect-blocked")).toBeVisible();
    await capture(page, manifest, {
      name: "auth-sign-in-methods-google-only-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "google-user",
      stateTags: ["auth", "account", "desktop", "google-only"]
    });
    await page.unroute("**/api/auth/sign-in-methods");
  },

  async legal({ page, request, manifest }) {
    await startFreshState(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/privacy");
    await expect(page.getByRole("heading", { name: "Privacy at Roll & Paint" })).toBeVisible();
    await capture(page, manifest, {
      name: "legal-privacy-desktop",
      group: "legal",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["legal", "privacy", "desktop"]
    });

    await page.goto("/terms");
    await expect(page.getByRole("heading", { name: "Roll & Paint Terms" })).toBeVisible();
    await capture(page, manifest, {
      name: "legal-terms-desktop",
      group: "legal",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["legal", "terms", "desktop"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "legal-terms-mobile",
      group: "legal",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["legal", "terms", "mobile"]
    });
  },

  async community({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/");
    await capture(page, manifest, {
      name: "community-home-desktop",
      group: "community",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["community", "desktop", "populated"]
    });

    await page.goto("/posts/all");
    await capture(page, manifest, {
      name: "community-archive-desktop",
      group: "community",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["community", "desktop", "archive"]
    });

    await page.goto("/posts/top");
    await page
      .locator("[data-testid='top-posts-carousel'], [data-testid='top-posts-error']")
      .first()
      .waitFor({ state: "visible", timeout: 10_000 });
    await capture(page, manifest, {
      name: "community-top-posts-desktop",
      group: "community",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["community", "desktop", "showcase"]
    });
  },

  async projects({ page, request, manifest }) {
    await startFreshState(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    const publicProjectListPattern = /\/api\/projects\?.*/;
    await page.route(publicProjectListPattern, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ items: [], totalCount: 0, pageNumber: 1, pageSize: 12 })
      });
    });
    await page.goto("/projects");
    await expect(page.getByTestId("projects-empty")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-public-empty-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "empty"]
    });
    await page.unroute(publicProjectListPattern);

    await page.route(publicProjectListPattern, async (route) => {
      await route.fulfill({
        status: 503,
        contentType: "application/problem+json",
        body: JSON.stringify({ title: "Review outage", status: 503 })
      });
    });
    await page.reload();
    await expect(page.getByTestId("projects-error")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-public-error-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "error", "retry"]
    });
    await page.unroute(publicProjectListPattern);

    await loginAsSeedUser(page, request);
    await page.goto("/projects/new");
    await clickReliably(page.getByTestId("project-create-submit"));
    await expect(page.locator(".validation-message").first()).toBeVisible();
    await capture(page, manifest, {
      name: "projects-create-validation-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "create", "validation"]
    });

    const projectResponse = await sendAuthedRequest(page, request, "POST", "/api/projects", {
      title: "UI Review Alpine Cohort",
      description: "A winter army diary with deliberate milestones, image-backed updates, and a curated finished showcase.",
      kind: "Army",
      gameSystem: "Smokehammer",
      factionTheme: "Alpine expedition",
      goal: "Finish two cohesive checkpoints for the review wall"
    });
    const project = await projectResponse.json();
    const profile = await ensureProfileExistsForCurrentUser(page, request, {
      displayName: "Project Review Painter",
      bio: "A hobby painter fueled by tea, building fantasy armies through image-backed diaries and finished showcases."
    });

    await page.goto(`/projects/${project.id}`);
    await expect(page.getByTestId("project-entries-empty")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-owner-empty-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "empty", "setup"]
    });

    await page.goto(`/posts/new?projectId=${project.id}`);
    await expect(page.getByTestId("create-post-project")).toHaveValue(String(project.id));
    await page.getByTestId("create-post-milestone").fill("First checkpoint");
    await capture(page, manifest, {
      name: "projects-aware-composer-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "composer", "milestone", "preselected"]
    });

    const undercoatPost = await createProjectReviewPost(page, request, "undercoat", project.id, "Palette locked");
    const commandPost = await createProjectReviewPost(page, request, "command", project.id, "Command group complete");

    await page.goto(`/projects/${project.id}?view=diary`);
    await expect(page.getByTestId("project-diary")).toContainText("Palette locked");
    await expect(page.getByTestId("project-diary")).toContainText("Command group complete");
    await capture(page, manifest, {
      name: "projects-owner-diary-populated-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "diary", "populated", "milestones"]
    });

    await page.goto("/projects/mine");
    await expect(page.getByTestId("my-project-list")).toContainText(project.title);
    const ownerCard = page.getByTestId("hobby-project-card").filter({ hasText: project.title }).first();
    expectBoxContainedWithin(
      await ownerCard.locator(".hobby-project-card__kind").boundingBox(),
      await ownerCard.locator(".hobby-project-card__media").boundingBox(),
      "project kind badge",
    );
    await capture(page, manifest, {
      name: "projects-owner-dashboard-populated-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "dashboard", "populated"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "projects-owner-dashboard-populated-mobile",
      group: "projects",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["projects", "owner", "mobile", "dashboard", "populated"]
    });

    await setViewport(page, VIEWPORTS.desktop);

    await page.goto(`/projects/${project.id}/edit`);
    await expect(page.getByTestId("project-entry-manager")).toBeVisible();
    await expect(page.getByTestId("available-post-list")).not.toContainText(undercoatPost.title);
    await expect(page.getByTestId("available-post-list")).not.toContainText(commandPost.title);
    await expectImagesLoaded(
      page.getByTestId("project-entry-manager").locator("img"),
      2,
      "linked project-management images",
    );
    await capture(page, manifest, {
      name: "projects-owner-management-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "management", "populated"]
    });

    await sendAuthedRequest(page, request, "PUT", `/api/projects/${project.id}/showcase`, {
      postIds: [undercoatPost.id, commandPost.id]
    });
    await sendAuthedRequest(page, request, "PUT", `/api/projects/${project.id}/showcase`, {
      postIds: [commandPost.id, undercoatPost.id]
    });
    await sendAuthedRequest(page, request, "PUT", `/api/projects/${project.id}/cover`, {
      postId: commandPost.id
    });
    await sendAuthedRequest(page, request, "PUT", `/api/projects/${project.id}/status`, {
      status: "Completed"
    });

    await page.goto(`/projects/${project.id}?view=showcase`);
    await expect(page.getByTestId("project-showcase")).toBeVisible();
    await expectImagesLoaded(
      page.getByTestId("project-showcase").locator("img"),
      2,
      "owner showcase images",
    );
    await capture(page, manifest, {
      name: "projects-owner-completed-showcase-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "completed", "showcase", "populated"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "projects-owner-completed-showcase-mobile",
      group: "projects",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["projects", "owner", "mobile", "completed", "showcase"]
    });

    await setViewport(page, VIEWPORTS.desktop);
    await sendAuthedRequest(page, request, "POST", `/api/projects/${project.id}/archive`);
    await page.goto(`/projects/${project.id}`);
    await expect(page.getByTestId("project-archived-state")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-owner-archived-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "archived"]
    });

    await sendAuthedRequest(page, request, "POST", `/api/projects/${project.id}/restore`);
    const moderatorToken = await requestAuthToken(request, "admin", "P@ssw0rd!");
    const hideResponse = await request.post(`/api/moderation/projects/${project.id}/hide`, {
      headers: { Authorization: `Bearer ${moderatorToken}` },
      data: { reason: "UI review hidden project state." }
    });
    expect(hideResponse.ok(), `Project hide failed with HTTP ${hideResponse.status()}`).toBeTruthy();
    await page.goto(`/projects/${project.id}`);
    await expect(page.getByTestId("project-hidden-state")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-owner-hidden-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "moderation-hidden"]
    });

    await page.goto("/projects/mine");
    await expect(page.getByTestId("my-project-list")).toContainText(project.title);
    await capture(page, manifest, {
      name: "projects-owner-dashboard-hidden-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["projects", "owner", "desktop", "dashboard", "moderation-hidden"]
    });

    const restoreResponse = await request.post(`/api/moderation/projects/${project.id}/restore`, {
      headers: { Authorization: `Bearer ${moderatorToken}` },
      data: { reason: "UI review restored project state." }
    });
    expect(restoreResponse.ok(), `Project restore failed with HTTP ${restoreResponse.status()}`).toBeTruthy();

    await clearClientState(page);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/projects");
    await expect(page.getByTestId("project-list")).toContainText(project.title);
    await capture(page, manifest, {
      name: "projects-public-discovery-populated-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "discovery", "populated"]
    });

    await page.goto(`/projects/${project.id}?view=diary`);
    await expect(page.getByTestId("project-diary")).toBeVisible();
    await capture(page, manifest, {
      name: "projects-public-diary-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "diary", "populated"]
    });

    await page.goto(`/projects/${project.id}?view=showcase`);
    await expect(page.getByTestId("project-showcase")).toBeVisible();
    await expectImagesLoaded(
      page.getByTestId("project-showcase").locator("img"),
      2,
      "public showcase images",
    );
    await capture(page, manifest, {
      name: "projects-public-showcase-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "completed", "showcase"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "projects-public-showcase-mobile",
      group: "projects",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "mobile", "completed", "showcase"]
    });

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto(`/users/${encodeURIComponent(profile.userId)}`);
    await expect(page.getByTestId("profile-projects")).toContainText(project.title);
    await expect(page.locator(".public-profile-card__summary"))
      .toHaveText("Hobby painter with tea on deck and fantasy streak in the bio.");
    await expect(page.locator(".public-profile-card__bio"))
      .toHaveText("A hobby painter fueled by tea, building fantasy armies through image-backed diaries and finished showcases.");
    await capture(page, manifest, {
      name: "projects-public-profile-integration-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "profile", "integration"]
    });

    await page.goto(`/search?q=${encodeURIComponent("UI Review Alpine")}&tab=projects`);
    await expect(page.getByTestId("search-projects-results")).toContainText(project.title);
    await capture(page, manifest, {
      name: "projects-public-search-integration-desktop",
      group: "projects",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["projects", "public", "desktop", "search", "integration"]
    });
  },

  async about({ page, request, manifest }) {
    await startFreshState(page, request);

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/about");
    await capture(page, manifest, {
      name: "about-manifesto-desktop",
      group: "about",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["about", "desktop", "manifesto"]
    });

    await setViewport(page, VIEWPORTS.desktopCompact);
    await capture(page, manifest, {
      name: "about-manifesto-compact-desktop",
      group: "about",
      viewport: VIEWPORTS.desktopCompact,
      authState: "unauthenticated",
      stateTags: ["about", "desktop", "compact-width", "manifesto"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await page.goto("/about");
    await capture(page, manifest, {
      name: "about-manifesto-mobile",
      group: "about",
      viewport: VIEWPORTS.mobile,
      authState: "unauthenticated",
      stateTags: ["about", "mobile", "manifesto"]
    });
  },

  async search({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/search?q=weathering&tab=posts");
    await expect(page.getByTestId("search-post-result").first()).toBeVisible();
    await capture(page, manifest, {
      name: "search-post-results-desktop",
      group: "search",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["search", "desktop", "posts-results"]
    });

    await setViewport(page, VIEWPORTS.desktopCompact);
    await capture(page, manifest, {
      name: "search-post-results-compact-desktop",
      group: "search",
      viewport: VIEWPORTS.desktopCompact,
      authState: "seed-user",
      stateTags: ["search", "desktop", "compact-width", "posts-results"]
    });

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/search?q=user&tab=users");
    await expect(page.getByTestId("search-user-result").first()).toBeVisible();
    await capture(page, manifest, {
      name: "search-user-results-desktop",
      group: "search",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["search", "desktop", "users-results"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await page.goto("/search?q=weathering&tab=posts");
    await expect(page.getByTestId("search-post-result").first()).toBeVisible();
    await capture(page, manifest, {
      name: "search-post-results-mobile",
      group: "search",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["search", "mobile", "posts-results"]
    });
  },

  async posts({ page, request, manifest }) {
    await startFreshState(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/posts/new");
    await page.waitForURL((url) => url.pathname === "/login" || url.pathname === "/posts/new", { timeout: 5_000 });
    await settle(page, 700);
    await capture(page, manifest, {
      name: "posts-new-auth-gated",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["posts", "desktop", "auth-gated"]
    });

    await loginAsSeedUser(page, request);
    await page.goto("/posts/new");
    await expect(page.getByTestId("create-post-draft-restored")).toHaveCount(0);
    await expect(page.getByTestId("create-post-title")).toHaveValue("");
    await expect(page.getByTestId("create-post-content")).toHaveValue("");
    await expect(page.getByTestId("create-post-tags")).toHaveValue("");
    await expect(page.getByTestId("create-post-project")).toHaveValue("");
    await expect(page.getByTestId("create-post-project-message")).toHaveCount(0);
    const activeCreateBackground = await page.locator(".dashboard-create-btn:visible").first()
      .evaluate((element) => getComputedStyle(element).backgroundColor);
    expect(activeCreateBackground, "The active New post action must not fall back to Bootstrap blue.")
      .not.toMatch(/^rgb\((?:10, 88, 202|11, 94, 215|13, 110, 253)\)$/);
    await capture(page, manifest, {
      name: "posts-new-composer",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "composer"]
    });

    await clickReliably(page.getByTestId("create-post-submit"));
    await expect(page.getByText("The Title field is required.", { exact: true })).toBeVisible();
    await expect(page.getByText("The Content field is required.", { exact: true })).toBeVisible();
    const focusedPublishBackground = await page.getByTestId("create-post-submit")
      .evaluate((element) => getComputedStyle(element).backgroundColor);
    expect(focusedPublishBackground, "The focused Publish post action must not fall back to Bootstrap blue.")
      .not.toMatch(/^rgb\((?:10, 88, 202|11, 94, 215|13, 110, 253)\)$/);
    await capture(page, manifest, {
      name: "posts-new-validation",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "validation"]
    });

    await page.goto("/");
    const seededCard = page.locator(".post-card", { hasText: "Seeded: glazing check" }).first();
    await clickReliably(seededCard.getByRole("link", { name: /Seeded: glazing check/i }).first());
    await capture(page, manifest, {
      name: "posts-detail-seeded",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "details", "populated"]
    });

    const seededDetailsUrl = page.url();
    await openViewerFromDetails(page);
    await expect(page.getByTestId("rich-image-viewer")).toHaveClass(/is-single-image/);
    await expect(page.getByTestId("viewer-thumbnail-rail")).toHaveCount(0);
    await expect(page.getByTestId("viewer-stage-prev")).toHaveCount(0);
    await expect(page.getByTestId("viewer-stage-next")).toHaveCount(0);
    await capture(page, manifest, {
      name: "posts-detail-single-image-viewer-desktop",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "single-image"]
    });

    await setViewport(page, VIEWPORTS.desktopCompact);
    await capture(page, manifest, {
      name: "posts-detail-single-image-viewer-compact-desktop",
      group: "posts",
      viewport: VIEWPORTS.desktopCompact,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "compact-width", "viewer-open", "single-image"]
    });
    await clickReliably(page.getByTestId("viewer-close"));

    await setViewport(page, VIEWPORTS.mobile);
    await page.goto(seededDetailsUrl);
    await openViewerFromDetails(page);
    await expect(page.getByTestId("rich-image-viewer")).toHaveClass(/is-single-image/);
    await expect(page.getByTestId("viewer-thumbnail-rail")).toHaveCount(0);
    await capture(page, manifest, {
      name: "posts-detail-single-image-viewer-mobile",
      group: "posts",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "mobile", "viewer-open", "single-image"]
    });
    await clickReliably(page.getByTestId("viewer-close"));
    await setViewport(page, VIEWPORTS.desktop);

    const richViewerPost = await createRichViewerPost(page, request, "ui-review", { extraPlainCommentsCount: 12 });
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-closed",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "details", "viewer-closed", "populated"]
    });

    const inlineThumbnails = page.getByTestId("post-details-thumbnail");
    await expect(inlineThumbnails).toHaveCount(6);
    await clickReliably(inlineThumbnails.nth(1));
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
    await expect(inlineThumbnails.nth(1)).toHaveAttribute("aria-current", "true");
    await settle(page, 250);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-inline-image-selected",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "details", "viewer-closed", "inline-image-selected"]
    });
    await clickReliably(inlineThumbnails.nth(0));

    await openViewerFromDetails(page);
    await waitForViewerLayout(page);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-portrait",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "portrait"]
    });

    await page.getByTestId("viewer-stage").hover();
    await clickReliably(page.getByTestId("viewer-view-fill"));
    await waitForViewerLayout(page);
    await settle(page, 250);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-fill",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "fill"]
    });

    await clickReliably(page.getByTestId("viewer-side-tab-comments"));
    await settle(page, 250);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-comments-tab",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "comments-tab"]
    });

    const viewerCommentsScroll = page.getByTestId("viewer-comments-scroll");
    const viewerCommentItems = page.locator("[data-testid='viewer-comments-thread'] [data-testid='comment-item']");
    await viewerCommentsScroll.hover({ position: { x: 8, y: 120 } });
    await page.mouse.wheel(0, 220);
    await viewerCommentsScroll.evaluate((element) => {
      element.scrollTop = element.scrollHeight;
    });
    await expect(viewerCommentItems).toHaveCount(richViewerPost.extraComments.length + 4);
    await expect(page.getByTestId("viewer-comments-load-sentinel")).toHaveCount(0);
    await viewerCommentsScroll.evaluate((element) => {
      element.scrollTop = Math.round((element.scrollHeight - element.clientHeight) * 0.45);
    });
    await settle(page, 180);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-comments-tab-scrolled",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "comments-tab", "scrolled"]
    });

    await page.getByTestId("viewer-stage").hover();
    await clickReliably(page.getByTestId("viewer-reset"));
    await clickReliably(page.getByTestId("viewer-side-tab-info"));
    await waitForViewerLayout(page);

    await cycleViewerToRatio(page, 1);
    await settle(page, 300);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-square",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "square"]
    });

    await cycleViewerToRatio(page, 21 / 9);
    await settle(page, 300);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-panorama",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "panorama"]
    });

    await clickReliably(page.getByTestId("viewer-close"));
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
    const pageComment = page
      .locator("[data-testid='comment-list-container']")
      .first()
      .getByTestId("comment-item")
      .filter({ hasText: "Square image anchor should stay centered while the comment panel highlights this thread" })
      .first();
    await pageComment.scrollIntoViewIfNeeded();
    await settle(page, 250);
    await capture(page, manifest, {
      name: "posts-detail-comment-mark-action-row",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      preserveScroll: true,
      stateTags: ["posts", "desktop", "comments", "populated", "marked-comment"]
    });
    await clickReliably(pageComment.getByTestId("comment-show-mark"));
    await expect(page.getByTestId("viewer-comment-mark")).toBeVisible();
    await expect(page.getByTestId("viewer-comment-state")).toContainText(`#${richViewerPost.markedCommentTwo.id}`);
    await expect(page.getByTestId("viewer-skeleton")).toHaveCount(0);
    await expect(page.getByTestId("viewer-stage-image")).toBeVisible();
    await expect(page.getByTestId("viewer-close")).toBeVisible();
    await waitForViewerLayout(page);
    await settle(page, 500);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-active-comment",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "active-comment-mark"]
    });

    await clickReliably(page.getByTestId("viewer-close"));
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
    await page.goto(`/posts/${richViewerPost.postId}`);
    await settle(page, 700);
    await openViewerFromDetails(page);

    const stage = page.getByTestId("viewer-stage");
    const stageBox = await stage.boundingBox();
    expect(stageBox).toBeTruthy();
    await page.mouse.move(stageBox.x + stageBox.width * 0.55, stageBox.y + stageBox.height * 0.52);
    await page.mouse.wheel(0, -260);
    await page.mouse.down();
    await page.mouse.move(stageBox.x + stageBox.width * 0.4, stageBox.y + stageBox.height * 0.42, { steps: 7 });
    await page.mouse.up();
    await waitForViewerLayout(page);
    await settle(page, 300);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-zoomed-pan",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "zoomed", "panned"]
    });

    await clickReliably(page.getByTestId("viewer-close"));
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);
    await openViewerFromDetails(page);
    await clickReliably(page.getByTestId("viewer-add-note"));
    const [placementStageBox, fitBox] = await Promise.all([
      page.getByTestId("viewer-stage").boundingBox(),
      page.getByTestId("viewer-stage-fitbox").boundingBox(),
    ]);
    expect(placementStageBox).toBeTruthy();
    expect(fitBox).toBeTruthy();
    await page.getByTestId("viewer-stage").click({
      position: {
        x: Math.round((fitBox.x - placementStageBox.x) + (fitBox.width * 0.6)),
        y: Math.round((fitBox.y - placementStageBox.y) + (fitBox.height * 0.44)),
      },
    });
    await expect(page.getByTestId("viewer-mark-composer")).toBeVisible();
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-author-note-composer",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "author-note", "composer"]
    });
    await clickReliably(page.getByTestId("viewer-mark-close"));
    await expect(page.getByTestId("viewer-mark-composer")).toHaveCount(0);

    const delayedImagePattern = new RegExp(richViewerPost.primaryImage.previewUrl.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
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
    await page.getByTestId("viewer-stage-image").dispatchEvent("error");
    await expect(page.getByTestId("viewer-image-error")).toBeVisible();
    await clickReliably(page.getByRole("button", { name: "Retry", exact: true }));
    await Promise.race([
      delayedImageRequested,
      page.waitForTimeout(5_000).then(() => {
        throw new Error("The rich-viewer retry did not request the cache-busted image within five seconds.");
      })
    ]);
    await expect(page.getByTestId("viewer-skeleton")).toHaveCount(1);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-loading",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "loading"]
    });
    releaseDelayedImage();
    await expect(page.getByTestId("viewer-skeleton")).toHaveCount(0);
    await page.unroute(delayedImagePattern);

    const failingImagePattern = /\/uploads\/images\/.*_preview\.(webp|jpg|png)$/;
    await page.route(failingImagePattern, async (route) => {
      if (route.request().url().includes(richViewerPost.primaryImage.previewUrl)) {
        await route.continue();
        return;
      }

      await route.abort("failed");
    });
    await clickReliably(page.getByTestId("viewer-stage-next"));
    await expect(page.getByTestId("viewer-image-error")).toBeVisible();
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-error",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "error"]
    });
    await page.unroute(failingImagePattern);

    await clickReliably(page.getByTestId("viewer-close"));
    await expect(page.getByTestId("rich-image-viewer-modal")).toHaveCount(0);

    await setViewport(page, VIEWPORTS.mobile);
    await page.goto(`/posts/${richViewerPost.postId}`);
    await settle(page, 700);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-closed-mobile",
      group: "posts",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["posts", "mobile", "details", "viewer-closed", "populated"]
    });

    await openViewerFromDetails(page);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-mobile-portrait",
      group: "posts",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "mobile", "viewer-open", "portrait"]
    });

    await cycleViewerToRatio(page, 21 / 9);
    await settle(page, 250);
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-open-mobile-panorama",
      group: "posts",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      fullPage: false,
      stateTags: ["posts", "mobile", "viewer-open", "panorama"]
    });

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/posts/mine");
    await page
      .locator(".post-card, .empty-state, [data-testid='my-posts-error']")
      .first()
      .waitFor({ state: "visible", timeout: 10_000 });
    await capture(page, manifest, {
      name: "posts-mine-desktop",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "mine", "populated"]
    });

    await startFreshState(page, request);
    await loginAsAdmin(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await createRichViewerPost(page, request, "ui-review-admin-comments", { extraPlainCommentsCount: 6 });
    await openViewerFromDetails(page);
    await waitForViewerLayout(page);
    await clickReliably(page.getByTestId("viewer-side-tab-comments"));
    const adminViewerPanel = page.getByTestId("viewer-side-panel");
    await expect(adminViewerPanel.getByTestId("comment-visibility-filter")).toBeVisible();
    await expect(adminViewerPanel.getByTestId("comment-visibility-select")).toHaveValue("active");
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-comments-admin-desktop",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      fullPage: false,
      stateTags: ["posts", "desktop", "viewer-open", "comments-tab", "admin", "visibility-filter"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await adminViewerPanel.getByTestId("viewer-comments-scroll").evaluate((element) => {
      element.scrollTop = 0;
    });
    await settle(page, 150);
    await expect(adminViewerPanel.getByTestId("comment-visibility-filter")).toBeVisible();
    await capture(page, manifest, {
      name: "posts-detail-rich-viewer-comments-admin-mobile",
      group: "posts",
      viewport: VIEWPORTS.mobile,
      authState: "admin",
      fullPage: false,
      stateTags: ["posts", "mobile", "viewer-open", "comments-tab", "admin", "visibility-filter"]
    });
    await clickReliably(page.getByTestId("viewer-close"));
  },

  async following({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/feed/following");
    await capture(page, manifest, {
      name: "following-empty-desktop",
      group: "following",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["following", "desktop", "empty"]
    });

    await ensureFollowedSeededAdmin(page, request);
    await page.goto("/feed/following");
    await capture(page, manifest, {
      name: "following-populated-desktop",
      group: "following",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["following", "desktop", "populated"]
    });
  },

  async profile({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/profile");
    await capture(page, manifest, {
      name: "profile-empty-desktop",
      group: "profile",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["profile", "desktop", "empty"]
    });

    await ensureProfileExistsForCurrentUser(page, request, {
      displayName: "User Painter",
      bio: "Builds display pieces and weathered warbands."
    });
    await page.goto("/profile");
    await capture(page, manifest, {
      name: "profile-populated-desktop",
      group: "profile",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["profile", "desktop", "populated"]
    });

    await startFreshState(page, request);
    await loginAsAdmin(page, request);
    const adminProfile = await ensureProfileExistsForCurrentUser(page, request, {
      displayName: "Admin Painter",
      bio: "Hobby painter handling moderation, seeded showcases, fantasy armies, and coffee."
    });
    await clearClientState(page);
    await loginAsSeedUser(page, request);
    await page.goto(`/users/${encodeURIComponent(adminProfile.userId)}`);
    await expect(page.locator(".public-profile-card__summary"))
      .toHaveText("Hobby painter with coffee on deck and fantasy streak in the bio.");
    await expect(page.locator(".public-profile-card__bio"))
      .toHaveText("Hobby painter handling moderation, seeded showcases, fantasy armies, and coffee.");
    await settle(page, 700);
    await capture(page, manifest, {
      name: "profile-public-desktop",
      group: "profile",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["profile", "desktop", "public", "populated"]
    });
  },

  async messages({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/messages");
    await expect(page.getByRole("heading", { name: "No conversations yet", exact: true })).toBeVisible();
    await expect(page.locator(".message-inbox-panel .spinner-border")).toHaveCount(0);
    await capture(page, manifest, {
      name: "messages-empty-desktop",
      group: "messages",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["messages", "desktop", "empty"]
    });

    await ensureConversationWithSeededAdmin(page, request);
    await capture(page, manifest, {
      name: "messages-populated-desktop",
      group: "messages",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["messages", "desktop", "populated"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "messages-populated-mobile",
      group: "messages",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["messages", "mobile", "populated"]
    });
  },

  async support({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/support");
    await expect(page.getByTestId("support-empty")).toBeVisible();
    await capture(page, manifest, {
      name: "support-empty-desktop",
      group: "support",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["support", "desktop", "empty"]
    });

    await page.goto("/support/new");
    await page.getByTestId("support-create-submit").click();
    await capture(page, manifest, {
      name: "support-create-validation-desktop",
      group: "support",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["support", "desktop", "composer", "validation"]
    });

    await page.getByTestId("support-category").selectOption("Bug");
    await page.getByTestId("support-subject").fill("UI review upload issue");
    await page.getByTestId("support-message-input").fill("The upload stops before the preview appears. This seeded request verifies the complete support thread layout.");
    // Blazor's InputText components commit their model value on `change`. Explicitly
    // blur the last field before requestSubmit so the review does not depend on a
    // synthetic button click racing the final bound-value update.
    await page.getByTestId("support-message-input").blur();
    await settle(page, 200);
    await expect(page.getByTestId("support-category")).toHaveValue("Bug");
    await expect(page.getByTestId("support-subject")).toHaveValue("UI review upload issue");
    const createResponse = page.waitForResponse(
      response => response.request().method() === "POST" && response.url().endsWith("/api/support/tickets"),
      { timeout: 15_000 });
    await page.getByTestId("support-create-form").locator("form").evaluate(form => form.requestSubmit());
    await expect((await createResponse).ok()).toBeTruthy();
    await expect(page).toHaveURL(/\/support\/\d+$/);
    await expect(page.getByTestId("support-ticket-detail")).toBeVisible();
    await capture(page, manifest, {
      name: "support-thread-populated-desktop",
      group: "support",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["support", "desktop", "thread", "populated"]
    });

    await setViewport(page, VIEWPORTS.mobile);
    await capture(page, manifest, {
      name: "support-thread-populated-mobile",
      group: "support",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["support", "mobile", "thread", "populated"]
    });

    await clearClientState(page);
    await loginAsAdmin(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/admin/support");
    await expect(page.getByTestId("admin-support-row").first()).toBeVisible();
    await expect(page.getByTestId("admin-support-inspector")).toContainText("UI review upload issue");
    await capture(page, manifest, {
      name: "admin-support-queue-populated-desktop",
      group: "support",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["support", "admin", "desktop", "queue", "populated"]
    });
  },

  async connections({ page, request, manifest }) {
    await ensureFollowedSeededAdmin(page, request);
    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/connections");
    await capture(page, manifest, {
      name: "connections-populated-desktop",
      group: "connections",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["connections", "desktop", "populated"]
    });
  },

  async admin({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsAdmin(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/admin");
    await page.locator("[data-testid='admin-inbox-row'], [data-testid='admin-inbox-empty']").first().waitFor({
      state: "visible",
      timeout: 10_000
    });
    await capture(page, manifest, {
      name: "admin-inbox-default-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "inbox", "default"]
    });

    await page.goto("/admin/controls");
    await capture(page, manifest, {
      name: "admin-controls-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "controls"]
    });

    await page.goto("/admin/dashboard");
    await capture(page, manifest, {
      name: "admin-dashboard-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "dashboard"]
    });

    await page.goto("/admin/audit");
    await capture(page, manifest, {
      name: "admin-audit-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "audit"]
    });

    await ensureReportedSeededPost(page, request);
    await clearClientState(page);
    await loginAsAdmin(page, request);
    await page.goto("/admin");
    await expect(page.getByTestId("admin-inbox-row").first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId("admin-inbox-inspector")).toBeVisible();
    await capture(page, manifest, {
      name: "admin-inbox-populated-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "inbox", "populated"]
    });
  }
};

module.exports = {
  createManifest,
  ensureOutputDir,
  loadScope,
  scenarioGroups,
  writeArtifacts
};
