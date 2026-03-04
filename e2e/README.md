# E2E Smoke Setup

This suite is isolated from your normal local app data.

- Default E2E DB:
  - CI uses `MiniPainterHub_E2E`
  - local runs use a unique timestamp/process-suffixed database name by default
- Reset endpoint is enabled only for E2E server runs:
  - `TestSupport__ResetEnabled=true`
  - `TestSupport__ResetToken=<token>`

Your regular app runs (without these env vars) do not expose the reset endpoint and do not get wiped by E2E.

## Smoke coverage

Current smoke scenarios include:

- auth (valid and invalid login)
- post create flow
- comment + like engagement flow
- profile create/update flow
- unauthenticated create-post guard
- create-post validation errors
- post details unauthenticated comment prompt
- admin moderation:
  - hide/restore post using list visibility filters (`Active only`, `Include hidden`, `Hidden only`)
  - hide/restore comment using comment visibility filters in post details

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
