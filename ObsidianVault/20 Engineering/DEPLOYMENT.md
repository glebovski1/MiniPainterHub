# Deployment

MiniPainterHub deploys as the existing `MiniPainterHub.Server` ASP.NET Core App Service. The hosted Blazor WebAssembly client is bundled into the server publish output, so deploy the server project only.

Production URL:

```text
https://minipainterhub-dqandpbghpgbfgf3.canadacentral-01.azurewebsites.net
```

## Verified local publish

```powershell
dotnet publish MiniPainterHub.Server/MiniPainterHub.Server.csproj -c Release -o output/publish
```

## Production CI/CD path

Production deployment is owned by GitHub Actions in `.github/workflows/deploy.yml`.

The deploy workflow:

1. Starts after the `CI` workflow succeeds on `master`, or from manual `workflow_dispatch`.
2. Pauses on the GitHub environment named `production`.
3. Publishes `MiniPainterHub.Server`.
4. Applies EF Core migrations to the production SQL database with the deployment SQL connection secret.
5. Deploys the publish folder to the existing Azure App Service with `azure/webapps-deploy@v3`.
6. Verifies `https://minipainterhub-dqandpbghpgbfgf3.canadacentral-01.azurewebsites.net/healthz/ready` returns `200 OK`.

This repository does not create the production App Service. The production app already exists in Azure and is deployed through its App Service publish profile.

## Required GitHub environment configuration

Create or update the GitHub environment named `production`. Add required reviewer `glebovski1` so production deployment requires approval.

Environment variables:

```text
AZURE_WEBAPP_NAME=MiniPainterHub
AZURE_WEBAPP_HOSTNAME=minipainterhub-dqandpbghpgbfgf3.canadacentral-01.azurewebsites.net
```

`AZURE_WEBAPP_NAME` is the Azure App Service resource name, not the hashed default hostname prefix. The current production App Service is named `MiniPainterHub`; Azure serves it at the hostname above.

Environment secrets:

```text
AZURE_WEBAPP_PUBLISH_PROFILE=<full App Service publish profile XML>
PRODUCTION_SQL_CONNECTION_STRING=<optional SQL connection string override for EF migrations>
```

Use a freshly downloaded publish profile from the target App Service when rotating credentials. Do not commit publish profiles or publish settings files.
When `PRODUCTION_SQL_CONNECTION_STRING` is configured, it should target the same database used by `ConnectionStrings__DefaultConnection` in App Service configuration and should be rotated with the database credentials.

When the SQL secret is omitted, the deploy job uses the publish profile's SCM credentials to read `DefaultConnection` from the existing App Service environment. The value is masked immediately and exists only in the deployment job environment. This fallback keeps the existing App Service configuration as the source of truth without committing or printing the connection string.

## First deployment

1. Confirm the GitHub `production` environment has the variables and `AZURE_WEBAPP_PUBLISH_PROFILE` secret above. Add `PRODUCTION_SQL_CONNECTION_STRING` only when an explicit migration credential override is required.
2. Open GitHub Actions and run `CI` or push through a PR into `master`.
3. Wait for `CI` to pass.
4. Open the `Deploy` workflow run.
5. Approve the `production` environment.
6. Confirm the deploy job completes and the `/healthz/ready` check passes.

Manual deploy is available from `Actions` -> `Deploy` -> `Run workflow`. The `ref` input can be `master`, a tag, or a commit SHA.

## Local validation before changing the pipeline

```powershell
dotnet restore MiniPainterHub.sln
dotnet build MiniPainterHub.sln --configuration Release
dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj --configuration Release
dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj --configuration Release
npm --prefix e2e ci
npm --prefix e2e run test:smoke
```

## Local emergency publish

If GitHub Actions is unavailable and you have a current App Service publish settings file, publish manually with Zip Deploy:

```powershell
$publishDir = Join-Path $env:TEMP 'mph-existing-publish'
$zipPath = Join-Path $env:TEMP 'mph-existing-publish.zip'
Remove-Item -LiteralPath $publishDir,$zipPath -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish MiniPainterHub.Server/MiniPainterHub.Server.csproj --configuration Release --no-restore --output $publishDir
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

[xml]$settings = Get-Content -Raw -LiteralPath '<path-to-publish-settings-file>'
$profile = @($settings.publishData.publishProfile) | Where-Object { $_.publishMethod -eq 'ZipDeploy' } | Select-Object -First 1
$pair = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($profile.userName):$($profile.userPWD)"))

Invoke-WebRequest `
  -Uri 'https://minipainterhub-dqandpbghpgbfgf3.scm.canadacentral-01.azurewebsites.net/api/zipdeploy?isAsync=true' `
  -Method Post `
  -Headers @{ Authorization = "Basic $pair" } `
  -InFile $zipPath `
  -ContentType 'application/zip' `
  -UseBasicParsing
```

## Post-deploy checks

After deployment:

1. Open the site root and confirm the Blazor app loads.
2. Confirm `/healthz` returns `200 OK`.
3. Confirm `/healthz/ready` returns `200 OK`.
4. Confirm registration or login works.
5. Confirm image upload works.
6. Confirm the database migrated successfully from GitHub Actions logs.
7. Confirm SignalR chat endpoints connect if chat is enabled.

Production startup does not run EF migrations by default. If the deployment migration step is unavailable during a single-instance emergency rollout, temporarily set `Database__AutoMigrateOnStartup=true`, deploy once, then remove it after the app starts cleanly.

## Rollback

The fastest rollback is to rerun the `Deploy` workflow manually with a known-good commit SHA or tag. The workflow republishes the selected server build and reruns the health check.

If the app fails after a successful upload, treat it as a deployed-app startup problem:

1. Turn on App Service logs.
2. Inspect the App Service log stream and deployment logs.
3. Use Kudu/Advanced Tools to inspect deployed files under `site/wwwroot`.
4. Run the deployed app directly from Kudu.

The most common runtime failure causes for this repo are missing `Jwt__...` settings, missing `ImageStorage__...` settings, failed database connectivity, or production dependencies that are unreachable from the App Service.
