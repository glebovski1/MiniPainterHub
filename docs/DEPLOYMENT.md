# Deployment

MiniPainterHub is deployed as the `MiniPainterHub.Server` ASP.NET Core app. The hosted Blazor WebAssembly client is bundled into that server publish output, so deploy the server project only.

## Verified local publish

This repository currently publishes successfully with:

```powershell
dotnet publish MiniPainterHub.Server/MiniPainterHub.Server.csproj -c Release -o output/publish
```

## Azure App Service

The repository already includes a GitHub Actions workflow at `.github/workflows/deploy.yml` that:

1. Restores the solution
2. Publishes `MiniPainterHub.Server`
3. Deploys the publish folder to Azure App Service with `azure/webapps-deploy`

### 1. Create or confirm the Azure resources

You need:

- An Azure App Service running .NET 8
- A SQL Server database reachable from the App Service
- An Azure Blob Storage container for image storage

### 2. Configure App Service application settings

Set these App Service settings before first production startup:

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
```

Notes:

- The app runs EF Core migrations automatically in production startup.
- The app seeds the admin account automatically in production startup.
- Use the hierarchical `ImageStorage__...` keys. Older flat keys are not used by the current app.

### Local production-style repro

To mirror Azure configuration locally without committing secrets:

1. Copy `MiniPainterHub.Server/appsettings.Local.Production.example.json` to `MiniPainterHub.Server/appsettings.Local.Production.json`
2. Fill the copied file with the same values used in App Service
3. Run the server with the `ProductionLocal` launch profile

The server now loads optional local override files in this order after the default appsettings files:

- `appsettings.Local.json`
- `appsettings.Local.{Environment}.json`

For a production-like local run, `appsettings.Local.Production.json` is the useful file. It is gitignored so you can place real local or Azure-matching secrets there safely.

### 3. Configure GitHub deploy secrets

Set these repository secrets in GitHub:

```text
AZURE_WEBAPP_NAME=<app-service-name>
AZURE_WEBAPP_PUBLISH_PROFILE=<full publish profile xml from Azure>
```

The deploy workflow uses `workflow_dispatch`, so after the secrets are present you can run it manually from the GitHub Actions UI.

### 4. Run the deployment workflow

In GitHub:

1. Open `Actions`
2. Open `Deploy`
3. Click `Run workflow`
4. Choose `staging` or `production`
5. Choose the branch, tag, or commit to deploy

## Direct local publish

If you want to publish directly from a developer machine instead of GitHub Actions, use a valid Azure publish profile from the target App Service and publish the server project with Visual Studio or `dotnet publish`.

## Post-deploy checks

After deployment:

1. Open the site root and confirm the Blazor app loads
2. Confirm registration/login works
3. Confirm image upload works
4. Confirm the database migrated successfully from App Service logs
5. Confirm SignalR chat endpoints connect if chat is enabled in the target environment
