# MiniPainterHub Best Practices

This guide describes preferred implementation patterns for MiniPainterHub.

## 1) Decision Principles

- Optimize for correctness first, then clarity, then performance.
- Keep change scope small and reviewable.
- Make behavior explicit at boundaries (DTOs, validation, errors).

## 2) Layer Responsibilities

Controllers:
- Handle routing, binding, HTTP semantics.
- Keep orchestration minimal.

Services:
- Implement business workflows.
- Enforce domain validation and permissions.
- Coordinate persistence and external dependencies.

Data layer:
- `AppDbContext` remains the persistence boundary.
- Entities model storage concerns, DTOs model transport concerns.

## 3) Validation Strategy

- Validate request shape at API boundary.
- Validate business rules in service layer.
- Return consistent error contracts using problem details.

## 4) API Contract Discipline

- Shared API request/response DTOs should live in `MiniPainterHub.Common` when used by server and WebApp.
- Avoid duplicate DTO definitions across projects.
- If contracts change, update both server and client callers in the same task.

## 5) Data Access

- Prefer query projection to DTOs for read endpoints.
- Use pagination for list endpoints.
- Keep soft-delete behavior consistent in reads/writes.

## 6) Authentication and Authorization

- Use JWT claims as identity source in controllers.
- Re-check ownership/permissions in services for any write action.
- Do not rely on UI visibility as a security mechanism.

## 7) Image and Upload Flows

- Enforce upload count and size limits consistently.
- Validate content type before processing.
- Use `IImageProcessor` + `IImageStore` pipeline for variant workflows.
- Keep storage backend abstracted (`LocalImageService` vs `AzureBlobImageService`).

## 8) Testing Practice

Minimum expected verification for backend-impacting changes:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

Test data quality rule:
- Seed realistic relational data even for EF InMemory tests.
- Avoid impossible states that production constraints would block.

## 9) Documentation Practice

When code behavior changes, update the matching docs in `/docs` during the same change.

## 10) Preferred Delivery Format

For each completed task provide:
- What changed.
- Why it changed.
- Validation run.
- Remaining risks or follow-ups.
