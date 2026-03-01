# Contributing to MiniPainterHub

Thanks for contributing. This guide keeps changes consistent and reviewable.

## 1) Docs to Read First

- `AGENT.md`
- `docs/ARCHITECTURE.md`
- `docs/CODE_STYLE.md`
- `docs/BEST_PRACTICES.md`
- `docs/ANTI_PATTERNS.md`

## 2) Suggested Branch Naming

- `feature/<short-topic>`
- `fix/<short-topic>`
- `refactor/<short-topic>`
- `docs/<short-topic>`

## 3) Change Workflow

1. Understand impacted flow and files.
2. Make the smallest coherent change set.
3. Add/update tests when behavior changes.
4. Run required validation.
5. Update docs if guidance changed.

## 4) Validation Expectations

Codex Cloud preflight (only when `dotnet` is unavailable):
- `bash tools/cloud/bootstrap-dotnet-and-test.sh`

Backend-impacting changes:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

UI-impacting changes:
- Build solution
- Run impacted page manually
- Optional snapshot check:
  - `./.venv/Scripts/python tools/ui_snapshot_panel.py`
- Optional behavioral smoke check:
  - `npm --prefix e2e ci`
  - `npm --prefix e2e run test:smoke`

## 5) Coding Expectations

- Controllers remain thin.
- Business rules live in services.
- Use async/await end to end.
- No `.Result`/`.Wait()`.
- Keep API contracts synchronized across server/client.

## 6) Security Expectations

- Keep auth middleware order intact in server pipeline.
- Protect write endpoints with authorization.
- Enforce ownership/permissions server-side.

## 7) Commit and PR Quality

- Keep commits focused and descriptive.
- Explain what changed and why.
- Include verification steps/results in PR description.

## 8) Documentation Is Part of the Change

If you modify architecture, workflow, style, or known pitfalls, update the corresponding doc in `/docs` in the same PR.
