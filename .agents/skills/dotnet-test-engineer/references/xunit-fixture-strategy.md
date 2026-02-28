# xUnit Fixture Strategy

## Fixture choice
- Default to no fixture when setup is cheap and isolation is valuable.
- Use `IClassFixture<T>` when host setup or expensive objects can be shared safely within one test class.
- Use `ICollectionFixture<T>` only for unavoidable shared infrastructure across classes.

## State management rules
- Shared fixture must expose deterministic reset methods.
- Reset mutable state in test setup (`await fixture.ResetAsync()`).
- Do not rely on test execution order.

## Data patterns for this repo
- Service tests: per-test `AppDbContext` from `AppDbContextFactory.Create()`.
- Integration tests: custom `WebApplicationFactory` with test auth and in-memory database override.
- Keep user/post/comment seeds explicit and minimal.

## Naming conventions
- Method format: `<Method>_<Condition>_<ExpectedBehavior>`.
- Keep one dominant assertion theme per test.
- Prefer readable Arrange/Act/Assert separation.
