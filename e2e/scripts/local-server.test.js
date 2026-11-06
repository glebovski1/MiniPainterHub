const { test } = require("node:test");
const assert = require("node:assert/strict");
const { localServer } = require("./local-server");

test("Windows LocalDB startup follows the actual connection's instance", () => {
  const result = localServer({ E2E_CONNECTION_STRING: "Server=(localdb)\\Custom;Database=MiniPainterHub_E2E" }, "win32");
  assert.match(result.command, /sqllocaldb start 'Custom'/);
});

for (const platform of ["win32", "linux"]) {
  test(`${platform} explicit SQL uses dotnet without PowerShell`, () => {
    const result = localServer({ E2E_CONNECTION_STRING: 'Data Source=127.0.0.1;Database=MiniPainterHub_E2E;Password="x;Server=unused"' }, platform);
    assert.match(result.command, /^dotnet run /);
  });
}

test("Linux default uses the existing InMemory development fallback", () => {
  assert.match(localServer({}, "linux").command, /^dotnet run /);
});

test("ambiguous SQL targets and shell fragments fail closed", () => {
  assert.throws(() => localServer({ E2E_CONNECTION_STRING: "Server=localhost;Data Source=remote" }));
  assert.throws(() => localServer({ E2E_LOCALDB_INSTANCE: "bad'; exit" }));
  assert.throws(() => localServer({ E2E_PORT: "5176; echo bad" }));
});
