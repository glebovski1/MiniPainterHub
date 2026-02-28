# E2E Smoke Setup

This suite is isolated from your normal local app data.

- Default E2E DB:
  - `Server=(localdb)\MSSQLLocalDB;Database=MiniPainterHub_E2E;Trusted_Connection=True;MultipleActiveResultSets=true`
- Reset endpoint is enabled only for E2E server runs:
  - `TestSupport__ResetEnabled=true`
  - `TestSupport__ResetToken=<token>`

Your regular app runs (without these env vars) do not expose the reset endpoint and do not get wiped by E2E.

## Run

```powershell
npm --prefix e2e ci
npm --prefix e2e run test:smoke
```

## Optional override

Use a custom isolated DB for E2E:

```powershell
$env:E2E_CONNECTION_STRING="Server=(localdb)\MSSQLLocalDB;Database=MiniPainterHub_E2E_Custom;Trusted_Connection=True;MultipleActiveResultSets=true"
npm --prefix e2e run test:smoke
```
