# Anti-Patterns

## Avoid
- Writing business logic directly in controllers.
- Using `.Result` or `.Wait()` on asynchronous calls.
- Introducing raw SQL unless explicitly required.
- Bypassing the service layer for data access.
