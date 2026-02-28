## Agent Workflow Contract

This file defines the default operating workflow for AI/code agents in this repository.

## 1) Objective

Deliver correct, reviewable changes with minimal risk.
Extended execution examples: `docs/ai/WORKFLOW_PLAYBOOK.md`.

## 2) Instruction Priority

When guidance conflicts, follow this order:
- User request in the current task
- `AGENT.md`
- `docs/ARCHITECTURE.md`
- `docs/CODE_STYLE.md`
- `docs/BEST_PRACTICES.md`
- `docs/ANTI_PATTERNS.md`
- `docs/CONTRIBUTING.md`
- Existing code patterns

If docs conflict with code behavior, trust running code and update docs in the same change.

## 3) Scope and Routing

Treat this as a single product with these projects:
- `MiniPainterHub.Server`: API and host
- `MiniPainterHub.WebApp`: Blazor WebAssembly client
- `MiniPainterHub.Common`: shared DTO/contracts
- `MiniPainterHub.Server.Tests`: tests

Architecture baseline:
- Controllers are transport layer only.
- Business logic lives in `MiniPainterHub.Server/Services`.
- Persistence is `AppDbContext` in `MiniPainterHub.Server/Data/AppDbContext.cs`.

## 4) Default Execution Workflow

For any non-trivial task, execute this sequence:
1. Discover: read only files needed to understand impacted flow.
2. Plan: identify minimal safe change set.
3. Implement: keep edits focused and consistent with existing patterns.
4. Verify: run required checks from Section 7.
5. Deliver: summarize what changed, why, and what was validated.

## 5) Flex Modes

Pick the lightest mode that safely completes the task:

`Quick Patch`:
- Small isolated fix.
- No architectural changes.
- Run targeted checks.

`Standard Change`:
- Multi-file feature/bug fix.
- Includes code + tests/docs when needed.
- Run project-level checks.

`High-Risk Change`:
- Auth, DB schema, cross-cutting contracts, upload/image pipeline, or security-sensitive paths.
- Add/adjust tests and run broader validation.
- Document assumptions and residual risk.

## 6) Guardrails

Always:
- Use async/await end to end; no `.Result` or `.Wait()`.
- Keep controller actions thin and delegate rules to services.
- Validate request shape at boundary and business rules in services.
- Preserve middleware order in `MiniPainterHub.Server/Program.cs`:
  - `UseAuthentication()` before `UseAuthorization()`.
- Keep API contracts in `MiniPainterHub.Common` unless there is a strong reason not to.

Never:
- Move logic into UI/controller layer to bypass service rules.
- Add raw SQL unless explicitly required.
- Skip verification for risky changes.

## 7) Verification Matrix

Use the strongest applicable check set:

Docs-only changes:
- Optional build.
- Ensure docs point to existing files/types.

Server code changes:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

WebApp UI changes:
- `dotnet build MiniPainterHub.sln`
- Visual smoke test of impacted page(s)
- `./.venv/Scripts/python tools/ui_snapshot_panel.py`
- Review images in `artifacts/ui-panel-check/`
- Behavioral smoke suite:
  - `npm --prefix e2e ci`
  - `npm --prefix e2e run test:smoke`

Contract/auth/data-flow changes:
- Full solution build
- Server tests
- Extra targeted manual checks for impacted endpoints

## 8) Documentation Maintenance Rule

When behavior changes, update docs in the same task:
- Architecture/layering changes -> `docs/ARCHITECTURE.md`
- Style/convention changes -> `docs/CODE_STYLE.md`
- New recommended workflow/pattern -> `docs/BEST_PRACTICES.md`
- New pitfall discovered -> `docs/ANTI_PATTERNS.md`
- Process change for contributors -> `docs/CONTRIBUTING.md`

## 9) Definition of Done

A task is complete only when:
- Requested behavior is implemented.
- Verification for the selected mode is done.
- Relevant docs are updated if guidance changed.
- Summary includes: files changed, validation run, and known follow-ups.
