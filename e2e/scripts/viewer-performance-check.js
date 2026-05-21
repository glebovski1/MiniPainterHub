const { spawnSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const e2eRoot = path.resolve(__dirname, "..");
const localBin = process.platform === "win32"
  ? path.join(e2eRoot, "node_modules", ".bin", "playwright.cmd")
  : path.join(e2eRoot, "node_modules", ".bin", "playwright");
const command = fs.existsSync(localBin) ? localBin : "npx";
const args = fs.existsSync(localBin)
  ? ["test", "tests/viewer-performance.spec.js", "--reporter=list"]
  : ["playwright", "test", "tests/viewer-performance.spec.js", "--reporter=list"];

const result = spawnSync(command, args, {
  cwd: e2eRoot,
  stdio: "inherit",
  env: {
    ...process.env,
    E2E_PORT: process.env.E2E_PORT || "5177",
    E2E_REUSE_EXISTING_SERVER: process.env.E2E_REUSE_EXISTING_SERVER || "false",
    E2E_PERF_MODE: process.env.E2E_PERF_MODE || "true"
  },
  shell: process.platform === "win32"
});

if (result.error) {
  console.error(result.error);
  process.exit(1);
}

process.exit(result.status ?? 1);
