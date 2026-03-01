const { defineConfig } = require("@playwright/test");

const PORT = 5176;
const BASE_URL = `http://127.0.0.1:${PORT}`;
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const E2E_CONNECTION_STRING =
  process.env.E2E_CONNECTION_STRING ||
  "Server=(localdb)\\MSSQLLocalDB;Database=MiniPainterHub_E2E;Trusted_Connection=True;MultipleActiveResultSets=true";

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
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: {
    command: `dotnet run --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${BASE_URL}`,
    url: `${BASE_URL}/healthz`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: {
      ASPNETCORE_ENVIRONMENT: "Development",
      DOTNET_ENVIRONMENT: "Development",
      ConnectionStrings__DefaultConnection: E2E_CONNECTION_STRING,
      TestSupport__ResetEnabled: "true",
      TestSupport__ResetToken: RESET_TOKEN,
    },
  },
});
