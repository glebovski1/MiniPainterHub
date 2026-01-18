# Code Style

## General
- Use clear, descriptive names for classes, methods, and variables.
- Prefer small, focused methods and classes.
- Keep code self-documenting; add comments only where intent is non-obvious.

## C# Conventions
- Use `PascalCase` for public types and members.
- Use `camelCase` for local variables and parameters.
- Favor `async`/`await` for asynchronous work.
- Prefer expression-bodied members where it improves readability.

## Organization
- Keep controllers thin and delegate business logic to the service layer.
- Place data access in the `Data/` layer and access EF Core via `HobbyCenterContext`.
- Group related functionality in cohesive folders and namespaces.
