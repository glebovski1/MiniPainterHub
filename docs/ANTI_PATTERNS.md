# Common Anti-Patterns (ANTI_PATTERNS.md)

This document lists known pitfalls and “do not do this” patterns for HobbyCenter.
It exists to prevent repeated bugs and keep the codebase consistent during enhancements/refactors.

If you see these patterns, treat them as **refactor targets**.

Related docs:

* `docs/CODE_STYLE.md` (style + async rules)
* `docs/BEST_PRACTICES.md` (preferred patterns)
* `docs/ARCHITECTURE.md` (layering and data flow)

---

## 1) Blocking Async Code (`.Result`, `.Wait()`)

### What it looks like

```csharp
var user = _accountService.GetUserByNameAsync(name).Result;
```

### Why it’s bad

* Can deadlock or starve the thread pool.
* Defeats the purpose of async/await (scalability and responsiveness).
* Hides real exceptions or changes behavior under load.

### Do this instead

Make the caller async and `await`:

```csharp
var user = await _accountService.GetUserByNameAsync(name);
```

**Rule:** async all the way from controller → service → EF Core.

---

## 2) Forgetting `app.UseAuthentication()` (Auth Middleware Missing)

### What it looks like

* Cookie auth is configured in `Program.cs` via `AddAuthentication().AddCookie(...)`,
  but the request pipeline has only:

```csharp
app.UseAuthorization();
```

### Why it’s bad

* Auth cookie is never validated on incoming requests.
* `HttpContext.User` may not be populated.
* `[Authorize]` may not work as expected.
* Security can silently break.

### Do this instead

Always include middleware in the correct order:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

---

## 3) Business Logic in Controllers (“Fat Controllers”)

### What it looks like

* Controllers query EF directly.
* Controllers implement ownership rules, rating rules, or “exists” checks.
* Controllers contain loops with DB queries.

### Why it’s bad

* Hard to test and reuse.
* Duplicates logic across MVC and API controllers.
* Makes refactoring risky and messy.

### Do this instead

* Keep controllers thin: bind/validate input, call service, return response.
* Put business rules in services (`Services/`).

---

## 4) Swallowing Exceptions (Silent Catch Blocks)

### What it looks like

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch
{
    // nothing
}
```

### Why it’s bad

* Failures become invisible.
* Data can end up in partial/inconsistent state.
* Debugging becomes painful.

### Do this instead

* Log exceptions (inject `ILogger<T>`).
* Return consistent failure results or throw meaningful exceptions.

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to save changes in AddRatingToImageAsync");
    return false;
}
```

---

## 5) N+1 Queries (Looping + DB Query per Item)

### What it looks like

```csharp
foreach (var comment in comments)
{
    var user = await _context.Users.FirstAsync(u => u.Id == comment.UserId);
}
```

### Why it’s bad

* Explodes DB calls (slow, expensive).
* Gets worse as data grows.

### Do this instead

* Use projection joins.
* Load all needed entities in one query.
* Use navigation properties + `Include` carefully.
* Or load users in bulk using `.Where(u => ids.Contains(u.Id))`.

---

## 6) Manual Relationship Orchestration Without Integrity

### What it looks like

* Insert an entity, call `SaveChangesAsync()`, then insert join-table records and call `SaveChangesAsync()` again.
* Delete an image but forget to delete related join records (orphan records).

### Why it’s bad

* Partial saves create inconsistent DB state.
* Orphan join records accumulate.
* Bugs are easy (wrong ID mapped, wrong join selected).

### Do this instead

* Prefer FK relationships (refactor direction).
* If join tables remain, enforce integrity:

  * use transactions for multi-step operations
  * configure cascade deletes or manually delete related rows
  * add proper indexes and constraints

---

## 7) Divide-by-Zero / Missing Edge Case Guards

### What it looks like

* Average rating computed like:

```csharp
return sum / ratings.Count;
```

### Why it’s bad

* Crashes when `ratings.Count == 0`.
* Causes errors on brand-new images.

### Do this instead

Guard empty collections or use SQL average with default:

```csharp
var avg = await _context.Ratings
    .Where(r => r.ImageId == imageId)
    .Select(r => (double?)r.Value)
    .AverageAsync();

return avg ?? 0.0;
```

---

## 8) Using Link Table Primary Keys Instead of Foreign Keys (Wrong Column Bugs)

### What it looks like

* Querying join-table IDs and treating them as image IDs or comment IDs.

Example mistake pattern:

```csharp
var imageIds = await _context.ImagesInUsers
    .Where(x => x.UserId == userId)
    .Select(x => x.Id) // wrong: should be x.ImageId
    .ToListAsync();
```

### Why it’s bad

* Returns incorrect results or empty sets.
* Hard to spot because it compiles and runs.

### Do this instead

Always select the actual FK:

```csharp
.Select(x => x.ImageId)
```

---

## 9) Duplicated Business Logic Across MVC and API

### What it looks like

* MVC controller and API controller both implement “add comment” but with slightly different checks and behaviors.

### Why it’s bad

* Rules drift over time.
* Fixing a bug in one path doesn’t fix the other.
* Harder to maintain.

### Do this instead

Both controllers should call the same service method. The service owns:

* validation
* authorization checks
* DB logic

Controllers only handle response format (View vs JSON).

---

## 10) Hardcoded Configuration / Secrets

### What it looks like

* Connection strings, base URLs, magic constants inside code.

### Why it’s bad

* Not environment-friendly (dev/stage/prod).
* Security risk if secrets end up in Git.
* Hard to change safely.

### Do this instead

* Use `appsettings.json` + environment overrides.
* Use secrets manager / environment variables for sensitive values.

---

## 11) Storing Large Binary Blobs in DB Without Limits (Images)

### What it looks like

* Images stored as `byte[]` in the database with no size constraints or performance considerations.

### Why it’s bad

* DB grows quickly; backups become heavy.
* Performance can degrade with many images.
* Transfer cost is higher for pages that list images.

### Do this instead (future enhancement)

* Store images in file storage / blob storage.
* Store only metadata + URL/path in DB.
* Add upload size limits and validation.

(For a hobby project, DB storage can be acceptable—just be aware of growth.)

---

## 12) UI-Only Security (“Button hidden so it’s safe”)

### What it looks like

* UI hides delete buttons, but server doesn’t enforce ownership.
* Relying on `CanDelete` to prevent deletes.

### Why it’s bad

* Anyone can craft an HTTP request to your endpoint.
* Security must be enforced server-side.

### Do this instead

* Use `[Authorize]` on write actions.
* Verify ownership in service before any delete/update.

---

## Quick Anti-Pattern Scan Checklist

Before merging a change, confirm:

* [ ] No `.Result` / `.Wait()`
* [ ] `UseAuthentication()` exists and is ordered correctly
* [ ] Controllers don’t contain business logic
* [ ] No silent catch blocks
* [ ] No N+1 DB query loops
* [ ] Deletes don’t leave orphan records
* [ ] Average computations handle empty sets
* [ ] Shared rules live in services (not duplicated across controllers)
