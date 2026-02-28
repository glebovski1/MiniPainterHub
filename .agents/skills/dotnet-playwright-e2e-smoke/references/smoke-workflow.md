# Smoke Workflow

## Scope Selection
- Keep smoke coverage to core user value and release risk.
- Prefer 3-6 tests over broad UI coverage.

## Suggested Flow Set
1. Login with valid credentials.
2. Create a post and verify it appears.
3. Add a comment and toggle like.
4. Update profile and verify persisted visible state.
5. Negative path: invalid input or unauthorized action returns expected UI feedback.

## Test Design Rules
- One user journey per test.
- Use explicit setup and teardown.
- Keep each test independent and idempotent.
- Avoid fixed sleeps when waiting for network/UI state; use explicit waits.

## Selector Policy
- Prefer `data-testid` or stable role-based selectors.
- Avoid CSS structure selectors that break on layout refactors.

## Failure Diagnostics
- Capture trace/screenshot/video only on failure.
- Store artifacts with predictable names tied to test title.
