## Agent Workflow Contract

This file defines the default operating workflow for AI/code agents in this repository.

## 1) Objective

Deliver correct, reviewable changes with minimal risk.
Extended execution examples: `docs/ai/WORKFLOW_PLAYBOOK.md`.

## 2) Instruction Priority

When guidance conflicts, follow this order:
- User request in the current task
- `AGENTS.md`
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
0. Scope: use `AGENTS.md` and `.agents/skills/context-scope-guard` to identify the minimal allowed file set before broad discovery.
1. Discover: read only files inside the current scope needed to understand impacted flow.
2. Plan: identify minimal safe change set.
3. Implement: keep edits focused and consistent with existing patterns.
4. Verify: run required checks from Section 7.
5. Deliver: summarize what changed, why, and what was validated.

### UI Change Overlay

Treat a task as a `UI Change` when it edits any of the following:
- Razor pages or shared UI components in `MiniPainterHub.WebApp`
- CSS, design tokens, layout, spacing, navigation, or responsive behavior
- Shared UI primitives, shell, empty/loading/error states, or auth/panel presentation

For any `UI Change`, also follow `docs/ai/UI_QUALITY_PLAYBOOK.md`.
For complex UI/UX implementation, redesigns, layout refactors, responsive fixes, modal/viewer/panel/sidebar work, or visual bug fixing, also use `.codex/skills/ui-iteration-guard/SKILL.md`.
Do not implement large UI changes in one pass. Plan the screen first, build in layers, visually validate each layer, and return to the last stable layer before continuing if a major regression appears.
Playwright is the authoritative UI verification tool in this repository. Optional browser-use scripts are diagnostics only and do not replace the Playwright review workflow.
For UI work, do not stop at defect reporting. If the Playwright review shows obvious in-scope defects, fix them and rerun the review loop until the reviewed scope is clean or a real blocker is documented.

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
- Broad-scan the repo before resolving a minimal file scope.
- Move logic into UI/controller layer to bypass service rules.
- Add raw SQL unless explicitly required.
- Skip verification for risky changes.
- Treat UI work as complete because it compiles or renders once.
- Claim UI success without reviewed Playwright screenshots for the required routes, states, and viewports.
- Stop after listing UI defects that are within the current task scope to fix.

## 7) Verification Matrix

Use the strongest applicable check set:

Codex Cloud preflight (when `dotnet` is missing):
- `bash tools/cloud/bootstrap-dotnet-and-test.sh`

Docs-only changes:
- Optional build.
- Ensure docs point to existing files/types.

Server code changes:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

WebApp UI changes:
- `dotnet build MiniPainterHub.sln`
- `npm --prefix e2e ci`
- Resolve scope with `npm --prefix e2e run ui-review:scope`
- Run `npm --prefix e2e run test:ui-review` for localized review or `npm --prefix e2e run test:ui-review:full` for full sweeps
- Review screenshots and manifest in `output/playwright/ui-review/`
- Fix the reviewed defects and rerun the Playwright review until no obvious problems remain in the reviewed scope
- Behavioral smoke suite:
  - `npm --prefix e2e run test:smoke`
- Optional diagnostics:
  - `./.venv/Scripts/python tools/ui_snapshot_panel.py`
  - `python tools/ui_audit_browser_use.py`

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
- For `UI Change` tasks: the Playwright review manifest exists, screenshots were explicitly reviewed, and the fix-and-rerun loop was completed until no obvious defects remained in the reviewed scope or a real blocker was documented.
- Summary includes: files changed, validation run, and known follow-ups.
