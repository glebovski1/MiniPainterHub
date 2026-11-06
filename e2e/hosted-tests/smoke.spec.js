const { test, expect } = require("@playwright/test");

// This suite performs only reads. Interactive OAuth, email, uploads and writes
// belong to the separately approved rehearsal with controlled accounts.
test("hosted SQL, migrations, media and Production configuration are ready", async ({ request }) => {
  const response = await request.get("/healthz/ready", { maxRedirects: 0 });
  expect(response.status()).toBe(200);
  const result = await response.json();
  expect(result.status).toBe("Healthy");
  for (const name of ["sql", "migrations", "imageStorage", "configuration"]) {
    expect(result.checks[name]?.isHealthy, `${name} readiness`).toBe(true);
  }
  expect(result.checks.migrations.detail).toBe("No pending EF migrations.");
  expect(result.checks.configuration.detail).toBe("Hosted configuration ok.");
});

test("public APIs return JSON at the exact destination", async ({ request }) => {
  for (const path of ["/api/posts?page=1&pageSize=1", "/api/auth/providers"]) {
    const response = await request.get(path, { maxRedirects: 0 });
    expect(response.status()).toBe(200);
    expect(response.headers()["content-type"]).toContain("application/json");
    expect(await response.json()).toBeTruthy();
  }
});

test("hosted Blazor shell loads without redirecting to another host", async ({ page, context, baseURL }) => {
  const failures = [];
  page.on("pageerror", error => failures.push(error.message));
  await context.route("**/*", route => {
    const request = route.request();
    if (!["GET", "HEAD"].includes(request.method())
      || (request.isNavigationRequest() && new URL(request.url()).origin !== baseURL)) {
      return route.abort();
    }
    return route.continue();
  });
  const response = await page.goto("/", { waitUntil: "domcontentloaded" });
  expect(response.status()).toBe(200);
  expect(new URL(page.url()).origin).toBe(baseURL);
  await expect(page.getByTestId("site-footer")).toBeVisible({ timeout: 45_000 });
  expect(failures).toEqual([]);
});

if (process.env.MPH_HOSTED_AUTH === "true") {
  test("controlled account token remains valid on this host", async ({ request }) => {
    const response = await request.get("/api/profiles/me", {
      maxRedirects: 0,
      headers: { Authorization: `Bearer ${process.env.MPH_HOSTED_ACCOUNT_TOKEN}` },
    });
    expect(response.status()).toBe(200);
    const profile = await response.json();
    expect(profile.userId).toBe(process.env.MPH_HOSTED_ACCOUNT_ID);
  });
}
