# Contributing to MiniPainterHub

Thanks for contributing. This guide keeps changes consistent and reviewable.

## 1) Docs to Read First

- `AGENTS.md`
- `AGENT.md`
- [Agent Navigation.md](<../00 Start Here/Agent Navigation.md>)
- [ARCHITECTURE.md](<../20 Engineering/ARCHITECTURE.md>)
- [CODE_STYLE.md](<../20 Engineering/CODE_STYLE.md>)
- [BEST_PRACTICES.md](BEST_PRACTICES.md)
- [ANTI_PATTERNS.md](ANTI_PATTERNS.md)

## 2) Suggested Branch Naming

- `feature/<short-topic>`
- `fix/<short-topic>`
- `refactor/<short-topic>`
- `docs-update/<short-topic>`

## 3) Change Workflow

1. Resolve the minimal relevant file scope first. For non-trivial work, use `AGENTS.md` and `.agents/skills/context-scope-guard`.
2. Make the smallest coherent change set.
3. Add/update tests when behavior changes.
4. Run required validation. For any non-doc code change, the minimum local gate is `dotnet build MiniPainterHub.sln` plus at least one affected automated test command before handoff.
5. Update docs if guidance changed.

## 4) Validation Expectations

Codex Cloud preflight (only when `dotnet` is unavailable):
- `bash tools/cloud/bootstrap-dotnet-and-test.sh`

Minimum local gate for any non-doc code change:
- `dotnet build MiniPainterHub.sln`
- Run at least one affected automated test command for the changed area before handoff. If multiple areas changed, run each affected suite below.

Backend-impacting changes:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory artifacts/test-results`
- `pwsh ./tools/coverage/check-server-coverage.ps1 -CoverageRoot artifacts/test-results -AssemblyName MiniPainterHub.Server -Threshold 80`
- Coverage gate baseline: `80%` line coverage for `MiniPainterHub.Server` (excluding EF migration generated files).

UI-impacting changes:
- `dotnet build MiniPainterHub.sln`
- `npm --prefix e2e ci`
- `npm --prefix e2e run ui-review:scope`
- Run `npm --prefix e2e run test:ui-review` for localized review or `npm --prefix e2e run test:ui-review:full` for full sweeps
- Review screenshots and manifest in `output/playwright/ui-review/`
- Fix obvious in-scope UI defects and rerun the same review scope until it is clean or a real blocker is documented
- `npm --prefix e2e run test:smoke`
- Optional diagnostics:
  - `./.venv/Scripts/python tools/ui_snapshot_panel.py`
  - `python tools/ui_audit_browser_use.py`

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

If you modify architecture, workflow, style, or known pitfalls, update the corresponding vault note in `ObsidianVault/` in the same PR. If the documentation structure changes, update [Vault Specification.md](<../00 Start Here/Vault Specification.md>) and [Project Index.md](<../00 Start Here/Project Index.md>).
