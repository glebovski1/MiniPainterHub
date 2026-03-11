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
- `MiniPainterHub.WebApp.Tests`: bUnit component tests for Blazor UI behavior.

High-level runtime flow:

`Browser (Blazor WASM) -> API controllers -> Services -> AppDbContext (EF Core + Identity) -> SQL Server/InMemory`

## 2) Backend Composition (`MiniPainterHub.Server`)

### 2.1 Hosting and Pipeline

Primary entry point: `MiniPainterHub.Server/Program.cs`.

Configured middleware (in order):

1. Exception handling (`UseExceptionHandler`)
2. HTTPS redirection
3. Authentication (`UseAuthentication`)
4. Maintenance gate (`UseMiddleware<MaintenanceModeMiddleware>`)
5. Static file and Blazor framework files
6. Authorization (`UseAuthorization`)
7. API controller mapping (`MapControllers`)
8. SignalR hub mapping (`MapHub<ChatHub>("/hubs/chat")`)
9. SPA fallback (`MapFallbackToFile("index.html")`)
10. Health endpoint (`/healthz`)

Development-only:
- Swagger UI
- WebAssembly debugging
- Relational DB startup uses `Database.MigrateAsync()`, with guarded recovery for legacy local schemas where Identity tables already exist but migration history does not. When this specific conflict is detected, Development recreates the DB and reapplies migrations (`Database:RecreateOnSchemaConflict`, default `true`).
- `DataSeeder.SeedAsync(...)`
- Explicit maintenance commands in `Program.cs`:
  - `--seed-dev-content --avatars-dir <path>` resets the development DB/local image storage and recreates seeded users, profiles, posts, comments, and avatars through `DevelopmentContentSeeder`.
  - `--seed-dev-content --avatars-dir <path> --post-images-dir <path>` also attaches one seeded image per post; if fewer source files are provided than seeded posts, files are reused in sorted order.
  - `--generate-dev-avatars --avatars-dir <path>` refreshes only the seed avatar assets and existing seed-user avatar URLs without reseeding the rest of the database.
  - `DevelopmentContentSeeder` also seeds deterministic cross-user comments, follow relationships, and direct-message conversations so social features have usable development fixtures immediately after reset.

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
- `IAccountRestrictionService -> AccountRestrictionService`
- `IModerationService -> ModerationService`
- `ISearchService -> SearchService`
- `IReportService -> ReportService`
- `IFollowService -> FollowService`
- `IConversationService -> ConversationService`
- `IMaintenanceBypassService -> MaintenanceBypassService` (singleton, cookie issuance/validation)
- `DevelopmentContentSeeder` for explicit development-only sample content/avatar maintenance commands

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
- `Tags`
- `PostTags`
- `Comments`
- `Likes`
- `ModerationAuditLogs`
- `ContentReports`
- `Follows`
- `Conversations`
- `ConversationParticipants`
- `DirectMessages`

Entity relationships (configured in `OnModelCreating`):
- `Profile` one-to-one with `ApplicationUser` (shared PK/FK `UserId`).
- `Post` -> `ApplicationUser` (creator) with `DeleteBehavior.Restrict`.
- `PostImage` -> `Post` with cascade delete.
- `Tag` <-> `Post` through `PostTag` join table.
- `Comment` -> `Post` with cascade delete; `Comment` -> `Author` with restrict delete.
- `Like` -> `Post` with cascade delete; `Like` -> `User` with restrict delete.
- `ContentReport` stores moderation queue state for post/comment/user reports.
- `Follow` models the social graph between `ApplicationUser` rows.
- `Conversation`/`ConversationParticipant`/`DirectMessage` model direct messaging.

Soft-delete behavior:
- `Post.IsDeleted` and `Comment.IsDeleted` are used by services to hide deleted records.
- `DeleteAsync` in post/comment services marks records deleted instead of physical removal.
- Moderation adds `ModeratedByUserId`, `ModeratedUtc`, and `ModerationReason` to preserve audit context.

## 4) API Surface (Current Controllers)

Main controllers under `MiniPainterHub.Server/Controllers`:

- `AuthController` (`/api/auth`)
  - `POST /register`
  - `POST /login`
  - `POST /maintenance-bypass`
  - `DELETE /maintenance-bypass`
- `PostsController` (`/api/posts`)
  - `GET /`
    - Supports visibility query options for moderation roles: `includeDeleted` and `deletedOnly`.
  - `GET /{id}`
  - `POST /` (JSON post create)
  - `POST /with-image` (multipart upload)
  - `PUT /{id}`
  - `DELETE /{id}`
- `SearchController` (`/api/search`)
  - `GET /overview`
  - `GET /posts`
  - `GET /users`
  - `GET /tags`
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
  - `GET /{id}` (allow anonymous; public profile read)
- `ModerationController` (`/api/moderation`)
  - `POST /posts/{postId}/hide`
  - `POST /posts/{postId}/restore`
  - `POST /comments/{commentId}/hide`
  - `POST /comments/{commentId}/restore`
  - `POST /users/{userId}/suspend`
  - `POST /users/{userId}/unsuspend`
  - `GET /audit`
  - `GET /users/lookup`
  - `GET /posts/{postId}/preview`
  - `GET /comments/{commentId}/preview`
- `ReportsController` (`/api/reports`)
  - `POST /posts/{postId}`
  - `POST /comments/{commentId}`
  - `POST /users/{userId}`
  - `GET /`
  - `POST /{reportId}/resolve`
- `FollowsController` and `FeedController` for social graph and following feed
- `ConversationsController` for direct messaging

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

Service clients in `MiniPainterHub.WebApp/Services` mirror server domains (`AuthService`, `PostService`, `CommentService`, `LikeService`, `ProfileService`, `SearchService`, `ReportService`, `FollowService`, `ConversationService`).
Service clients also include moderation flows (`ModerationService`) and keep query DTO/filter parity with the server API.

Admin UX composition:
- Left collapsible user panel (`Layout/MainLayout.razor` + `Shared/UserPanelContent.razor`) is the primary entry for admin actions.
- `Moderator` and `Admin` see moderation + audit links.
- `Moderator` and `Admin` also see the reports queue.
- `Admin` additionally sees user suspension tools.
- Admin pages currently live under:
  - `Pages/Admin/ModerationDashboard.razor`
  - `Pages/Admin/AuditLog.razor`
  - `Pages/Admin/Reports.razor`
  - `Pages/Admin/UserSuspensions.razor`

Search and reporting UX composition:
- Global nav search routes into `Pages/Search.razor`.
- Post cards/details render technique tags through `Shared/TagBadgeList.razor`.
- Posts, comments, and public profiles expose inline reporting through `Shared/ReportAction.razor`.

## 8) Testing Strategy

Server tests are in `MiniPainterHub.Server.Tests` and cover services/controllers.
Blazor component tests are in `MiniPainterHub.WebApp.Tests`.

Preferred validation commands:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj`

When adding tests with EF InMemory, seed realistic relational data (for example all referenced users for likes/comments) to avoid impossible production states.
