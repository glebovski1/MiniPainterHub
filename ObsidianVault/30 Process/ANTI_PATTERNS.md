# MiniPainterHub Anti-Patterns

This document lists patterns that should be avoided.

## 1) Blocking Async

Avoid:
- `.Result`
- `.Wait()`

Why:
- deadlocks/thread starvation risk
- inconsistent runtime behavior

## 2) Fat Controllers

Avoid putting business rules in controllers:
- ownership checks
- cross-entity workflows
- heavy query logic

Use services for all of the above.

## 3) Duplicate Contracts

Avoid duplicating request/response DTOs across projects.

Use `MiniPainterHub.Common` for shared contracts.

## 4) Inconsistent Auth Flow

Avoid changing middleware order or removing auth middleware in `Program.cs`.

Required order:
- `UseAuthentication()`
- `UseAuthorization()`

## 5) Silent Exception Swallowing

Avoid empty catch blocks and hidden failures.

Log exceptions with context and return consistent errors.

## 6) N+1 Query Patterns

Avoid loops that execute DB query per item.

Prefer projections, bulk fetches, and deliberate includes.

## 7) Unrealistic Test Fixtures

Avoid test data that violates relational assumptions (for example missing referenced users).

Seed all referenced entities in tests to keep behavior close to production.

## 8) UI-Only Security

Avoid relying on hidden buttons or client checks.

Always enforce authorization and ownership on server side.

## 9) Unbounded Upload Paths

Avoid accepting files without type/size/count limits.

Always validate and keep limits centralized in service rules/options.

## 10) Stale Docs as Truth

Avoid leaving docs outdated after behavior changes.

Update the matching vault note in `ObsidianVault/` in the same change when architecture, style, or workflow rules change.
