# AI Workflow Playbook

Use this playbook with `AGENT.md` for task execution.

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
- Run targeted check for impacted project at minimum.

Standard:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj` when backend is touched.

High-Risk:
- Full build + server tests.
- Targeted endpoint/manual checks for changed flows.
- Explicitly document residual risks.

## 3) Retrieval Strategy

When answering or implementing:
- Start from `AGENT.md`.
- Pull details from the most specific matching doc.
- If docs and code conflict, follow code and patch docs in same change.

## 4) Robustness Rules

- Prefer additive changes over broad rewrites unless requested.
- Keep backwards compatibility unless change requires break.
- For breaking changes, include migration notes in task summary.
- Keep test fixtures realistic (valid references and constraints).

## 5) Delivery Template

For each completed task, report:
- Files changed.
- Behavioral impact.
- Validation commands and outcomes.
- Follow-ups (if any).
