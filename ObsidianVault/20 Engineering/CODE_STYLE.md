# MiniPainterHub Code Style Guide

This document defines coding conventions for MiniPainterHub.

## 1) Core Standards

- Keep controllers thin and services thick.
- Use async/await end to end.
- Prefer explicit, readable code over clever code.
- Match existing project patterns unless there is a clear reason to improve them.

## 2) Project Layout

- `MiniPainterHub.Server/Controllers`: HTTP transport concerns only.
- `MiniPainterHub.Server/Services`: business rules and orchestration.
- `MiniPainterHub.Server/Data`: EF Core context and configuration.
- `MiniPainterHub.Server/Entities`: persistence models.
- `MiniPainterHub.Common`: shared DTO/contracts.
- `MiniPainterHub.WebApp/Services`: API client-side wrappers.

## 3) Naming

- Types: `PascalCase`
- Methods/properties: `PascalCase`
- Interfaces: `I` prefix + `PascalCase`
- Local variables/parameters: `camelCase`
- Private fields: `_camelCase`
- Async methods returning `Task`/`Task<T>`: suffix `Async`

## 4) C# Formatting

- 4 spaces indentation, no tabs.
- Prefer braces for all control blocks.
- Keep methods focused and short where practical.
- Use file-scoped namespaces in new files when practical.

## 5) Async and EF Core Rules

- Do not use `.Result` or `.Wait()`.
- Prefer EF async APIs (`AnyAsync`, `FirstOrDefaultAsync`, `ToListAsync`, `SaveChangesAsync`).
- Avoid N+1 query patterns; use projection/join/include deliberately.
- Use `AsNoTracking()` for read-only queries unless tracking is needed.

## 6) Controller and Service Boundaries

Controllers should:
- Validate transport-level input.
- Extract user identity/claims.
- Call service methods.
- Return HTTP responses.

Services should:
- Enforce business rules and permissions.
- Own entity existence checks and domain validation.
- Avoid web/UI-specific types.

## 7) Dependency Injection

- Use constructor injection.
- Typical lifetimes:
  - `DbContext`: scoped
  - Domain services: scoped
  - Stateless helpers: singleton when thread-safe

## 8) Error Handling and Logging

- Do not swallow exceptions.
- Throw meaningful domain exceptions in services.
- Use structured logging with context.
- Never log secrets, tokens, or passwords.

## 9) Security and Auth

- Keep middleware order in server pipeline:
  - `UseAuthentication()`
  - `UseAuthorization()`
- All modifying endpoints must require authorization and service-side permission checks.

## 10) CSS Ownership

- Put global design tokens, resets, layout primitives, and cross-route utilities in `MiniPainterHub.WebApp/wwwroot/css/app.css`.
- Put site-wide polish overrides that must load after the base global stylesheet in `MiniPainterHub.WebApp/wwwroot/css/app-polish.css`.
- Put reusable component styles beside the shared component in its `.razor.css` file.
- Put route-only styles beside the owning page in that page's `.razor.css` file.
- Avoid adding page-specific selectors to `app.css`; move them into page-scoped CSS when ownership is clear.

## 11) Documentation Rule

When introducing a new pattern or changing an existing one, update the relevant vault note in `ObsidianVault/` in the same change. Use [Agent Navigation.md](<../00 Start Here/Agent Navigation.md>) to choose the source of truth.

Run `python tools/docs/check-doc-references.py --repo .` after moving, deleting, or renaming docs, skills, or commonly referenced code paths.
