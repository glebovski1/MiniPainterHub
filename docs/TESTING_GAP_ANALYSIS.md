# Testing Gap Analysis and Completion Plan

## Baseline (2026-03-03)

- Server tests: `82` (`MiniPainterHub.Server.Tests`)
- WebApp bUnit tests: `3` (`MiniPainterHub.WebApp.Tests`)
- Playwright smoke tests: `5` (`e2e/tests/smoke.spec.js`)
- Server coverage (current gate target assembly): `76.27%` (`1093/1433`, migrations excluded by gate script)
- CI coverage threshold for server: `65%`

## Execution Status (2026-03-03)

- Phase 1 completed.
  - Added shared ProblemDetails assertion helper and reusable integration seeding helpers.
  - Added shared bUnit test doubles/context extensions.
- Phase 2 completed.
  - Added backend tests for image services, validation exception handler, data seeding, OpenAPI upload filter, and auth edge paths.
- Phase 3 completed.
  - Expanded WebApp bUnit suite from `3` to `24` tests.
- Phase 4 completed.
  - Expanded Playwright smoke suite from `5` to `8` tests including protected-route and negative-path coverage.
- Phase 5 completed.
  - Raised CI server coverage gate from `65%` to `80%`.
  - Enabled WebApp coverage collection in CI.
  - Added PR anti-flake rerun step for changed test files.

Current verification snapshot:
- Server tests: `122` passing.
- WebApp tests: `24` passing.
- Playwright smoke tests: `8` passing.
- Server coverage gate run: `85.09%` (`1227/1442`) against threshold `80%`.

## Key Gaps

1. Backend edge-path coverage is uneven.
   - Very low coverage in image storage services (`AzureBlobImageService`, `LocalImageService`), validation/error infrastructure, seeding, and OpenAPI filter code.
2. Frontend unit/component coverage is minimal.
   - Only `Login` has bUnit tests; core user flows (`Registration`, `UserProfile`, posts pages/components) are largely untested at component level.
3. Contract-level negative cases are incomplete.
   - ProblemDetails consistency, validation payload shape, and auth/authorization edge paths are not fully asserted end-to-end.
4. CI quality gates are functional but not yet "polish" level.
   - Server coverage gate is low for current baseline; no explicit WebApp coverage guard; no anti-flake signal step.

## Execution Plan

### Phase 1: Stabilize Test Foundations (P0)

Deliverables:
- Add reusable assertion helper for ProblemDetails in server tests.
- Add test data builders/fixtures for users/posts/comments to reduce setup duplication.
- Add shared bUnit test setup helpers for auth/service stubs and navigation assertions.

Acceptance criteria:
- New helper utilities are used by at least 3 existing test files.
- No behavioral change in app code; all existing tests stay green.

### Phase 2: Close Highest-Risk Backend Gaps (P0)

Target files:
- `MiniPainterHub.Server.Tests/Services/AzureBlobImageServiceTests.cs`
- `MiniPainterHub.Server.Tests/Services/LocalImageServiceTests.cs`
- `MiniPainterHub.Server.Tests/Controllers/AuthControllerTests.cs`
- `MiniPainterHub.Server.Tests/ProgramStartupTests.cs` (or new focused files)
- Add new tests for:
  - `MiniPainterHub.Server/ErrorHandling/ValidationExceptionHandler.cs`
  - `MiniPainterHub.Server/OpenAPIOperationFilter/FileUploadOperationFilter.cs`
  - `MiniPainterHub.Server/Data/DataSeeder.cs`

Test scenarios:
- Image services: upload/save variants, keep-original on/off, delete idempotency, download success path.
- Auth: invalid login model state, register duplicate/identity error mapping, token claims presence.
- Validation handler: DomainValidation and FluentValidation mapping to `application/problem+json` with `errors`.
- Seeder: idempotent role/user creation and `SeedAdmin` safe no-op when config missing.
- OpenAPI filter: no-op for non-file endpoints, multipart schema for file endpoints.

Acceptance criteria:
- Server coverage reaches `>= 80%`.
- All new tests deterministic (no clock/network dependency, isolated fs/db).

### Phase 3: Expand WebApp bUnit Coverage (P1)

Target files to add:
- `MiniPainterHub.WebApp.Tests/Pages/RegistrationTests.cs`
- `MiniPainterHub.WebApp.Tests/Pages/UserProfileTests.cs`
- `MiniPainterHub.WebApp.Tests/Pages/ProfilePanelTests.cs`
- `MiniPainterHub.WebApp.Tests/Pages/Posts/CreateTests.cs`
- `MiniPainterHub.WebApp.Tests/Shared/LikeButtonTests.cs`
- `MiniPainterHub.WebApp.Tests/Shared/CommentFormTests.cs`

Test scenarios:
- Success/failure flows for register/profile create/edit/save.
- Callback wiring and disabled/busy state behavior in `ProfilePanel`.
- Post create submit, validation/error rendering, and navigation behavior.
- Like/comment interaction component state transitions.

Acceptance criteria:
- WebApp test count grows from `3` to `>= 20`.
- Critical UI flows (login, register, create post, profile update) have at least one component-level test each.

### Phase 4: Smoke and Regression Polish (P1)

Deliverables:
- Add 2-3 Playwright smoke checks for high-value regressions:
  - Protected route redirect when unauthenticated.
  - Failed create-post validation message path.
  - Comment failure path (API error surfaced in UI).

Acceptance criteria:
- Smoke suite remains stable in CI (`>= 98%` pass over repeated local runs).
- Failure artifacts stay actionable (trace/screenshot/video already retained).

### Phase 5: Raise CI Quality Gates (P2)

Deliverables:
- Increase server coverage threshold from `65%` to `80%`.
- Add optional WebApp coverage collection and publish as artifact (gate later once baseline is known).
- Add a simple anti-flake step for PRs touching tests (rerun impacted test project once).

Acceptance criteria:
- CI still completes within acceptable runtime budget.
- Coverage gate increases without introducing unstable failures.

## Suggested PR Breakdown

1. Test foundation helpers and refactors only.
2. Backend gap closure (image services + validation + seeding + OpenAPI).
3. Auth/controller contract edge cases.
4. WebApp bUnit expansion.
5. Playwright polish + CI gate raise.

## Done Definition

- Server coverage `>= 80%`.
- WebApp test suite `>= 20` focused bUnit tests.
- Smoke suite expanded and stable.
- CI gates enforce meaningful quality without flaky churn.
