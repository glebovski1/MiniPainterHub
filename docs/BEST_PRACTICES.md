# Best Practices for HobbyCenter (BEST_PRACTICES.md)

This guide defines the recommended patterns and practices for developing and refactoring HobbyCenter.
It’s designed for both humans and AI assistants to keep the codebase consistent, maintainable, and safe.

If you’re unsure how to implement something, check **Architecture** first: `docs/ARCHITECTURE.md`, and follow **Code Style**: `docs/CODE_STYLE.md`.

---

## Core Principles

1. **Controllers are thin; Services are thick.**
2. **Async all the way. No `.Result` / `.Wait()`.**
3. **Validate at the edges, enforce rules in services.**
4. **Prefer simple data models (FKs + navigation) over manual join-table orchestration (unless truly needed).**
5. **Make failures visible: log and return consistent errors.**
6. **Refactor toward consistency (docs are the source of truth).**

---

## Layer Responsibilities

### Controllers (`Controllers/`)

Controllers should:

* Handle routing and HTTP semantics (status codes, redirects, view rendering).
* Bind and validate request input (via model binding and `ModelState`).
* Extract identity/claims (`User`).
* Call service methods and translate results into responses.

Controllers should not:

* Build complex EF queries.
* Contain business rules (ownership checks, rating update logic, etc.).
* Block on async.

### Services (`Services/`)

Services should:

* Own business rules and workflows.
* Interact with EF Core context.
* Perform authorization checks *in addition to controller-level `[Authorize]`* (ownership, etc.).
* Return clear result objects or booleans (and log failures when needed).

Services should not:

* Depend on UI or MVC types (`ViewBag`, `IActionResult`, etc.).
* Use `HttpContext` directly (keep services testable and reusable).

---

## Dependency Injection

### Register services with correct lifetimes

* `DbContext`: **Scoped** (one per request)
* Domain services: **Scoped**
* Pure helpers: **Singleton** only if truly stateless and thread-safe

Avoid:

* Creating `DbContext` manually with `new`.
* Using Service Locator (`HttpContext.RequestServices...`) in business logic.

Preferred:

* Constructor injection everywhere.

Example:

```csharp
public class ImageService
{
    private readonly HobbyCenterContext _context;
    private readonly ILogger<ImageService> _logger;

    public ImageService(HobbyCenterContext context, ILogger<ImageService> logger)
    {
        _context = context;
        _logger = logger;
    }
}
```

---

## Authentication & Authorization

### Middleware order matters

Authentication and authorization must be configured correctly:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

### Protect write actions

Any action that creates/modifies/deletes data should be:

* `[Authorize]` at controller/action level
* and also checked in the service for ownership/permissions

Example:

* Controller: `[Authorize]`
* Service: verify the current user owns the image before deletion

**Do not trust the UI** to hide buttons. Always validate server-side.

---

## Async and EF Core Best Practices

### Async all the way

* Services that hit the DB should be async (`Task`/`Task<T>`).
* Controllers should `await` service calls.
* Never use `.Result` / `.Wait()`.

### Prefer EF-side operations

Do calculations in SQL when possible:

* `AnyAsync`, `CountAsync`, `AverageAsync`, `SumAsync`
* Use `Select` projections to fetch only what you need

Avoid:

* Loading a full list into memory just to count or average (unless tiny and justified).

### Avoid N+1 queries

If you loop items and query the DB inside the loop, you likely have N+1.

Bad:

```csharp
foreach (var comment in comments)
{
    var user = await _context.Users.FindAsync(comment.UserId);
}
```

Better:

* Query with joins/projections
* Or load users in one query
* Or use navigation properties with `Include` (carefully)

---

## Data Modeling and Relationships

### Prefer straightforward FK relationships

Current project uses join/link tables for relationships (images ↔ users, comments ↔ images, ratings ↔ images). This adds complexity and often causes bugs and extra queries.

Preferred (refactor direction):

* `ImageModel` has `OwnerUserId`
* `CommentModel` has `ImageId` and `UserId`
* `RatingModel` has `ImageId` and `UserId` (and unique constraint on pair)

### Enforce integrity

* Configure cascading deletes where appropriate
* Ensure dependent records are removed when a parent is deleted
* Add indexes for frequent queries (e.g., `ImageId`, `UserId`)

---

## Validation Strategy

### Validate at the edges (Controller)

Use model validation for request shape:

* Data annotations (`[Required]`, `[MaxLength]`)
* `ModelState.IsValid`

If invalid:

* MVC: return the same view with validation errors
* API: return `BadRequest(ModelState)` or a structured error response

### Enforce rules in services

Service should validate:

* entity exists (image, user, comment)
* ownership and permissions
* “one rating per user per image” rule
* business constraints (e.g., rating range)

---

## Error Handling and Result Patterns

### Prefer explicit results over silent failures

Do not swallow exceptions.

Use one of these patterns:

#### Pattern A: Return a “Result” object

```csharp
public record ServiceResult(bool Success, string? Error = null);

public async Task<ServiceResult> DeleteImageAsync(int userId, int imageId)
{
    // ...
    return new ServiceResult(true);
}
```

#### Pattern B: Return bool + log error

Acceptable for small operations, but log failures.

#### Pattern C: Throw domain exceptions

Use with a consistent controller/global handler.

---

## Logging

Log meaningful events:

* unexpected exceptions
* denied operations (ownership fail)
* important write operations (delete image, update rating)

Never log:

* passwords
* hashes/salts
* secrets

Use structured logging if possible:

```csharp
_logger.LogWarning("User {UserId} tried to delete image {ImageId} without ownership", userId, imageId);
```

---

## Controller Patterns (MVC and API)

### Keep one source of truth for write endpoints

If you have both:

* MVC actions
* API endpoints

Avoid duplicating logic across both. Prefer:

* Controller action calls service
* API controller calls same service
* Both share validation and rules in service

Do not maintain two separate business rule implementations.

---

## ViewModels and UI Concerns

* ViewModels should not contain EF entities.
* ViewModels can include UI helpers like `CanDelete`, `AverageRating`, etc.
* Compute UI flags in services or controllers, but rules should still be enforced in services.

---

## JavaScript and AJAX

* Prefer moving JS into `wwwroot/js/` once it grows.
* Handle failure cases (show user feedback, not only `console.error`).
* Use consistent endpoints and response shapes.

---

## Refactoring Practices

### Make refactors safe

When refactoring:

* change one layer at a time
* keep commits small and focused
* update docs when a pattern changes
* prefer tests for the service layer (future direction)

### Known high-impact refactors (recommended roadmap)

* Remove `.Result` usage everywhere
* Fix/ensure auth middleware order (`UseAuthentication`)
* Simplify relationships (reduce manual join-table usage)
* Prevent divide-by-zero in rating averages
* Avoid orphan records on deletes (cascade or manual cleanup)

---

## Checklist Before Merge

* [ ] No `.Result` / `.Wait()` added
* [ ] Controllers remain thin
* [ ] Services own business rules and authorization checks
* [ ] EF queries are async and avoid N+1
* [ ] Validation is consistent (ModelState + service checks)
* [ ] Errors are logged and returned consistently
* [ ] Docs updated if architecture/pattern changed
