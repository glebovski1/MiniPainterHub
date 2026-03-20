const { defineConfig } = require("@playwright/test");

const PORT = 5176;
const BASE_URL = `http://127.0.0.1:${PORT}`;
const RESET_TOKEN = process.env.E2E_RESET_TOKEN || "local-e2e-reset-token";
const LOCALDB_INSTANCE = process.env.E2E_LOCALDB_INSTANCE || (process.env.CI ? "MSSQLLocalDB" : "MiniPainterHubE2E");
const DEFAULT_E2E_DB = process.env.CI
  ? "MiniPainterHub_E2E"
  : `MiniPainterHub_E2E_${Date.now()}_${process.pid}`;
const E2E_CONNECTION_STRING =
  process.env.E2E_CONNECTION_STRING ||
  `Server=(localdb)\\${LOCALDB_INSTANCE};Database=${DEFAULT_E2E_DB};Trusted_Connection=True;MultipleActiveResultSets=true`;
const WEB_SERVER_COMMAND = process.env.CI
  ? `dotnet run --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${BASE_URL}`
  : `powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "& { sqllocaldb info ${LOCALDB_INSTANCE} *> $null; if ($LASTEXITCODE -ne 0) { sqllocaldb create ${LOCALDB_INSTANCE} | Out-Null }; sqllocaldb start ${LOCALDB_INSTANCE} | Out-Null; dotnet run --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${BASE_URL} }"`;

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
    command: WEB_SERVER_COMMAND,
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
