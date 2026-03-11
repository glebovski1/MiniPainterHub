# Deployment

MiniPainterHub is deployed as the `MiniPainterHub.Server` ASP.NET Core app. The hosted Blazor WebAssembly client is bundled into that server publish output, so deploy the server project only.

## Verified local publish

This repository currently publishes successfully with:

```powershell
dotnet publish MiniPainterHub.Server/MiniPainterHub.Server.csproj -c Release -o output/publish
```

## Supported Azure publish paths

- Preferred local path: Visual Studio publishing the `MiniPainterHub.Server` project with a freshly downloaded `Zip Deploy` publish profile.
- Preferred fallback path: the GitHub Actions workflow in `.github/workflows/deploy.yml`.
- Do not publish the solution or `MiniPainterHub.WebApp` directly. Only the server project is deployable.
- Do not rely on repo-managed `Properties/PublishProfiles` artifacts. Publish profiles are machine-local Azure credentials and should be re-downloaded from the target App Service when needed.

## Azure App Service target validation

Before changing code or credentials, confirm the deployment target itself:

1. The target App Service is the app you actually intend to update.
2. The App Service OS is known (`Windows` or `Linux`).
3. The stack is configured for `.NET 8`.
4. Visual Studio is publishing `MiniPainterHub.Server`.

The current publish output includes a `web.config`, which is normal for Windows App Service and IIS-hosted ASP.NET Core apps. If the target is Linux App Service, still publish the same server project, but use Linux-appropriate diagnostics when troubleshooting startup.

## App Service application settings

Set these settings before first production startup:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<sql-connection-string>
Jwt__Key=<long-random-secret>
Jwt__Issuer=MiniPainterHubApi
Jwt__Audience=MiniPainterHubClient
ImageStorage__AzureConnectionString=<azure-storage-connection-string>
ImageStorage__AzureContainer=<blob-container-name>
```

Optional settings:

```text
Maintenance__Enabled=false
Maintenance__Message=<optional maintenance banner>
SeedAdmin__Enabled=true
SeedAdmin__Email=<admin-email>
SeedAdmin__Password=<admin-password>
```

Notes:

- The app runs EF Core migrations automatically in production startup.
- The app seeds the admin account automatically in production startup.
- Use the hierarchical `ImageStorage__...` keys. Older flat keys such as `ImageStorageAzureConnectionString` and `ImageStorageAzureContainer` are not used by the current app.
- The server now validates these non-development settings during startup and fails fast with a single configuration error if required keys are missing or misnamed.

## Local production-style repro

To mirror Azure configuration locally without committing secrets:

1. Copy `MiniPainterHub.Server/appsettings.Local.Production.example.json` to `MiniPainterHub.Server/appsettings.Local.Production.json`
2. Fill the copied file with the same values used in App Service
3. Run the server with the `ProductionLocal` launch profile

The server now loads optional local override files in this order after the default appsettings files:

- `appsettings.Local.json`
- `appsettings.Local.{Environment}.json`

For a production-like local run, `appsettings.Local.Production.json` is the useful file. It is gitignored so you can place real local or Azure-matching secrets there safely.

## Visual Studio publish reset

If Visual Studio publish starts failing, reset the publish state instead of trying to repair old profile files:

1. In Azure Portal, open the target App Service and download a fresh publish profile.
2. In Visual Studio, remove the existing publish profile for this app.
3. Create a new publish profile from the downloaded Azure profile.
4. Start with `Zip Deploy`.
5. Switch to `Web Deploy` only if `Zip Deploy` is unavailable and you do not have transport or certificate errors.
6. Keep FTP only as a last-resort diagnostic path.

If Visual Studio fails before the upload begins, capture the exact Visual Studio publish log and classify the failure as a publish transport/profile problem. Do not debug runtime startup until the package has actually been deployed.

## Site-startup diagnostics after a successful upload

If the publish succeeds but the site returns `500`, `503`, or a generic startup page, treat it as a deployed-app problem:

1. Turn on App Service logs.
2. Inspect `eventlog.xml`.
3. Use Kudu to inspect the deployed files under `site/wwwroot`.
4. Temporarily enable `stdoutLogEnabled="true"` in the deployed `web.config`, reproduce once, capture the startup exception, then turn stdout logging back off.
5. Run the deployed app directly from Kudu to surface the real host startup error.

Expected Kudu commands:

- Windows App Service:

```powershell
dotnet D:\home\site\wwwroot\MiniPainterHub.Server.dll
```

- Linux App Service:

```bash
dotnet /home/site/wwwroot/MiniPainterHub.Server.dll
```

The most common runtime failure causes for this repo are missing `Jwt__...` settings, missing `ImageStorage__...` settings, or production dependencies that are unreachable from the App Service.

## GitHub Actions fallback

The repository already includes a GitHub Actions workflow at `.github/workflows/deploy.yml` that:

1. Restores the solution
2. Publishes `MiniPainterHub.Server`
3. Deploys the publish folder to Azure App Service with `azure/webapps-deploy`

Configure these repository secrets before using the workflow:

```text
AZURE_WEBAPP_NAME=<app-service-name>
AZURE_WEBAPP_PUBLISH_PROFILE=<full publish profile xml from Azure>
```

The deploy workflow uses `workflow_dispatch`, so after the secrets are present you can run it manually from the GitHub Actions UI. Use this path if Visual Studio still fails after one clean publish-profile reset.

Run the deployment workflow:

1. Open `Actions`
2. Open `Deploy`
3. Click `Run workflow`
4. Choose `staging` or `production`
5. Choose the branch, tag, or commit to deploy

## Post-deploy checks

After deployment:

1. Open the site root and confirm the Blazor app loads.
2. Confirm `/healthz` returns `200 OK`.
3. Confirm registration or login works.
4. Confirm image upload works.
5. Confirm the database migrated successfully from App Service logs.
6. Confirm SignalR chat endpoints connect if chat is enabled in the target environment.
