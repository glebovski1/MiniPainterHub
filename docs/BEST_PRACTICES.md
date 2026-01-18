# Best Practices

## Development
- Favor `async`/`await` for I/O-bound work.
- Use dependency injection for services and data access.
- Validate inputs at boundaries (e.g., API controllers).

## Architecture
- Keep controllers thin; move business rules to services.
- Centralize data access in the `Data/` layer.
- Keep domain concepts in `Common` for reuse.

## Reliability
- Handle errors gracefully and return meaningful responses.
- Log actionable information without leaking sensitive data.
