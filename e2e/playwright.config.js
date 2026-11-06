const { defineConfig } = require("@playwright/test");
const { localServer } = require("./scripts/local-server");

const { baseURL: BASE_URL, connectionString: E2E_CONNECTION_STRING, command: WEB_SERVER_COMMAND } = localServer();
const PERF_MODE = process.env.E2E_PERF_MODE === "true";
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const AUTH_PERMIT_LIMIT = process.env.E2E_AUTH_PERMIT_LIMIT || "100";

module.exports = defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
  ],
  use: {
    baseURL: BASE_URL,
    trace: PERF_MODE ? "off" : "retain-on-failure",
    screenshot: PERF_MODE ? "off" : "only-on-failure",
    video: PERF_MODE ? "off" : "retain-on-failure",
  },
  webServer: {
    command: WEB_SERVER_COMMAND,
    url: `${BASE_URL}/healthz`,
    reuseExistingServer: false,
    timeout: 180_000,
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      DOTNET_ENVIRONMENT: "Development",
      ConnectionStrings__DefaultConnection: E2E_CONNECTION_STRING,
      TestSupport__ResetEnabled: "true",
      TestSupport__ResetToken: RESET_TOKEN,
      TrafficShaping__Auth__PermitLimit: AUTH_PERMIT_LIMIT,
      Authentication__Google__Enabled: "true",
      Authentication__Google__UseFakeProvider: "true",
      Authentication__Google__PublicOrigin: "https://localhost:7295",
      Authentication__Discord__Enabled: "true",
      Authentication__Discord__UseFakeProvider: "true",
      Authentication__Discord__PublicOrigin: "https://localhost:7295",
      Site__SupportEmail: "support@rollandpaint.com",
    },
  },
});
