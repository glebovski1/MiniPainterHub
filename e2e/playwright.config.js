const { defineConfig } = require("@playwright/test");

const PORT = Number.parseInt(process.env.E2E_PORT || "5176", 10);
const BASE_URL = `http://127.0.0.1:${PORT}`;
const PERF_MODE = process.env.E2E_PERF_MODE === "true";
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const AUTH_PERMIT_LIMIT = process.env.E2E_AUTH_PERMIT_LIMIT || "100";
const LOCALDB_INSTANCE = process.env.E2E_LOCALDB_INSTANCE || (process.env.CI ? "MSSQLLocalDB" : "MiniPainterHubE2E");
const LOCALDB_INSTANCE_PS = LOCALDB_INSTANCE.replace(/'/g, "''");
const DEFAULT_E2E_DB = process.env.CI
  ? "MiniPainterHub_E2E"
  : `MiniPainterHub_E2E_${Date.now()}_${process.pid}`;
const E2E_CONNECTION_STRING =
  process.env.E2E_CONNECTION_STRING ||
  `Server=(localdb)\\${LOCALDB_INSTANCE};Database=${DEFAULT_E2E_DB};Trusted_Connection=True;MultipleActiveResultSets=true`;
const WEB_SERVER_COMMAND = process.env.CI
  ? `dotnet run --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${BASE_URL}`
  : `powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "& { $localDbInstances = sqllocaldb info; if ($localDbInstances -notcontains '${LOCALDB_INSTANCE_PS}') { sqllocaldb create '${LOCALDB_INSTANCE_PS}' | Out-Null }; sqllocaldb start '${LOCALDB_INSTANCE_PS}' | Out-Null; dotnet run --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${BASE_URL} }"`;

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
    reuseExistingServer: process.env.E2E_REUSE_EXISTING_SERVER !== "false" && !process.env.CI,
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
      Site__SupportEmail: "support@minipainterhub.example",
    },
  },
});
