const { defineConfig } = require("@playwright/test");

const configured = process.env.MPH_HOSTED_ORIGIN;
if (!configured) throw new Error("Set MPH_HOSTED_ORIGIN to the exact HTTPS origin under test.");
const origin = new URL(configured);
if (origin.protocol !== "https:" || origin.username || origin.password
  || origin.pathname !== "/" || origin.search || origin.hash
  || ["localhost", "127.0.0.1", "[::1]"].includes(origin.hostname)) {
  throw new Error("MPH_HOSTED_ORIGIN must be a hosted HTTPS origin without credentials, path, query or fragment.");
}
if (process.env.MPH_HOSTED_AUTH === "true"
  && (!process.env.MPH_HOSTED_ACCOUNT_TOKEN || !process.env.MPH_HOSTED_ACCOUNT_ID)) {
  throw new Error("Authenticated smoke requires a controlled account token and expected account ID.");
}

module.exports = defineConfig({
  testDir: "./hosted-tests",
  workers: 1,
  retries: 0,
  timeout: 60_000,
  outputDir: "../artifacts/hosted-smoke/test-results",
  reporter: [["list"], ["json", { outputFile: "../artifacts/hosted-smoke/results.json" }]],
  use: {
    baseURL: origin.origin,
    ignoreHTTPSErrors: false,
    serviceWorkers: "block",
    trace: "off",
    screenshot: "off",
    video: "off",
  },
});
