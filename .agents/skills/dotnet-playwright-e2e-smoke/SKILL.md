---
name: "dotnet-playwright-e2e-smoke"
description: "Use when adding or maintaining Playwright end-to-end smoke tests for a .NET web app, including deterministic test setup, stable selectors, and CI artifact capture."
---

# .NET Playwright E2E Smoke

## When To Use
- Creating first-pass end-to-end smoke tests for core user journeys.
- Investigating regressions that unit/integration tests did not catch.
- Defining stable selectors and deterministic browser test setup.
- Adding CI jobs for browser smoke checks and failure artifacts.

## Workflow
1. Choose a thin smoke scope.
   - Cover only highest-value flows: auth, create/read/update actions, and one critical negative path.
2. Make test setup deterministic.
   - Use known seeded users and data.
   - Reset or isolate test data before each scenario.
3. Use resilient selectors.
   - Prefer explicit test IDs and stable accessible roles.
   - Avoid brittle style/layout selectors.
4. Keep assertions user-centered.
   - Verify page state and key user-visible outcomes.
   - Avoid over-asserting internal implementation details.
5. Keep suite fast.
   - Small number of flows, serial only when isolation demands it.
   - Push domain/contract checks to backend integration tests.
6. Wire into CI.
   - Run smoke tests after build and service startup.
   - Upload traces/screenshots/videos on failure.

## Repo-Focused Defaults
- Scope smoke tests to the top flows for `MiniPainterHub.WebApp` with API hosted by `MiniPainterHub.Server`.
- Start with journeys:
  - login
  - create post
  - comment/like
  - profile update/avatar path
- Keep UI snapshot checks as separate diagnostics, not replacements for behavior assertions.
- Keep durable browser-review policy in `ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md`.

## References
- `references/smoke-workflow.md`
- `references/playwright-ci-integration.md`
