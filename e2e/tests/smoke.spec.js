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

async function createPost(page, suffix) {
  const title = `Smoke title ${suffix}`;
  const content = `Smoke content ${suffix}`;

  await page.goto("/posts/new");
  await page.getByTestId("create-post-title").fill(title);
  await page.getByTestId("create-post-content").fill(content);
  await page.getByTestId("create-post-submit").click();

  await expect(page).toHaveURL(/\/posts\/\d+$/);
  await expect(page.getByTestId("post-title")).toHaveText(title);

  return { title, content };
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
