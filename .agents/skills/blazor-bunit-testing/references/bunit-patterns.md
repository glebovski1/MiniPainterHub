# bUnit Patterns

## Core setup pattern
- Create `using var ctx = new TestContext();`
- Register mocks in `ctx.Services`.
- Render with explicit parameters using `RenderComponent<T>(...)`.

## Dependency injection guidance
- Only register services required by the component under test.
- Use strict mocks for service calls that should happen.
- Return deterministic DTOs to keep assertions stable.

## Auth and navigation
- Use test authentication state provider for `[Authorize]`-gated UI.
- Register `NavigationManager` expectations where redirect behavior matters.

## JS interop
- Configure expected JS interop calls explicitly when component logic depends on them.
- Fail tests on unexpected JS interop calls.
