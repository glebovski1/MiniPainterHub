# MiniPainterHub Architecture

This document is the architecture source of truth for the current `MiniPainterHub` codebase.
If implementation and this file diverge, update this file as part of the same change.

Related docs:
- [CODE_STYLE.md](CODE_STYLE.md)
- [BEST_PRACTICES.md](<../30 Process/BEST_PRACTICES.md>)
- [ANTI_PATTERNS.md](<../30 Process/ANTI_PATTERNS.md>)
- [CONTRIBUTING.md](<../30 Process/CONTRIBUTING.md>)

## 1) Solution Overview

`MiniPainterHub` is a .NET 10 solution with:

- `MiniPainterHub.Server`: ASP.NET Core backend API + static host for Blazor WebAssembly.
- `MiniPainterHub.WebApp`: Blazor WebAssembly client.
- `MiniPainterHub.Common`: shared DTO/auth contracts used by server and client.
- `MiniPainterHub.Server.Tests`: server unit/integration-style tests.
- `MiniPainterHub.WebApp.Tests`: bUnit component tests for Blazor UI behavior.
- `MiniPainterHub.LoadTests`: NBomber API/load smoke and event-spike scenarios.

High-level runtime flow:

`Browser (Blazor WASM) -> API controllers -> Services -> AppDbContext (EF Core + Identity) -> SQL Server/InMemory`

## 2) Backend Composition (`MiniPainterHub.Server`)

### 2.1 Hosting and Pipeline

Primary entry point: `MiniPainterHub.Server/Program.cs`.

`Program.cs` is the composition orchestrator only. Startup implementation is split under
`MiniPainterHub.Server/Infrastructure/Startup`:
- `ServiceRegistration.cs` owns framework, auth, image infrastructure, and domain service registration.
- `ApplicationStartup.cs` owns local/production database startup and seeding.
- `HttpPipeline.cs` owns middleware ordering, static files, endpoint mapping, and test-support reset routing.

Configured middleware (in order):

1. Exception handling (`UseExceptionHandler`)
2. HTTPS redirection
3. Routing (`UseRouting`)
4. Authentication (`UseAuthentication`)
5. Rate limiting (`UseRateLimiter`)
6. Maintenance gate (`UseMiddleware<MaintenanceModeMiddleware>`)
7. Static file and Blazor framework files
8. Authorization (`UseAuthorization`)
9. API controller mapping (`MapControllers`)
10. SignalR hub mapping (`MapHub<ChatHub>("/hubs/chat")`)
11. SPA fallback (`MapFallbackToFile("index.html")`)
12. Health endpoints (`/healthz`, `/healthz/live`, `/healthz/ready`)

Development-only:
- Swagger UI
- WebAssembly debugging
- Relational DB startup uses `Database.MigrateAsync()`, with guarded recovery for legacy local schemas where Identity tables already exist but migration history does not. When this specific conflict is detected, Development recreates the DB and reapplies migrations (`Database:RecreateOnSchemaConflict`, default `true`).
- `DataSeeder.SeedAsync(...)`
- Explicit maintenance commands in `Program.cs`:
  - `--seed-dev-content --avatars-dir <path>` resets the development DB/local image storage and recreates seeded users, profiles, posts, comments, and avatars through `DevelopmentContentSeeder`.
- `--seed-dev-content --avatars-dir <path> --post-images-dir <path>` attaches images only to seed posts that declare an explicit source filename. The current five title-to-file mappings are deterministic, unmatched posts remain image-free, and startup fails before the reset when a declared file is missing.
  - `--generate-dev-avatars --avatars-dir <path>` refreshes only the seed avatar assets and existing seed-user avatar URLs without reseeding the rest of the database.
  - `DevelopmentContentSeeder` also seeds deterministic cross-user comments, follow relationships, and direct-message conversations so social features have usable development fixtures immediately after reset.

Production-only:
- HSTS
- Pending EF migrations fail startup unless `Database:AutoMigrateOnStartup=true` is explicitly enabled for an emergency single-instance rollout. Normal production migration application belongs to the deployment workflow before the app starts.
- `DataSeeder.SeedAdminAsync(...)`

### 2.2 Dependency Injection and Service Layer

Business logic is being modularized as a monolith. Legacy service entry points remain in
`MiniPainterHub.Server/Services` behind interfaces in `Services/Interfaces`, while extracted
feature-owned helpers live under `MiniPainterHub.Server/Features`.

Current extracted backend feature modules:
- `Features/Tags`: shared tag text normalization, slug creation, and unique slug resolution for posts, search, and seeders.
- `Features/Media`: post image upload limits and validation.
- `Features/Pagination`: shared page/page-size validation for public list endpoints (`MaxPageSize = 100`).
- `Features/Posts`: post DTO/projection mapping.

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
- `IPaintingGuideService -> PaintingGuideService`
- `IMaintenanceBypassService -> MaintenanceBypassService` (singleton, cookie issuance/validation)
- `DevelopmentContentSeeder` for explicit development-only sample content/avatar maintenance commands

Image infrastructure:
- `IImageProcessor -> ImageProcessor`
- `IImageService` + `IImageStore`:
  - Development: `LocalImageService`
  - Non-development: `AzureBlobImageService`
- `IUploadConcurrencyLimiter -> UploadConcurrencyLimiter` gates expensive upload/image-processing work per user/IP and globally.

Traffic shaping:
- `TrafficShapingOptions` configures fixed-window rate limits for auth, search, write, upload, and realtime handshake traffic.
- Rate-limited endpoints return HTTP `429` with `ProblemDetails` and `Retry-After` when the runtime exposes a retry value.
- Auth uses both the auth rate-limit policy and Identity lockout (`5` failed attempts for `15` minutes by default).

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
- `PaintingGuides`
- `PaintingGuideSteps`
- `NewsAnnouncements`
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
- `Post` stores optional paint recipe metadata (`MiniatureName`, `PaintsUsed`, `Techniques`, `Difficulty`, `TimeSpent`) that is surfaced on post create, feed cards, details, and viewer info.
- `PostImage` -> `Post` with cascade delete.
- `PaintingGuide` -> `ApplicationUser` (creator) with restrict delete; `PaintingGuideStep` -> `PaintingGuide` with cascade delete and a unique guide/order pair.
- `NewsAnnouncement` -> `ApplicationUser` (admin author) with restrict delete.
- `Tag` <-> `Post` through `PostTag` join table.
- `Comment` -> `Post` with cascade delete; `Comment` -> `Author` with restrict delete.
- `Like` -> `Post` with cascade delete; `Like` -> `User` with restrict delete.
- `ContentReport` stores moderation queue state for post/comment/user reports.
- `Follow` models the social graph between `ApplicationUser` rows.
- `Conversation`/`ConversationParticipant`/`DirectMessage` model direct messaging.
- `Like` has a database-level unique index on `(PostId, UserId)`; service code treats duplicate insert races as an already-liked outcome.
- Direct one-to-one conversations store a deterministic `DirectConversationKey` with a filtered unique index. The key is generated from the sorted participant IDs so reverse-order conversation creation resolves to the same thread.

Soft-delete behavior:
- `Post.IsDeleted` and `Comment.IsDeleted` are used by services to hide deleted records.
- `DeleteAsync` in post/comment services marks records deleted instead of physical removal.
- Post deletion also best-effort removes image artifacts when the post image row contains local/blob storage metadata.
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
- `PaintingGuidesController` (`/api/guides`) for public guide browsing and authenticated guide creation
  - `GET /`
  - `GET /{id}`
  - `POST /` (JSON guide create)
  - `POST /with-step-photos` (multipart guide create with optional per-step photos)
- `NewsAnnouncementsController` (`/api/news`) for public announcements and admin-authored updates
  - `GET /`
  - `GET /{id}`
  - `POST /` (admin-only JSON create)

## 5) Image Processing and Storage

Post image uploads:
- Entry: `PostsController.CreateWithImage([FromForm] CreateImagePostDto dto)`
- Service: `PostService.CreateWithImagesAsync(...)`
- Limits include max images per post, aggregate multipart file bytes, request/form size limits, and legacy-thumbnail rejection when the image-processing pipeline is enabled. Shared product limits live in `PostImageUploadRules`; server validation is centralized in `Features/Media/PostImageUploadValidator`.

Two storage paths:
- Legacy direct upload path through `IImageService.UploadAsync(...)`.
- Processing pipeline path through:
  - `IImageProcessor.ProcessAsync(...)` (variant generation)
  - `IImageStore.SaveAsync(...)` (persist variants)

All post upload paths validate supported image MIME types before storage. The legacy direct upload path uses server-generated storage keys and rejects client filenames that contain path separators, rooted paths, or parent-directory segments.

Posts can also carry optional paint recipe metadata. JSON and multipart create flows share the same recipe fields, and post detail/summary DTOs expose the data so the client can render recipe context without a second request.

Painting guides are long-form user-authored walkthroughs. Each guide has ordered steps with descriptions, optional paint/color notes, optional technique notes, and optional step photos. Multipart guide creation sends photo-index pairs so a photo can attach to a specific step without requiring every step to have an image.
Guide photo limits are shared through `GuidePhotoUploadRules` and enforced with request/form size limits, total file-byte validation, source image dimension checks, and upload concurrency gates.

Post projection mapping is centralized in `Features/Posts/PostDtoMapper` so query and detail
responses share thumbnail fallback and tag ordering behavior.

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
- JWT token access is centralized behind `MiniPainterHub.WebApp/Services/Auth/ITokenStore.cs`.
- `LocalStorageTokenStore` is the browser `localStorage` implementation for the existing `authToken` key.
- `Services/Http/ApiClient.cs` attaches bearer tokens to outbound API requests when the request does not already provide an `Authorization` header.
- API calls go through `Services/Http/ApiClient.cs` for:
  - common request logic
  - standardized error parsing
  - notification handling

Service clients in `MiniPainterHub.WebApp/Services` mirror server domains (`AuthService`, `PostService`, `PaintingGuideService`, `NewsAnnouncementService`, `CommentService`, `LikeService`, `ProfileService`, `SearchService`, `ReportService`, `FollowService`, `ConversationService`).
Service clients also include moderation flows (`ModerationService`) and keep query DTO/filter parity with the server API.

Guide UX composition:
- `Pages/Guides/GuideList.razor` lists public painting guides.
- `Pages/Guides/CreateGuide.razor` lets authenticated users create ordered guide steps with optional photos.
- `Pages/Guides/GuideDetails.razor` renders the walkthrough, color notes, technique notes, and attached images.

News UX composition:
- `Pages/News/NewsList.razor` and `Pages/News/NewsDetails.razor` expose public announcements.
- `Pages/Admin/CreateNewsAnnouncement.razor` is the admin-authored announcement composer.

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
- `dotnet run --project MiniPainterHub.LoadTests/MiniPainterHub.LoadTests.csproj -- --profile smoke --base-url <ready server URL>`

When adding tests with EF InMemory, seed realistic relational data (for example all referenced users for likes/comments) to avoid impossible production states.
