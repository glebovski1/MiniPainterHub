# HobbyCenter Architecture (ARCHITECTURE.md)

This document describes the current architecture of the HobbyCenter ASP.NET Core MVC application and the intended direction for refactoring.
It is part of the repo’s in-repo RAG knowledge base and should be used as the source of truth for how the codebase is organized.

Related docs:

* `docs/CODE_STYLE.md` — naming, formatting, async rules
* `docs/BEST_PRACTICES.md` — preferred implementation patterns
* `docs/ANTI_PATTERNS.md` — what to avoid

---

## 1) System Overview

HobbyCenter is an ASP.NET Core MVC application where users can:

* Create accounts and log in (cookie-based auth)
* Upload images (stored in the database in current implementation)
* View images and details
* Add comments to images
* Rate images (with per-user rating behavior)

The project follows a classic layered structure:

**HTTP → Controllers → Services → EF Core DbContext → Database**

Controllers handle HTTP concerns, services handle business logic and data operations, and EF Core handles persistence.

---

## 2) High-Level Layers

### 2.1 Presentation Layer (MVC + API)

**Controllers** live in `Controllers/` and handle:

* routing and action methods
* model binding + validation (`ModelState`)
* view rendering (`View(...)`)
* redirects (`RedirectToAction(...)`)
* API responses (JSON via `ApiController` where used)

The codebase may include both:

* Standard MVC controllers returning views
* API controllers returning JSON (used for AJAX features like comments/ratings)

**Rule:** Controllers should remain thin; business rules live in services.

### 2.2 Business Logic Layer (Services)

**Services** live in `Services/` and implement:

* data retrieval and transformations
* creation/update/delete workflows
* business rules (ownership, uniqueness, rating rules)
* cross-entity operations (e.g., delete image + related records)

Services depend on:

* EF Core `HobbyCenterContext`
* other services (when needed)
* logging (`ILogger<T>` recommended)

### 2.3 Data Access Layer (EF Core)

The EF Core database context is `HobbyCenterContext` (in `Data/`).

It provides `DbSet<T>` collections for entities like:

* Users
* Images
* Comments
* Ratings
* Link/join tables (see below)

Entities live in `Models/`.

---

## 3) Current Data Model (As Implemented Today)

### 3.1 Core Entities

* **UserModel**

  * Stores username and password hash/salt.
* **ImageModel**

  * Stores image binary data (`byte[]`), name, description.
* **CommentModel**

  * Stores comment text and author user id.
* **Rating**

  * Stores numeric rating value and rater user id.

### 3.2 Link / Join Tables

The current implementation uses explicit join/link entities:

* **ImagesInUsers**

  * Links image ownership: (`UserId`, `ImageId`)
* **CommentInImage**

  * Links a comment to an image: (`CommentId`, `ImageId`)
* **RatingsInImages**

  * Links a rating record to an image: (`RatingId`, `ImageId`)

This approach works but increases complexity and query count (manual relationship orchestration).

> **Important:** Because these link tables exist, services must be careful to select correct FK columns (e.g., `ImageId`, not the link row’s own `Id`) and to delete dependent records when removing parents.

---

## 4) Request / Data Flow

### 4.1 Typical Read Flow (View Image Details)

1. Browser requests an image page: `GET /Image/Image/{id}`
2. Controller action receives `id`, calls service:

   * `ImageService.GetImageByIdAsync(id)` (or similar)
3. Service queries `Images` table for the image
4. Service builds `ImageViewModel`:

   * poster/owner username (via link table → user)
   * average rating (via ratings table + link table)
   * comments (via comment link table + comment table)
5. Controller returns view `Views/Image/Image.cshtml` with the view model

### 4.2 Typical Write Flow (Add Comment via AJAX)

1. Browser sends AJAX request: `POST /api/Comment/AddCommentToImage`
2. API controller validates request + `[Authorize]`
3. Controller extracts current user id from claims
4. Calls `CommentService.AddCommentToImageAsync(userId, imageId, text)`
5. Service:

   * validates input
   * verifies image exists (recommended)
   * inserts `CommentModel`
   * inserts `CommentInImage` row linking comment to image
6. API returns success response
7. UI reloads comments list via `GET /api/Comment/CommentsOfImage/{imageId}`

---

## 5) Authentication & Authorization

### 5.1 Cookie Authentication

The app uses cookie authentication:

* Login creates a `ClaimsPrincipal` and calls `SignInAsync`
* The cookie is stored in the browser
* Each request should be authenticated by middleware

### 5.2 Critical Pipeline Requirement

For `[Authorize]` to work correctly, middleware must include:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

Missing `UseAuthentication()` breaks authentication. This is a known pitfall (see `docs/ANTI_PATTERNS.md`).

### 5.3 Claims Usage

On login, store at minimum:

* username
* user id (as claim `"Id"` or standard `ClaimTypes.NameIdentifier`)

Preferred in controller:

* read user id from claims
* avoid extra DB call when possible

Example:

```csharp
var userId = int.Parse(User.FindFirstValue("Id"));
```

Services should still validate ownership/permissions even if UI hides buttons.

---

## 6) ViewModels

ViewModels live in `ViewModels/` and exist to shape data for UI/API use.

Examples:

* `ImageViewModel` includes:

  * image info
  * owner username
  * `CanDelete` (UI flag)
  * average rating
  * comments list
* `CommentViewModel` includes:

  * comment text
  * author username
  * timestamp (if stored)
* `StarRatingViewModel` may include:

  * current user rating
  * average rating
  * rating scale

**Rule:** ViewModels should not expose password hashes/salts or internal DB-only details.

---

## 7) Known Architectural Risks / Technical Debt

These are areas to keep in mind when enhancing/refactoring:

### 7.1 Async Mixed With Sync Blocking

Use of `.Result` inside services/controllers causes deadlocks and performance issues.
Refactor toward `async/await` everywhere.

### 7.2 Link Table Complexity

Join-table approach increases risk of:

* wrong column selection bugs
* orphan record accumulation
* extra query roundtrips

### 7.3 Delete Integrity

Deleting images should also delete:

* related join rows
* related comments/ratings (depending on design rules)

Without cascades or manual cleanup, orphan records remain.

### 7.4 Average Rating Edge Case

Computing average ratings must handle the “no ratings yet” case (avoid divide-by-zero).

### 7.5 Storage Scalability

Storing image blobs in DB is simple but does not scale well.
Future enhancement may move to file/blob storage.

---

## 8) Refactor Direction (Recommended)

This section defines the preferred direction for future refactoring. You can do it gradually.

### 8.1 Simplify Relationships With Foreign Keys

Replace link tables where they represent 1:N relationships:

* `ImageModel`

  * add `OwnerUserId`
* `CommentModel`

  * add `ImageId`
  * keep `UserId`
* `RatingModel`

  * add `ImageId`
  * keep `UserId`
  * enforce unique `(UserId, ImageId)` (one rating per user per image)

This eliminates:

* `ImagesInUsers`
* `CommentInImage`
* `RatingsInImages`
  (assuming no many-to-many requirement remains)

### 8.2 Make Services the Single Source of Business Rules

Move all shared logic into services so:

* MVC controllers and API controllers call the same service methods
* no duplicated business logic exists in different controllers

### 8.3 Adopt Migrations (If Not Already)

If the project currently relies on `EnsureCreated()`, consider switching to migrations for controlled schema changes, especially when refactoring relationships.

---

## 9) Repository “RAG” Documentation Map

These docs are part of the intended architecture:

* `docs/CODE_STYLE.md` — how to write code (style + async rules)
* `docs/BEST_PRACTICES.md` — where code belongs (controllers vs services), validation, EF usage
* `docs/ANTI_PATTERNS.md` — known pitfalls and what to avoid
* `docs/ARCHITECTURE.md` — this document
* `docs/CONTRIBUTING.md` — contribution workflow and expectations

When architectural decisions change, update this file first.

---

## 10) Quick “Where Do I Put This?” Guide

* New DB table/entity → `Models/` + `HobbyCenterContext`
* New view/page → `Controllers/` + `Views/` + `ViewModels/`
* New business rule/workflow → `Services/`
* Shared helper function → `Utils/` (only if truly generic)
* Rule/style change → update `docs/*`

---

## 11) Glossary

* **MVC**: Model-View-Controller pattern used for server-side HTML pages.
* **Service layer**: Business logic layer that sits between controllers and EF Core.
* **EF Core**: ORM used to map C# models to database tables.
* **Join/link table**: A table that links two entities by storing their IDs.
* **ClaimsPrincipal**: Authenticated identity stored in `HttpContext.User`.

---
