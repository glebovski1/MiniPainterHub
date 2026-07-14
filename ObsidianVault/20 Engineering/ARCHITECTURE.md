# MiniPainterHub Architecture

This document is the architecture source of truth for the current `MiniPainterHub` codebase.
If implementation and this file diverge, update this file as part of the same change.

Purpose: own the implemented solution structure, runtime boundaries, persistence model, API surface, and client composition.

When to read: architecture changes, cross-cutting feature work, persistence or API design, deployment-impact review, and test planning.

Update triggers: entities or relationships change, APIs or services are added or removed, startup/runtime composition changes, or a major client workflow is introduced.

Related notes:
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
- `DevelopmentContentSeeder` also seeds deterministic cross-user comments, follow relationships, direct-message conversations, and three projects built from existing seeded posts: active army and terrain diaries plus a completed army showcase. Project fixtures do not duplicate post content or image rows.

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
- `IHobbyProjectService -> HobbyProjectService` with the same scoped implementation exposed as `IHobbyProjectPostLinker` for atomic post/project creation without circular service dependencies
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

Hobby Project responsibilities stay behind two interfaces:
- `IHobbyProjectService` owns public and owner projections, metadata and status changes, archive/restore, existing-post linking and confirmed moves, milestone changes, showcase ordering, cover selection, and project limits.
- `IHobbyProjectPostLinker` is the narrow integration boundary used by `PostService` to validate a selected project before image processing and stage the post plus project entry in the same EF unit of work. Its rollback hook restores completed-project lifecycle state when later image storage fails.

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
- `IJwtTokenIssuer` issues the same role-bearing application JWT after password or external authentication.
- Google uses an explicit ASP.NET Core external-provider challenge while JWT bearer remains the default API authentication and challenge scheme.
- Provider identities are stored through Identity's `AspNetUserLogins`; Google `sub` is the durable key and email is used only for collision detection and explicit same-account linking.
- The OAuth callback creates a short-lived, hashed, single-use `ExternalAuthExchange`. Its raw handle is held only in an HttpOnly cookie and is exchanged by the SPA for the ordinary application JWT.
- New Google users choose a MiniPainterHub username before atomic user/profile/login creation. Matching email never auto-merges accounts; an authenticated user must explicitly connect Google.
- Authenticated account security supports adding a password and disconnecting Google only when another sign-in method remains.

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
- `SupportTickets`
- `SupportTicketMessages`
- `ExternalAuthExchanges`
- `HobbyProjects`
- `HobbyProjectEntries`

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
- `SupportTicket` -> `ApplicationUser` (requester) with restrict delete; `SupportTicketMessage` -> `SupportTicket` with cascade delete and -> `ApplicationUser` (author) with restrict delete.
- Support tickets store category, workflow status, activity/resolution timestamps, last staff reply, and requester read time. Messages preserve whether the author was acting as support staff so historical presentation does not depend on current roles.
- `ExternalAuthExchange` stores only the hashed handoff handle plus provider identity metadata, purpose, safe return path, expiry, and consumption state. Exchanges expire after ten minutes and are consumed atomically to prevent replay.
- `HobbyProject` -> `ApplicationUser` (owner), optional moderation actor, and optional cover `Post`, all with restricted deletion. Project metadata stores kind, status, optional game/faction/goal/start date, activity/completion/archive timestamps, and moderation visibility.
- `HobbyProjectEntry` links one existing `Post` to one project with an optional milestone label and nullable showcase order. Project maintenance deletion may cascade entries, while post deletion is restricted; ordinary post removal remains soft deletion and disappears from project projections.
- Unique indexes enforce one project membership per post and one non-null showcase order per project. Owner/activity, public visibility/status/kind, diary, and reverse-post indexes support project dashboards and discovery.
- Normalized non-null user email is uniquely indexed. Migration fails on existing duplicates rather than merging or deleting accounts.
- `Like` has a database-level unique index on `(PostId, UserId)`; service code treats duplicate insert races as an already-liked outcome.
- Direct one-to-one conversations store a deterministic `DirectConversationKey` with a filtered unique index. The key is generated from the sorted participant IDs so reverse-order conversation creation resolves to the same thread.

### 3.1 Hobby Project Invariants

- Project text is trimmed plain text. Titles are limited to 140 characters, descriptions to 4,000, game system and faction/theme to 120 each, goals to 240, and milestone labels to 140. The client renders these values as text rather than trusted markup.
- Project kinds are `Miniature`, `Unit`, `Army`, `Warband`, `Terrain`, `Diorama`, and `Other`; lifecycle statuses are `Planning`, `InProgress`, `OnHold`, and `Completed`.
- An owner may have at most 50 projects. A project may link at most 250 posts and curate at most 24 showcase entries. One post may belong to only one project.
- A project is publicly eligible only while it is not archived, not moderation-hidden, and has at least one non-hidden linked post. Empty projects remain owner-only setup records. Owner reads include empty, archived, and moderation-hidden records.
- Diary order is post creation time descending and then post ID descending. Showcase order is the owner's explicit ascending order. Showcase entries must be visible linked posts with at least one image; the leading existing post image is used without copying media.
- A cover must be an image-bearing linked post. Read projections fall back from the selected cover to the first visible showcase entry, then the newest visible diary image; the WebApp supplies the standard placeholder when no image remains.
- Completion requires a visible image-bearing showcase entry and records `CompletedUtc`. Linking or moving a diary post into a completed project reopens it as `InProgress`; milestone, showcase-order, and cover changes do not reopen it. Removing the final showcase entry from a completed project is rejected until the owner changes status.
- Confirmed moves are atomic between projects owned by the same user. They clear an old cover and showcase placement when applicable, update both projects' activity times, and never copy or delete the post.
- Archiving is reversible and never changes linked posts. Archived projects must be restored before metadata, status, milestone, showcase, cover, or link changes, while unlink remains available for cleanup. Hiding a project does not hide its posts; hiding a post excludes it from diary/showcase/cover projections without silently changing project status. Owners see a curation warning when moderation removes the final visible showcase item from a completed project.
- Suspended users cannot create, edit, restore, link, move, curate, change cover/status, or publish a project-aware post; archive and unlink remain available for cleanup. The `new-posts` site control pauses project creation, restore, linking, moves, and project-aware publishing while leaving metadata, milestone, status, showcase, and cover maintenance available. Every project mutation uses the existing write rate-limit policy.
- Anonymous users can read only publicly eligible projects. Owner mutations use ownership-isolated lookups so another user receives `404`; malformed data returns `400`, while duplicate membership, limits, and invalid lifecycle changes return `409`.
- Authenticated non-owners can report a public project through the existing report queue, with self-report and duplicate-open-report protection. Project preview/hide/restore endpoints remain limited to Moderator/Admin roles and write the same audit trail as other moderation targets.
- Projects reuse post comments, likes, images, tags, recipes, and rich viewing. They do not emit separate following-feed events, and there is no project-level engagement or duplicated publishing stack.

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
- `ExternalAuthenticationController` (`/api/auth`)
  - `GET /providers` and `GET /google/start`
  - Google middleware callback `/signin-google` and internal completion `/google/complete`
  - `POST /external/exchange` and `POST /external/register`
  - `POST /google/link-intent`
  - `GET /sign-in-methods`, `POST /password/set`, and `DELETE /google`
- `PostsController` (`/api/posts`)
  - `GET /`
    - Supports visibility query options for moderation roles: `includeDeleted` and `deletedOnly`.
  - `GET /{id}`
  - `POST /` (JSON post create; accepts optional `ProjectId` and `MilestoneLabel`)
  - `POST /with-image` (multipart upload; accepts the same optional project fields)
  - `PUT /{id}`
  - `DELETE /{id}`
- `SearchController` (`/api/search`)
  - `GET /overview`
  - `GET /posts`
  - `GET /projects` searches public project title, description, game system, faction/theme, goal, and owner display name. Title matches rank first, followed by metadata relevance and recent activity.
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
  - `POST /projects/{projectId}/hide`
  - `POST /projects/{projectId}/restore`
  - `GET /projects/{projectId}/preview`
- `ReportsController` (`/api/reports`)
  - `POST /posts/{postId}`
  - `POST /comments/{commentId}`
  - `POST /users/{userId}`
  - `POST /projects/{projectId}`
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
- `SupportTicketsController` (`/api/support/tickets`) for authenticated requester-owned support threads
  - `GET /` and `GET /{id}`
  - `POST /` and `POST /{id}/messages`
  - `GET /unread-count` and `POST /{id}/read`
- `AdminSupportTicketsController` (`/api/admin/support/tickets`) for Admin-only support operations
  - `GET /` and `GET /{id}`
  - `POST /{id}/messages`
  - `PUT /{id}/status`
- `HobbyProjectsController` (`/api/projects`) combines anonymous discovery/detail/diary/showcase reads with authenticated owner management:
  - `GET /`, `GET /{id}`, `GET /{id}/diary`, and `GET /{id}/showcase`
  - `GET /mine` and `GET /{id}/available-posts`
  - `POST /`, `PUT /{id}`, and `PUT /{id}/status`
  - `POST /{id}/archive` and `POST /{id}/restore`
  - `POST /{id}/posts`, `PUT /{id}/posts/{postId}`, and `DELETE /{id}/posts/{postId}`
  - `PUT /{id}/showcase` replaces the complete ordered post-ID list; `PUT /{id}/cover` selects or clears the cover
- `ModerationController` adds Moderator/Admin project preview, hide, and restore operations. Hiding a project does not hide its posts; hiding a post removes it from project projections.

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

Service clients in `MiniPainterHub.WebApp/Services` mirror server domains (`AuthService`, `PostService`, `HobbyProjectService`, `PaintingGuideService`, `NewsAnnouncementService`, `CommentService`, `LikeService`, `ProfileService`, `SearchService`, `ReportService`, `FollowService`, `ConversationService`, `SupportTicketService`).
Service clients also include moderation flows (`ModerationService`) and keep query DTO/filter parity with the server API.

Authentication UX composition:
- `Pages/Login.razor` and `Pages/Registration.razor` retain password forms and conditionally expose Google when the server reports it enabled.
- `Pages/ExternalAuthCallback.razor` exchanges the HttpOnly handoff and renders cancellation, expiry, conflict, restriction, and provider-failure states.
- `Pages/ExternalAuthRegistration.razor` holds onboarding state only in scoped WASM memory and collects the permanent username.
- `Pages/SignInMethods.razor` manages Google linking, local-password setup, and guarded disconnection.
- `Pages/Privacy.razor` and `Pages/Terms.razor` are public pilot legal surfaces; their support address comes from server configuration.
- All password and external success paths use the same token-acceptance logic and validated local return paths.

Guide UX composition:
- `Pages/Guides/GuideList.razor` lists public painting guides.
- `Pages/Guides/CreateGuide.razor` lets authenticated users create ordered guide steps with optional photos.
- `Pages/Guides/GuideDetails.razor` renders the walkthrough, color notes, technique notes, and attached images.

News UX composition:
- `Pages/News/NewsList.razor` and `Pages/News/NewsDetails.razor` expose public announcements.
- `Pages/Admin/CreateNewsAnnouncement.razor` is the admin-authored announcement composer.

Support UX composition:
- `Pages/Support.razor`, `Pages/SupportCreate.razor`, and `Pages/SupportDetails.razor` provide authenticated ticket history, creation, unread acknowledgement, threaded replies, and requester-driven reopening.
- `Pages/Admin/Support.razor` provides the Admin-only support queue, filtering, inspection, replies, and resolution. Moderators do not receive this route or navigation entry.

Hobby Project UX composition:
- `/projects` (`ProjectList.razor`) provides public search/filter discovery, while `/projects/mine` (`MyProjects.razor`) separates the owner's setup, active, completed, on-hold, archived, and moderation-hidden records.
- `/projects/new` (`CreateProject.razor`) persists its draft locally until successful creation or explicit discard. `/projects/{id}?view=diary|showcase` (`ProjectDetails.razor`) keeps the accessible view selection in the URL; non-completed projects default to Diary, while completed projects default to Showcase when a visible showcase remains.
- `/projects/{id}/edit` (`EditProject.razor`) covers metadata/status lifecycle, existing-post linking and confirmed moves, milestone editing, cover selection, explicit move-up/move-down showcase ordering, completion requirements, and archive/restore.
- Existing post cards/details expose a compact public project reference, while the post composer can publish directly into an owned project and persist project/milestone draft state.
- `/posts/new?projectId={id}` overrides an older draft project selection, requires an explicit replacement or `No project` choice if the requested project is unavailable, and returns a successful project-aware publish to the new diary entry.
- Public profiles show up to three recent projects, global search includes overview and dedicated project results, and project reports/moderation reuse the existing safety workflows.

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
- Posts, comments, public profiles, and hobby projects expose inline reporting through `Shared/ReportAction.razor`.

## 8) Testing Strategy

Server tests are in `MiniPainterHub.Server.Tests` and cover services/controllers.
Blazor component tests are in `MiniPainterHub.WebApp.Tests`.

Preferred validation commands:
- `dotnet build MiniPainterHub.sln`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj`
- `dotnet run --project MiniPainterHub.LoadTests/MiniPainterHub.LoadTests.csproj -- --profile smoke --base-url <ready server URL>`

When adding tests with EF InMemory, seed realistic relational data (for example all referenced users for likes/comments) to avoid impossible production states.
