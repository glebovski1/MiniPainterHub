# Testing Gap Analysis (Unit, Integration, E2E)

## Scope
This analysis reviews the current automated test coverage in:
- `MiniPainterHub.Server.Tests` (service + API behavior)
- `MiniPainterHub.WebApp.Tests` (component tests with bUnit)
- `e2e` Playwright smoke suite

## Current State

### 1) Unit tests
**What exists today**
- Strong service-layer unit coverage on backend business logic:
  - Posts, comments, likes, profiles
  - Image processing and storage adapters
- Tests are mostly isolated with mocked dependencies and in-memory EF usage.

**Strengths**
- Business rules are validated close to service boundary.
- Image pipeline has direct tests (processor/store services), which reduces regression risk.

**Gaps**
- DTO validation edge cases (null/whitespace/length boundaries) are not consistently tested.
- Negative/authz scenarios are less represented than happy-paths in some areas.
- WebApp unit coverage is currently very narrow (focused mainly on login page behavior).

### 2) Integration tests
**What exists today**
- Controller tests and upload tests run through an in-process test host (`WebApplicationFactory` pattern).
- Startup tests validate host composition.
- There is infrastructure for integration testing (`IntegrationTestApplicationFactory`, test data helpers).

**Strengths**
- Good baseline for transport-to-service behavior.
- Upload endpoint behavior is covered with realistic request handling.

**Gaps**
- Limited explicit contract tests around auth policy boundaries and ProblemDetails error envelopes.
- Limited coverage of persistence-specific behavior that differs from in-memory providers.
- Missing migration safety checks (e.g., schema drift checks in CI).

### 3) End-to-end (E2E)
**What exists today**
- Playwright smoke suite covers:
  - login success/failure
  - post creation
  - comment + like interaction
  - profile create/edit
- E2E suite includes reset endpoint + isolated DB convention for test runs.

**Strengths**
- Covers critical user journeys with serial, deterministic setup.
- Includes trace/screenshot/video-on-failure configuration.

**Gaps**
- Coverage is smoke-level only (no deep flows around uploads, pagination, authorization redirects, or concurrency).
- Browser/runtime dependency bootstrap is not yet robust across all environments.
- No explicit cross-browser matrix or mobile viewport coverage.

## Recommendations

## Priority 1 (High impact, low-medium effort)
1. **Expand WebApp component tests beyond login**
   - Add bUnit tests for:
     - `Create` post form validation and submit states
     - `CommentForm` and optimistic UI updates
     - Profile panel create/edit validation
   - Goal: raise confidence in client-side state and validation without full E2E runtime.

2. **Add API error-contract integration tests**
   - Verify standardized ProblemDetails payloads for:
     - 400 validation failures
     - 401/403 auth/authz failures
     - 404 not found cases
   - Goal: lock down API contract expectations for frontend consumers.

3. **Introduce mutation-style negative tests for services**
   - Specifically test whitespace/empty content, forbidden ownership operations, and duplicate-like behavior under repeated calls.
   - Goal: harden domain invariants.

## Priority 2 (Medium impact)
4. **Run integration tests against relational provider in CI lane**
   - Keep fast in-memory tests for PR feedback.
   - Add nightly/merge-gate lane using SQLite or SQL Server container for EF behavior parity.
   - Goal: catch query translation and relational constraints issues.

5. **Broaden E2E from smoke to scenario packs**
   - Add suites for:
     - image upload + rendering variants
     - pagination/sorting behavior
     - unauthorized navigation and token-expiry behavior
   - Goal: cover high-value workflows with production-like behavior.

6. **Add test data builders/fixtures to reduce duplication**
   - Consolidate repeated setup patterns into factories/builders for services and controllers.
   - Goal: lower maintenance cost and increase readability.

## Priority 3 (Strategic)
7. **Define coverage quality gates**
   - Set thresholds (example):
     - Service layer line coverage >= 80%
     - Critical-path branch coverage >= 70%
   - Enforce with `coverlet` + CI reporting.

8. **Add contract tests between WebApp service clients and API DTOs**
   - Validate serialization/deserialization and nullability expectations for key endpoints.
   - Goal: catch silent integration breaks early.

9. **Create a test pyramid policy in docs**
   - Document desired ratio and purpose of unit vs integration vs E2E.
   - Goal: guide future contributors toward sustainable test investment.

## Suggested Immediate Next Sprint Plan
1. Add 6-10 bUnit tests for `Create`, `CommentForm`, and profile panel.
2. Add 6-8 integration tests focused on ProblemDetails and authz boundaries.
3. Add 2 Playwright scenarios: image upload flow + unauthorized route redirect.
4. Add CI step to publish coverage summary and fail below threshold.
