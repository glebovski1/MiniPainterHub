const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const {
  DEFAULT_MATRIX_PATH,
  REPO_ROOT,
  getGitDiffFiles,
  loadMatrix,
  resolveScope
} = require("./resolve-ui-review-scope");

const OUTPUT_DIR = path.resolve(REPO_ROOT, "output/playwright/ui-review");

function parseArgs(argv) {
  return {
    full: argv.includes("--full")
  };
}

function buildFullScope(matrix) {
  return {
    scope: "full",
    changedFiles: [],
    uiChangedFiles: [],
    groups: Object.keys(matrix.groups),
    matchedByGroup: Object.fromEntries(Object.keys(matrix.groups).map((group) => [group, "forced full review"])),
    reasons: ["Forced full UI review run."],
    reviewCommand: "npm --prefix e2e run test:ui-review:full"
  };
}

function buildBaselineScope(matrix, previousScope) {
  return {
    ...previousScope,
    scope: "targeted",
    groups: matrix.defaultGroups,
    matchedByGroup: Object.fromEntries(matrix.defaultGroups.map((group) => [group, "baseline local review"])),
    reasons: [
      ...previousScope.reasons,
      "No UI diff was detected locally, so the baseline shell/community review was used."
    ],
    reviewCommand: "npm --prefix e2e run test:ui-review"
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const matrix = loadMatrix(DEFAULT_MATRIX_PATH);

  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  const scope = args.full
    ? buildFullScope(matrix)
    : (() => {
        const resolved = resolveScope({
          matrix,
          changedFiles: getGitDiffFiles()
        });

        return resolved.scope === "none"
          ? buildBaselineScope(matrix, resolved)
          : resolved;
      })();

  const scopePath = path.join(OUTPUT_DIR, "scope.json");
  fs.writeFileSync(scopePath, `${JSON.stringify(scope, null, 2)}\n`, "utf8");

  const runner = process.platform === "win32"
    ? spawnSync(
        process.env.ComSpec || "cmd.exe",
        ["/d", "/s", "/c", "npx playwright test tests/ui-review.spec.js"],
        {
          cwd: path.resolve(__dirname, ".."),
          env: {
            ...process.env,
            UI_REVIEW_SCOPE_FILE: scopePath
          },
          stdio: "inherit"
        }
      )
    : spawnSync(
        "npx",
        ["playwright", "test", "tests/ui-review.spec.js"],
        {
          cwd: path.resolve(__dirname, ".."),
          env: {
            ...process.env,
            UI_REVIEW_SCOPE_FILE: scopePath
          },
          stdio: "inherit"
        }
      );

  if (runner.error) {
    console.error(runner.error);
    process.exit(1);
  }

  process.exit(runner.status ?? 1);
}

main();
