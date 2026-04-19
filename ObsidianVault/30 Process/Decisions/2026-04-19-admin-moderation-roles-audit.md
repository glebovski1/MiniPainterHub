---
type: adr
status: accepted
date: 2026-04-19
canonical: true
related_code:
  - MiniPainterHub.Server/Controllers/ModerationController.cs
  - MiniPainterHub.Server/Controllers/ReportsController.cs
  - MiniPainterHub.Server/Services/ModerationService.cs
  - MiniPainterHub.Server/Services/ReportService.cs
  - MiniPainterHub.Server/Entities/ModerationAuditLog.cs
related_notes:
  - ../README.md
  - ../../20 Engineering/ARCHITECTURE.md
---

# ADR: Admin Moderation Roles And Audit Trail

## Context

The superseded admin moderation implementation plan captured decisions that now belong in durable project memory. The current architecture note already records moderation controllers, services, audit logs, report flows, admin pages, and Playwright smoke coverage.

## Decision

MiniPainterHub keeps moderation rules in the service layer and exposes them through thin API controllers.

Accepted moderation model:

- `Moderator` can hide, restore, and review content.
- `Admin` can perform moderator actions and suspend or unsuspend users.
- Moderation actions produce append-only audit log entries.
- Reports are resolved through explicit report workflow endpoints rather than ad-hoc content mutation.
- Soft-delete and moderation visibility remain explicit service-query predicates unless a dedicated refactor introduces global query filters uniformly.
- Shared request/response contracts used by server and WebApp belong in `MiniPainterHub.Common`.
- Errors continue to use the existing ProblemDetails exception path.

## Consequences

- Controllers should stay transport/orchestration only.
- Future moderation work should update [Architecture](<../../20 Engineering/ARCHITECTURE.md>) when API surface, entities, or service responsibilities change.
- Any change to roles, audit mutability, or soft-delete semantics needs a new ADR or an update to this one.
- Historical planning content can be archived after these durable decisions are captured.
