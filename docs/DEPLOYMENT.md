# Deployment

MiniPainterHub deploys as the `MiniPainterHub.Server` ASP.NET Core app. The hosted Blazor WebAssembly client is bundled into the server publish output, so deploy the server project only.

## Verified local publish

```powershell
dotnet publish MiniPainterHub.Server/MiniPainterHub.Server.csproj -c Release -o output/publish
```

## Production CI/CD path

Production deployment is owned by GitHub Actions in `.github/workflows/deploy.yml`.

The deploy workflow:

1. Starts after the `CI` workflow succeeds on `master`, or from manual `workflow_dispatch`.
2. Pauses on the GitHub environment named `production`.
3. Authenticates to Azure with OIDC through `azure/login@v2`.
4. Verifies the production resource group exists.
5. Creates or updates Azure infrastructure inside that resource group from `infra/main.bicep`.
6. Publishes `MiniPainterHub.Server`.
7. Deploys the publish folder to Azure App Service with `azure/webapps-deploy@v3`.
8. Verifies `https://<app-host>/healthz` returns `200 OK`.

The current target is production-only and student-minimal. There is no staging App Service, deployment slot, or publish-profile secret in the primary path.

## One-time Azure and GitHub bootstrap

The local Azure CLI account may show a subscription while still having revoked management tokens. If Azure commands fail with `invalid_grant`, refresh the login first:

```powershell
az logout
az login --tenant "4b4b6ba8-5186-4d09-b9b2-8c95f729c4b2" --scope "https://management.core.windows.net//.default"
az account set --subscription "23df46c9-3639-4a03-bb4b-61234224142b"
```

Then run the bootstrap script from the repository root:

```powershell
.\tools\azure\bootstrap-github-oidc.ps1 -ConfigureGitHub
```

The script creates or verifies:

- Resource group `rg-minipainterhub-prod`
- User-assigned identity `id-gha-minipainterhub-prod`
- Contributor role assignment scoped to the production resource group
- Federated credential for `repo:glebovski1/MiniPainterHub:environment:production`
- GitHub environment `production` with required reviewer `glebovski1`
- GitHub environment variables and required secrets

If GitHub CLI is unavailable or you do not want the script to configure GitHub, run it without `-ConfigureGitHub`. It will print the exact `gh variable set` and `gh secret set` commands to run manually.

## Required GitHub environment configuration

Create or update the GitHub environment named `production`. Add required reviewer `glebovski1` so production deployment requires approval before Azure changes are applied.

Environment variables:

```text
AZURE_CLIENT_ID=<client-id of id-gha-minipainterhub-prod>
AZURE_TENANT_ID=4b4b6ba8-5186-4d09-b9b2-8c95f729c4b2
AZURE_SUBSCRIPTION_ID=23df46c9-3639-4a03-bb4b-61234224142b
AZURE_RESOURCE_GROUP=rg-minipainterhub-prod
AZURE_LOCATION=westus
SQL_ADMIN_LOGIN=mphsqladmin
```

Environment secrets:

```text
AZURE_SQL_ADMIN_PASSWORD=<strong Azure SQL admin password>
JWT_KEY=<long random JWT signing secret>
```

Optional environment secrets:

```text
SEED_ADMIN_EMAIL=<admin-email>
SEED_ADMIN_PASSWORD=<admin-password>
```

If both optional seed-admin secrets are present, the deployment sets `SeedAdmin__Enabled=true`; otherwise it sets `SeedAdmin__Enabled=false`.

## Azure resources managed by Bicep

`infra/main.bicep` manages one production stack:

- Linux App Service Plan on Basic `B1`
- Linux App Service configured for `.NET 8`
- Azure SQL logical server and Basic database
- SQL firewall rule allowing Azure services to reach the database
- Standard LRS storage account and private blob container for uploaded images
- Required production app settings:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ConnectionStrings__DefaultConnection`
  - `Jwt__Key`
  - `Jwt__Issuer=MiniPainterHubApi`
  - `Jwt__Audience=MiniPainterHubClient`
  - `ImageStorage__AzureConnectionString`
  - `ImageStorage__AzureContainer`

The app runs EF Core migrations automatically during production startup. Missing or misnamed required settings cause startup to fail fast with a configuration error.

## First deployment

1. Confirm the bootstrap completed successfully.
2. Open GitHub Actions and run `CI` or push through a PR into `master`.
3. Wait for `CI` to pass.
4. Open the `Deploy` workflow run.
5. Approve the `production` environment.
6. Confirm the deploy job completes and the `/healthz` check passes.

Manual deploy is still available from `Actions` -> `Deploy` -> `Run workflow`. The `ref` input can be `master`, a tag, or a commit SHA.

## Local validation before changing the pipeline

```powershell
az bicep build --file infra/main.bicep --outfile "$env:TEMP\minipainterhub-main.json"
dotnet restore MiniPainterHub.sln
dotnet build MiniPainterHub.sln --configuration Release
dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj --configuration Release
dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj --configuration Release
npm --prefix e2e ci
npm --prefix e2e run test:smoke
```

After a fresh Azure login, preview production infrastructure changes before applying them:

```powershell
az deployment group what-if `
  --resource-group rg-minipainterhub-prod `
  --template-file infra/main.bicep `
  --parameters `
    location=westus `
    sqlAdministratorLogin=mphsqladmin `
    sqlAdministratorPassword="<sql-admin-password>" `
    jwtKey="<jwt-key>"
```

## Post-deploy checks

After deployment:

1. Open the site root and confirm the Blazor app loads.
2. Confirm `/healthz` returns `200 OK`.
3. Confirm registration or login works.
4. Confirm image upload works.
5. Confirm the database migrated successfully from App Service logs.
6. Confirm SignalR chat endpoints connect if chat is enabled.

## Rollback

The fastest rollback is to rerun the `Deploy` workflow manually with a known-good commit SHA or tag. The workflow reapplies the same infrastructure template, republishes the selected server build, and reruns the health check.

If the app fails after a successful upload, treat it as a deployed-app startup problem before changing infrastructure:

1. Turn on App Service logs.
2. Inspect the App Service log stream and deployment logs.
3. Use Kudu/Advanced Tools to inspect deployed files under `site/wwwroot`.
4. Run the deployed app directly from Kudu:

Windows App Service:

```powershell
dotnet D:\home\site\wwwroot\MiniPainterHub.Server.dll
```

Linux App Service:

```bash
dotnet /home/site/wwwroot/MiniPainterHub.Server.dll
```

The most common runtime failure causes for this repo are missing `Jwt__...` settings, missing `ImageStorage__...` settings, failed SQL connectivity, or production dependencies that are unreachable from the App Service.
