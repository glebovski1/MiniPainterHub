# HobbyCenter Code Style Guide (CODE_STYLE.md)

This document defines the coding conventions and “house style” for the HobbyCenter ASP.NET Core MVC project.
It is part of the repo’s in-repo RAG knowledge base: when adding or refactoring code, follow these rules first.

If a guideline conflicts with an existing pattern in code, prefer **this document** and refactor toward it.

---

## Goals

* Keep the codebase consistent and easy to scan.
* Prevent common reliability issues (especially around async and middleware).
* Make refactoring safe by standardizing patterns and file layout.

---

## Project Structure Expectations

Keep code organized by responsibility:

* `Controllers/` — HTTP endpoints + MVC flow. **Thin**. No heavy business logic.
* `Services/` — business logic + orchestration of EF Core operations. **Thick**.
* `Data/` — EF Core `DbContext`, configuration, migrations (if/when added).
* `Models/` — EF Core entities (database schema).
* `ViewModels/` — models passed to views / API responses (no persistence concerns).
* `Utils/` — small pure helpers (e.g., password hashing). Avoid “god utils”.

**Rule of thumb:**
If code decides *what should happen* → service.
If code decides *how to respond to an HTTP request* → controller.

---

## Naming Conventions

### Types

* **Classes / Records / Structs:** `PascalCase`
  Examples: `ImageService`, `AccountController`, `RatingModel`, `ImageViewModel`.
* **Interfaces:** `I` + `PascalCase`
  Examples: `IImageRepository`, `IClock`.
* **Enums:** `PascalCase` singular noun
  Example: `UserRole`.

### Members

* **Methods / Properties:** `PascalCase`
  Examples: `GetImageAsync`, `AverageRating`, `RemoveImageAsync`.
* **Fields (private):** `_camelCase`
  Examples: `_context`, `_logger`, `_imageService`.
* **Local variables / parameters:** `camelCase`
  Examples: `imageId`, `userId`, `createdAt`.

### Async Naming

* Any method returning `Task` / `Task<T>` should end with **`Async`**, especially in services and data access.

  * ✅ `GetUserByNameAsync`
  * ✅ `AddCommentToImageAsync`
  * ❌ `GetUserByName` (if it returns `Task<UserModel>`)

**Controller actions are allowed to omit `Async`** (ASP.NET convention), but consistency is preferred if the file is already using `Async`.

### Boolean Names

* Use prefixes: `Is`, `Has`, `Can`, `Should`

  * `IsOwner`, `HasImage`, `CanDelete`, `ShouldShowComments`.

---

## Formatting & Layout

### C# Formatting

* **Indentation:** 4 spaces (no tabs).
* **Braces:** Allman style (braces on a new line).
* **Always use braces** for `if/else/for/foreach/while`, even for one-liners.

Preferred:

```csharp
if (isValid)
{
    DoWork();
}
else
{
    HandleError();
}
```

Avoid:

```csharp
if (isValid) DoWork();
```

### Line Length

* Soft limit: **120 characters**.
* Break long LINQ chains and method calls across lines.

Example:

```csharp
var ratings = await _context.Ratings
    .Where(r => r.ImageId == imageId)
    .Select(r => r.Value)
    .ToListAsync();
```

### File Layout Order

Recommended order inside a `.cs` file:

1. `using` directives
2. `namespace ...;` (file-scoped namespace preferred)
3. Type declaration
4. Fields
5. Constructor(s)
6. Public methods
7. Private methods

---

## Using Directives

Group `using` directives:

1. `System.*`
2. third-party packages
3. project namespaces

Example:

```csharp
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

using HobbyCenter.Data;
using HobbyCenter.Models;
```

---

## Dependency Injection Style

* Prefer **constructor injection** only.
* No `HttpContext.RequestServices.GetService(...)` inside business logic.
* Services should be registered in `Program.cs` with correct lifetime:

  * `DbContext` → **Scoped**
  * Domain services → **Scoped**
  * Stateless helpers → **Singleton** only if truly safe

**Rule:** One request = one DbContext scope.

---

## Async/Await Rules (Critical)

### 1) Never block on async

**Do not use** `.Result`, `.Wait()`, or `Task.Run` for IO-bound work.

Avoid:

```csharp
var user = _accountService.GetUserByNameAsync(name).Result;
```

Preferred:

```csharp
var user = await _accountService.GetUserByNameAsync(name);
```

### 2) Async all the way

If a service method calls async EF methods, the service method must be async and awaited by the controller.

### 3) Prefer EF Core async calls

Use `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `ToListAsync`, `AnyAsync`, `SaveChangesAsync`.

### 4) Parallelism with care

If you need multiple independent queries, use `Task.WhenAll`, but avoid excessive fan-out that floods the DB.

---

## EF Core & Data Access Style

### Query conventions

* Prefer expressing calculations in SQL when possible:

  * `AverageAsync`, `CountAsync`, `AnyAsync`, etc.
* Avoid N+1 query patterns (looping and querying DB each iteration).
* Prefer projection (`Select`) into ViewModels/DTOs where it makes sense.

### Entity design (current + refactor direction)

**Current code uses join tables** (e.g., `ImagesInUsers`, `CommentInImage`, `RatingsInImages`).
When refactoring, prefer simpler FK relationships unless you truly need many-to-many:

* `ImageModel` should have `UserId` (owner) if ownership is 1:N.
* `CommentModel` should have `ImageId` and `UserId`.
* `RatingModel` should have `(ImageId, UserId)` with unique constraint.

If you keep join tables, enforce consistency:

* Delete dependent rows when deleting parent rows (or configure cascades).
* Add indexes on foreign key columns.

---

## Controllers

### Keep controllers thin

Controllers should:

* Validate incoming model binding (`ModelState.IsValid`)
* Extract current user identity/claims
* Call a service
* Return `View(...)`, `RedirectToAction(...)`, or `IActionResult`/JSON

Controllers should not:

* Implement business rules
* Build complex multi-entity queries
* Call `.Result` on service tasks

### Claims usage

If you store user id in claims at login, prefer reading it from `HttpContext.User` rather than querying DB again.

Preferred:

```csharp
var userId = int.Parse(User.FindFirstValue("Id"));
```

---

## Validation & Errors

### Validation placement

* **Controller:** validate request shape (required fields, model binding).
* **Service:** validate business rules (ownership, entity existence, uniqueness rules).

### Error handling

* Avoid silent catches.
* Log unexpected exceptions in services (`ILogger<T>`).
* In services: either return a result type (preferred) or throw a meaningful exception.
* In controllers: map failures to user-friendly responses.

Recommended patterns:

* For MVC forms: return view with message.
* For API endpoints: return `BadRequest(...)`, `NotFound(...)`, `Unauthorized()`, etc.

---

## Logging

* Inject `ILogger<T>` into controllers/services.
* Log:

  * unexpected exceptions
  * security-related events (failed login attempts) **without sensitive data**
  * important actions (image deleted, rating updated), optionally at `Information`

Do not log:

* plaintext passwords
* password hashes/salts
* secrets / connection strings

---

## Security & Authentication Conventions

* Cookie auth middleware must be correctly configured:

  * `app.UseAuthentication();`
  * `app.UseAuthorization();`
  * in that order (after routing).

* Any action that modifies data should be protected:

  * `[Authorize]` on controller/action, plus service-level ownership checks.

---

## JavaScript & Frontend Conventions (Minimal)

* Prefer placing significant JS into `wwwroot/js/*.js` instead of embedding large blocks in `.cshtml`.
* When using AJAX endpoints:

  * keep URL paths consistent
  * return consistent JSON shapes for errors
  * handle failure cases (not only `console.error`)

---

## Documentation Rules (RAG)

When you introduce a new convention or refactor a major pattern:

* Update **this file** if it affects style rules.
* Update `docs/ARCHITECTURE.md` if it changes layering or data model.
* Update `docs/ANTI_PATTERNS.md` if you discover a new pitfall.

Docs are considered part of the deliverable.

---

## Quick Checklist Before Committing

* [ ] No `.Result` / `.Wait()` anywhere
* [ ] Controllers are thin (logic is in services)
* [ ] EF queries are async and not N+1
* [ ] Proper validation and clear error paths
* [ ] Logging added for unexpected failures
* [ ] Naming and formatting match this guide
* [ ] Docs updated if behavior/pattern changed
