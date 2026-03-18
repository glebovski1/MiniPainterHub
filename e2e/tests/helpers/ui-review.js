const fs = require("fs");
const path = require("path");
const { expect } = require("@playwright/test");
const { loadMatrix } = require("../../scripts/resolve-ui-review-scope");

const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const REPO_ROOT = path.resolve(__dirname, "../../..");
const OUTPUT_DIR = path.resolve(REPO_ROOT, "output/playwright/ui-review");
const SCOPE_FILE = process.env.UI_REVIEW_SCOPE_FILE || path.join(OUTPUT_DIR, "scope.json");
const MATRIX = loadMatrix(path.resolve(__dirname, "../../ui-review.matrix.json"));

const VIEWPORTS = {
  desktop: { width: 1440, height: 1400, name: "desktop" },
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
    localStorage.removeItem("authToken");
    localStorage.removeItem("userPanelDesktopCollapsed");
    sessionStorage.clear();
  });
  await page.context().clearCookies();
}

async function startFreshState(page, request) {
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
    }
  }

  await expect(logoutLink).toBeVisible();
  await settle(page, 700);
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

  await settle(page);
  await page.screenshot({
    path: filePath,
    fullPage: true,
    animations: "disabled"
  });

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

  try {
    await expect(panel).toHaveClass(/show/);
    await expect(panel).toBeVisible({ timeout: 2_000 });
  } catch {
    await page.evaluate(() => {
      const panelElement = document.getElementById("userPanelOffcanvas");
      if (!panelElement) {
        return;
      }

      const bootstrapApi = window.bootstrap?.Offcanvas;
      if (bootstrapApi) {
        bootstrapApi.getOrCreateInstance(panelElement).show();
      }

      panelElement.classList.add("show");
      panelElement.style.visibility = "visible";
      panelElement.style.transform = "translateX(0)";
    });

    await expect(panel).toHaveClass(/show/);
    await expect(panel).toBeVisible();
  }

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
    bio: "Handles moderation and seeded showcase content."
  });
  const adminUserId = adminProfile.userId;

  await clearClientState(page);
  await loginAsSeedUser(page, request);
  await sendAuthedRequest(page, request, "POST", `/api/follows/${encodeURIComponent(adminUserId)}`);
  return adminUserId;
}

async function ensureConversationWithSeededAdmin(page, request) {
  const adminUserId = await ensureFollowedSeededAdmin(page, request);
  await sendAuthedRequest(page, request, "POST", `/api/conversations/direct/${encodeURIComponent(adminUserId)}`);
  await page.goto("/messages");
  await settle(page, 700);
}

async function ensureReportedSeededPost(page, request) {
  await startFreshState(page, request);
  await loginAsSeedUser(page, request);
  await page.goto("/");
  await settle(page, 700);
  const seededCard = page.locator(".card", { hasText: "Seeded: glazing check" }).first();
  await expect(seededCard).toBeVisible();
  await clickReliably(seededCard.getByRole("link", { name: /Seeded: glazing check/i }).first());
  await settle(page, 700);
  const postId = getPathSegment(page.url(), "/posts/");
  await sendAuthedRequest(page, request, "POST", `/api/reports/posts/${encodeURIComponent(postId)}`, {
    reasonCode: "Spam",
    details: "UI review seeded report."
  });
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

    await setViewport(page, VIEWPORTS.mobile);
    await openMobilePanel(page);
    await capture(page, manifest, {
      name: "shell-home-mobile-panel-open",
      group: "shell",
      viewport: VIEWPORTS.mobile,
      authState: "seed-user",
      stateTags: ["shell", "community", "mobile", "panel-open"]
    });
  },

  async auth({ page, request, manifest }) {
    await startFreshState(page, request);

    await setViewport(page, VIEWPORTS.desktop);
    await page.goto("/login");
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

    await page.goto("/register");
    await capture(page, manifest, {
      name: "auth-register-desktop",
      group: "auth",
      viewport: VIEWPORTS.desktop,
      authState: "unauthenticated",
      stateTags: ["auth", "desktop", "entry"]
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
    await capture(page, manifest, {
      name: "community-top-posts-desktop",
      group: "community",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["community", "desktop", "showcase"]
    });
  },

  async search({ page, request, manifest }) {
    await startFreshState(page, request);
    await loginAsSeedUser(page, request);
    await setViewport(page, VIEWPORTS.desktop);

    await page.goto("/search?q=seeded&tab=posts");
    await capture(page, manifest, {
      name: "search-post-results-desktop",
      group: "search",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["search", "desktop", "posts-results"]
    });

    await page.goto("/search?q=user&tab=users");
    await capture(page, manifest, {
      name: "search-user-results-desktop",
      group: "search",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["search", "desktop", "users-results"]
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
    await capture(page, manifest, {
      name: "posts-new-composer",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "composer"]
    });

    await clickReliably(page.getByTestId("create-post-submit"));
    await capture(page, manifest, {
      name: "posts-new-validation",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "validation"]
    });

    await page.goto("/");
    const seededCard = page.locator(".card", { hasText: "Seeded: glazing check" }).first();
    await clickReliably(seededCard.getByRole("link", { name: /Seeded: glazing check/i }).first());
    await capture(page, manifest, {
      name: "posts-detail-seeded",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "details", "populated"]
    });

    await page.goto("/posts/mine");
    await capture(page, manifest, {
      name: "posts-mine-desktop",
      group: "posts",
      viewport: VIEWPORTS.desktop,
      authState: "seed-user",
      stateTags: ["posts", "desktop", "mine", "populated"]
    });
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
      bio: "Handles moderation and seeded showcase content."
    });
    await clearClientState(page);
    await loginAsSeedUser(page, request);
    await page.goto(`/users/${encodeURIComponent(adminProfile.userId)}`);
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

    await page.goto("/admin/reports");
    await capture(page, manifest, {
      name: "admin-reports-empty-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "reports", "empty"]
    });

    await page.goto("/admin/moderation");
    await capture(page, manifest, {
      name: "admin-moderation-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "moderation"]
    });

    await page.goto("/admin/audit");
    await capture(page, manifest, {
      name: "admin-audit-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "audit"]
    });

    await page.goto("/admin/suspensions");
    await capture(page, manifest, {
      name: "admin-suspensions-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "suspensions"]
    });

    await ensureReportedSeededPost(page, request);
    await clearClientState(page);
    await loginAsAdmin(page, request);
    await page.goto("/admin/reports");
    await capture(page, manifest, {
      name: "admin-reports-populated-desktop",
      group: "admin",
      viewport: VIEWPORTS.desktop,
      authState: "admin",
      stateTags: ["admin", "desktop", "reports", "populated"]
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
