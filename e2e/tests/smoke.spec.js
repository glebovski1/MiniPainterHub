const { test, expect } = require("@playwright/test");

test.describe.configure({ mode: "serial" });
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";

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
  await expect(page.getByTestId("search-post-result").first()).toContainText(title);

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
