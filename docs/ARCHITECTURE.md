# MiniPainterHub Architecture

This document is the architecture source of truth for the current `MiniPainterHub` codebase.
If implementation and this file diverge, update this file as part of the same change.

Related docs:
- `docs/CODE_STYLE.md`
- `docs/BEST_PRACTICES.md`
- `docs/ANTI_PATTERNS.md`
- `docs/CONTRIBUTING.md`

## 1) Solution Overview

`MiniPainterHub` is a .NET 8 solution with:

- `MiniPainterHub.Server`: ASP.NET Core backend API + static host for Blazor WebAssembly.
- `MiniPainterHub.WebApp`: Blazor WebAssembly client.
- `MiniPainterHub.Common`: shared DTO/auth contracts used by server and client.
- `MiniPainterHub.Server.Tests`: server unit/integration-style tests.

High-level runtime flow:

`Browser (Blazor WASM) -> API controllers -> Services -> AppDbContext (EF Core + Identity) -> SQL Server/InMemory`

## 2) Backend Composition (`MiniPainterHub.Server`)

### 2.1 Hosting and Pipeline

Primary entry point: `MiniPainterHub.Server/Program.cs`.

Configured middleware (in order):

1. Exception handling (`UseExceptionHandler`)
2. HTTPS redirection
3. Static file and Blazor framework files
4. Authentication (`UseAuthentication`)
5. Authorization (`UseAuthorization`)
6. API controller mapping (`MapControllers`)
7. SPA fallback (`MapFallbackToFile("index.html")`)
8. Health endpoint (`/healthz`)

Development-only:
- Swagger UI
- WebAssembly debugging
- `DataSeeder.SeedAsync(...)`

Production-only:
- HSTS
- `Database.MigrateAsync()`
- `DataSeeder.SeedAdminAsync(...)`

### 2.2 Dependency Injection and Service Layer

Business logic lives in `MiniPainterHub.Server/Services` behind interfaces in `Services/Interfaces`.

Registered scoped domain services:
- `IProfileService -> ProfileService`
- `IPostService -> PostService`
- `ICommentService -> CommentService`
- `ILikeService -> LikeService`

Image infrastructure:
- `IImageProcessor -> ImageProcessor`
- `IImageService` + `IImageStore`:
  - Development: `LocalImageService`
  - Non-development: `AzureBlobImageService`

### 2.3 Authentication and Authorization

Authentication model:
- ASP.NET Core Identity (`ApplicationUser`) backed by `AppDbContext`.
- JWT bearer authentication (`AddAuthentication().AddJwtBearer(...)`).
- JWT issued by `AuthController` on successful login.

Authorization model:
- Controllers are typically `[Authorize]` with `[AllowAnonymous]` on selected read endpoints.
- User identity is read from claims (`ClaimTypes.NameIdentifier` or helper methods in `Server/Identity`).

## 3) Data Model and Persistence

DbContext:
- `MiniPainterHub.Server/Data/AppDbContext.cs`
- Inherits `IdentityDbContext<ApplicationUser>`

Domain DbSets:
- `Profiles`
- `Posts`
- `PostImages`
- `Comments`
- `Likes`

Entity relationships (configured in `OnModelCreating`):
- `Profile` one-to-one with `ApplicationUser` (shared PK/FK `UserId`).
- `Post` -> `ApplicationUser` (creator) with `DeleteBehavior.Restrict`.
- `PostImage` -> `Post` with cascade delete.
- `Comment` -> `Post` with cascade delete; `Comment` -> `Author` with restrict delete.
- `Like` -> `Post` with cascade delete; `Like` -> `User` with restrict delete.

Soft-delete behavior:
- `Post.IsDeleted` and `Comment.IsDeleted` are used by services to hide deleted records.
- `DeleteAsync` in post/comment services marks records deleted instead of physical removal.

## 4) API Surface (Current Controllers)

Main controllers under `MiniPainterHub.Server/Controllers`:

- `AuthController` (`/api/auth`)
  - `POST /register`
  - `POST /login`
- `PostsController` (`/api/posts`)
  - `GET /`
  - `GET /{id}`
  - `POST /` (JSON post create)
  - `POST /with-image` (multipart upload)
  - `PUT /{id}`
  - `DELETE /{id}`
- `CommentsController`
  - `GET /api/posts/{postId}/comments`
  - `POST /api/posts/{postId}/comments`
  - `GET /api/comments/{id}`
  - `PUT /api/comments/{id}`
  - `DELETE /api/comments/{id}`
- `LikesController` (`/api/posts/{postId}/likes`)
  - `GET /`
  - `POST /`
  - `DELETE /`
- `ProfilesController` (`/api/profiles`)
  - `GET /me`
  - `POST /me`
  - `PUT /me`
  - `POST /me/avatar`
  - `GET /{id}`

## 5) Image Processing and Storage

Post image uploads:
- Entry: `PostsController.CreateWithImage([FromForm] CreateImagePostDto dto)`
- Service: `PostService.CreateWithImagesAsync(...)`
- Limits include max images per post and max upload size.

Two storage paths:
- Legacy direct upload path through `IImageService.UploadAsync(...)`.
- Processing pipeline path through:
  - `IImageProcessor.ProcessAsync(...)` (variant generation)
  - `IImageStore.SaveAsync(...)` (persist variants)

Profile avatar uploads:
- Entry: `ProfilesController.UploadAvatar`
- Service: `ProfileService.UploadAvatarAsync`
- Validates size/content type, resizes to bounded dimensions, stores via `IImageService`.

## 6) Error Handling

Global error handling is centralized with:
- `AddProblemDetails(...)`
- `AddExceptionHandler<ValidationExceptionHandler>()`
- `AddExceptionHandler<GlobalExceptionHandler>()`

Domain/service exceptions (for example `NotFoundException`, `DomainValidationException`) are translated into HTTP `ProblemDetails` responses.

## 7) Client Architecture (`MiniPainterHub.WebApp`)

The WebApp is a Blazor WebAssembly SPA:

- `Program.cs` registers auth state and typed services.
- JWT token is stored in browser `localStorage`.
- `JwtAuthorizationMessageHandler` injects bearer token into outbound API requests.
- API calls go through `Services/Http/ApiClient.cs` for:
  - common request logic
  - standardized error parsing
  - notification handling

Service clients in `MiniPainterHub.WebApp/Services` mirror server domains (`AuthService`, `PostService`, `CommentService`, `LikeService`, `ProfileService`).

## 8) Testing Strategy

Server tests are in `MiniPainterHub.Server.Tests` and cover services/controllers.

Preferred validation commands:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

When adding tests with EF InMemory, seed realistic relational data (for example all referenced users for likes/comments) to avoid impossible production states.
