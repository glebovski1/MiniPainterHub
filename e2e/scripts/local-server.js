function localServer(env = process.env, platform = process.platform) {
  const port = Number(env.E2E_PORT || 5176);
  if (!Number.isInteger(port) || port < 1024 || port > 65535) {
    throw new Error("E2E_PORT must be an integer from 1024 to 65535.");
  }
  const baseURL = `http://127.0.0.1:${port}`;
  const instance = env.E2E_LOCALDB_INSTANCE || (env.CI ? "MSSQLLocalDB" : "MiniPainterHubE2E");
  if (!/^[A-Za-z0-9_-]+$/.test(instance)) {
    throw new Error("E2E_LOCALDB_INSTANCE contains unsupported characters.");
  }
  if (platform !== "win32" && platform !== "linux" && !env.E2E_CONNECTION_STRING) {
    throw new Error("Set E2E_CONNECTION_STRING to a disposable loopback SQL database on this OS.");
  }
  const connectionString = env.E2E_CONNECTION_STRING
    || `Server=(localdb)\\${instance};Database=MiniPainterHub_E2E_${Date.now()}_${process.pid};Trusted_Connection=True;MultipleActiveResultSets=true`;
  // Parse quoted values without interpreting a semicolon inside a password as another key.
  const pair = /\s*([^=;]+)\s*=\s*(?:"((?:[^"]|"")*)"|'((?:[^']|'')*)'|([^;]*))\s*(?:;|$)/gy;
  const sources = [];
  let cursor = 0;
  while (cursor < connectionString.trimEnd().length) {
    pair.lastIndex = cursor;
    const match = pair.exec(connectionString);
    if (!match) throw new Error("E2E_CONNECTION_STRING must use SQL connection-string syntax.");
    cursor = pair.lastIndex;
    const key = match[1].trim().toLowerCase();
    if (["server", "data source", "address", "addr", "network address"].includes(key)) {
      sources.push((match[2] ?? match[3] ?? match[4]).trim());
    }
  }
  if (sources.length !== 1) throw new Error("Specify exactly one SQL data source for E2E.");
  const localDb = /^\(localdb\)\\([A-Za-z0-9_-]+)$/i.exec(sources[0]);
  const dotnet = `dotnet run --no-launch-profile --project ../MiniPainterHub.Server/MiniPainterHub.Server.csproj --urls ${baseURL}`;
  const command = platform === "win32" && localDb
    ? `powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "& { $instances = sqllocaldb info; if ($instances -notcontains '${localDb[1]}') { sqllocaldb create '${localDb[1]}' | Out-Null }; sqllocaldb start '${localDb[1]}' | Out-Null; ${dotnet} }"`
    : dotnet;
  return { baseURL, connectionString, command };
}

module.exports = { localServer };
