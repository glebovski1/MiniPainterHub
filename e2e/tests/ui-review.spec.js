const { test, expect } = require("@playwright/test");
const {
  createManifest,
  ensureOutputDir,
  loadScope,
  scenarioGroups,
  writeArtifacts
} = require("./helpers/ui-review");

test.describe.configure({ mode: "serial" });
test.setTimeout(20 * 60 * 1000);

test("capture UI review suite", async ({ page, request }) => {
  const scope = loadScope();
  test.skip(scope.scope === "none", "No UI review scope was requested.");

  ensureOutputDir();
  const manifest = createManifest(scope);

  try {
    for (const group of scope.groups) {
      const runner = scenarioGroups[group];
      if (!runner) {
        continue;
      }

      try {
        await runner({ page, request, manifest, scope });
      } catch (error) {
        manifest.failures.push({
          group,
          message: error instanceof Error ? error.message : String(error)
        });
        throw error;
      }
    }
  } finally {
    writeArtifacts(scope, manifest);
  }

  expect(manifest.captures.length).toBeGreaterThan(0);
});
