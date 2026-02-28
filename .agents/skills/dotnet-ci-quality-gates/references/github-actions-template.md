# GitHub Actions Template

## Baseline job outline
- Trigger: `pull_request` and `push` to main branch.
- Steps:
  1. Checkout.
  2. Setup .NET SDK.
  3. Restore.
  4. Build.
  5. Test.
  6. Upload test results artifacts.

## Suggested commands for this repo
- `dotnet restore MiniPainterHub.sln`
- `dotnet build MiniPainterHub.sln --configuration Release --no-restore`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj --configuration Release --no-build --logger trx`

## Optional additions
- Coverage: `--collect:"XPlat Code Coverage"`.
- Formatting: `dotnet format --verify-no-changes`.
- UI snapshots: run snapshot script and upload artifacts.
