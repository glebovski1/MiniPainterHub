---
name: "blazor-bunit-testing"
description: "Use when designing or implementing bUnit tests for Blazor components and pages, including render-state assertions, DI setup, auth state, and user interaction flows."
---

# Blazor bUnit Testing

## When to use
- Adding tests for Blazor components or pages.
- Verifying render-state transitions, conditional UI, and interactions.
- Mocking DI services, auth state, or JS interop dependencies in component tests.

## Workflow
1. Define behavior under test.
   - Initial render state, user interaction, async updates, error states.
2. Build a deterministic `TestContext`.
   - Register required services in `Services`.
   - Mock dependencies with stable data.
   - Configure auth state provider when auth-sensitive UI is involved.
3. Render and interact.
   - Render component with explicit parameters.
   - Trigger events (`Click`, `Input`, `Change`, form submit).
4. Assert render state.
   - Use semantic assertions on visible content and key selectors.
   - Assert transition from loading to loaded or error states.
   - Use `WaitForAssertion` for async UI updates.
5. Keep tests maintainable.
   - Avoid fragile full-markup snapshots for dynamic pages.
   - Prefer focused assertions that reflect user-visible behavior.

## Repo-focused defaults
- Place bUnit tests in a dedicated `MiniPainterHub.WebApp.Tests` project.
- Mirror production DI registration only as needed for the target component.
- Mock API-facing services (`IPostService`, `ICommentService`, etc.) at component boundary.

## References
- `references/bunit-patterns.md`
- `references/render-state-assertions.md`
