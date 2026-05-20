---
name: "dotnet-test-engineer"
description: "Use when implementing or reviewing .NET tests with xUnit, fixture strategy, WebApplicationFactory integration patterns, or ProblemDetails contract assertions."
---

# .NET Test Engineer

## When to use
- Adding or refactoring tests in ASP.NET Core/.NET projects.
- Choosing between unit, service, and integration tests.
- Designing fixture and lifetime strategy for xUnit.
- Asserting ProblemDetails responses (`application/problem+json`).

## Workflow
1. Identify target behavior and risk.
   - Contract and authorization, business rules, persistence, serialization.
2. Choose test level.
   - Unit: no host, fast deterministic.
   - Service: EF in-memory and stub dependencies.
   - Integration: `WebApplicationFactory<Program>` with overridden services.
3. Apply xUnit fixture strategy.
   - No fixture for isolated tests.
   - `IClassFixture<T>` for shared expensive setup per class.
   - `ICollectionFixture<T>` only for required cross-class shared state.
   - Add reset hooks to avoid order coupling.
4. Standardize ProblemDetails assertions for API tests.
   - Assert status code.
   - Assert content type contains `application/problem+json`.
   - Parse ProblemDetails and assert `title`, `detail`, `status`.
   - Assert expected extensions (`errors`, `requestId`) when relevant.
5. Keep tests deterministic.
   - Seed explicit identities and IDs.
   - Avoid wall-clock dependencies when possible.
   - Isolate filesystem and network through interfaces and test doubles.
6. Verify and report.
   - Run targeted tests first, then broader suite.
   - Report untested risky paths.

## Repo-focused defaults
- Prefer `MiniPainterHub.Server.Tests` for backend tests.
- Reuse existing patterns in `Infrastructure/AppDbContextFactory.cs` and `Infrastructure/TestData.cs` when appropriate.
- For host-level tests, follow the approach from `Controllers/PostsUploadTests.cs`.
- Put durable test strategy updates in `ObsidianVault/20 Engineering/TESTING_GAP_ANALYSIS.md` or `ObsidianVault/30 Process/WORKFLOW_PLAYBOOK.md`.

## References
- `references/xunit-fixture-strategy.md`
- `references/problem-details-assertions.md`
