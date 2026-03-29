# AI Workflow Playbook

Use this playbook with `AGENT.md` for task execution and `AGENTS.md` for repo-wide context minimization.

## 1) Mode Selection

Choose one mode per task:

`Quick Patch`
- Small isolated bug/docs fix.
- No contract or architecture changes.

`Standard`
- Multi-file feature/fix.
- Localized impact in one domain.

`High-Risk`
- Auth, persistence rules, API contracts, upload/image pipeline, or cross-domain refactor.

## 2) Per-Mode Validation

Quick Patch:
- Run `dotnet build MiniPainterHub.sln`.
- Run at least one relevant automated test command for the impacted area.

Standard:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj` when backend is touched.

High-Risk:
- Full build + server tests.
- Run `MiniPainterHub.WebApp.Tests` when client service contracts or query composition changed.
- Targeted endpoint/manual checks for changed flows.
- Explicitly document residual risks.

## 2.5) UI Change Workflow

If a task touches UI, layout, CSS, responsive behavior, navigation, or shared client presentation:
- Follow `docs/ai/UI_QUALITY_PLAYBOOK.md`
- Treat Playwright review as mandatory, not optional
- Run the behavioral smoke suite and the UI review suite
- Review screenshots before closing the task
- Fix obvious in-scope defects immediately and rerun the same review scope
- Repeat until the reviewed scope is clean or a real blocker is documented

Use the repo commands:
- `npm --prefix e2e run ui-review:scope`
- `npm --prefix e2e run test:ui-review`
- `npm --prefix e2e run test:ui-review:full`
- `npm --prefix e2e run test:smoke`

Default UI review scope:
- Changed areas + shared shell + one narrow viewport

Escalate to a full app sweep when:
- global CSS or theme files change
- `MiniPainterHub.WebApp/Layout/` changes
- shared UI primitives or shell components change
- auth shell or navigation composition changes

UI remediation loop:
1. Resolve the review scope.
2. Run the required Playwright review.
3. Inspect screenshots and manifest.
4. Fix every obvious issue that is in scope for the current task.
5. Rerun the same review.
6. Only stop when the scope is clean or a genuine blocker is recorded in the delivery summary.

## 3) Retrieval Strategy

When answering or implementing:
- Resolve the minimal file scope first with `AGENTS.md` for non-trivial work.
- Start from `AGENT.md`.
- Pull details from the most specific matching doc.
- If docs and code conflict, follow code and patch docs in same change.

## 4) Robustness Rules

- Prefer additive changes over broad rewrites unless requested.
- Keep backwards compatibility unless change requires break.
- For breaking changes, include migration notes in task summary.
- Keep test fixtures realistic (valid references and constraints).
- For auth/data-flow/schema changes, update both code and matching docs (`docs/ARCHITECTURE.md`, and `project_structure.txt` if structure references changed) in the same change.

## 5) Delivery Template

For each completed task, report:
- Files changed.
- Behavioral impact.
- Validation commands and outcomes.
- Follow-ups (if any).

For UI tasks also report:
- which routes/states/viewports were reviewed
- where the screenshots/report were written
- whether the review was localized or full-app

## 6) Phase Gates

When implementing a multi-phase feature or fix:
- Complete one phase at a time.
- After each code-change phase, run `dotnet build MiniPainterHub.sln` and at least one relevant automated test command before moving on.
- Prefer narrow test slices first, then broad suite/build gates at the end.
- If a phase fails validation, fix it before starting the next phase.
