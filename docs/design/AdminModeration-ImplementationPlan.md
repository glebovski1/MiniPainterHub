# Admin Moderation — Implementation Plan

## Scope of this document
This is a **planning-only** document based on static inspection of the current repository. It defines a minimal MVP path that fits existing architecture and conventions in `MiniPainterHub`.

> Path convention note: `docs/design` did not exist; this plan uses the requested path and adds that folder as a docs-only extension under existing `docs/` conventions.

---

## 1) Architecture Fit

### Existing patterns observed
- **Layering**: controllers are transport/orchestration; business rules belong in `MiniPainterHub.Server/Services` (`AGENT.md`, `docs/ARCHITECTURE.md`, current controller/service code).
- **Persistence**: EF Core via `AppDbContext` in `MiniPainterHub.Server/Data/AppDbContext.cs`.
- **Auth**: ASP.NET Identity + JWT bearer in `MiniPainterHub.Server/Program.cs`, with identity entity `ApplicationUser`.
- **Errors**: standardized `ProblemDetails` via exception handlers in `MiniPainterHub.Server/ErrorHandling`.
- **Soft delete pattern**: `IsDeleted` + `UpdatedUtc` fields on `Post` and `Comment`; filtering is currently explicit in service queries (`Where(... && !IsDeleted)`), not global query filters.
- **Client auth/UI**: Blazor WASM with `AuthorizeRouteView`, `AuthorizeView`, and JWT claims parsing in `MiniPainterHub.WebApp/Services/JwtAuthenticationStateProvider.cs`.
- **Contracts**: shared API DTOs usually in `MiniPainterHub.Common`.

### Fit approach for moderation MVP
- Keep **controllers thin** and put moderation/suspension/maintenance rules into new or extended services.
- Reuse existing **Identity roles** (`Admin` already seeded) and extend JWT issuance to include role claims, rather than introducing a new auth framework.
- Keep soft-delete behavior consistent with current explicit query predicates for MVP; avoid introducing global query filters unless done uniformly for existing entities in a dedicated preparatory refactor phase.
- Reuse existing exception-driven `ProblemDetails` response path (throw domain exceptions from services).

---

## 2) Decisions locked for MVP

1. **Moderator restrict only; Admin can suspend**
   - `Moderator`: content moderation actions only (hide/remove/restore content + audit).
   - `Admin`: all moderator actions + user suspension/unsuspension.

2. **Audit logs append-only**
   - No update/delete path for moderation audit rows from API.
   - Insert-only from service layer.

3. **Soft delete retention 30 days**
   - Soft-deleted moderated content retained for 30 days before purge eligibility.
   - Purge mechanism can be deferred but schema must support timestamp and reason.

4. **Maintenance mode content negotiation**
   - If request `Accept` includes `text/html`, return maintenance HTML response.
   - Otherwise return JSON `503` `ProblemDetails`.

---

## 3) Data model mapping

## 3.1 `ApplicationUser` changes
- **Entity**: `MiniPainterHub.Server.Identity.ApplicationUser`
- **File**: `MiniPainterHub.Server/Identity/ApplicationUser.cs`
- **New fields (MVP)**:
  - `DateTime? SuspendedUntilUtc`
  - `string? SuspensionReason`
  - `DateTime? SuspensionUpdatedUtc`
- **DbContext changes**:
  - No new DbSet needed (Identity user table extension).
  - Add property constraints in `OnModelCreating` if needed (e.g., reason max length).

## 3.2 `Post` moderation metadata
- **Entity**: `MiniPainterHub.Server.Entities.Post`
- **File**: `MiniPainterHub.Server/Entities/Post.cs`
- **New fields**:
  - `DateTime? ModeratedUtc`
  - `string? ModeratedByUserId`
  - `string? ModerationReason`
  - `DateTime? SoftDeletedUtc` (for 30-day retention calculation)
- **DbContext changes**:
  - Configure optional FK from `Post.ModeratedByUserId` to `ApplicationUser` with `DeleteBehavior.Restrict`.

## 3.3 `Comment` moderation metadata
- **Entity**: `MiniPainterHub.Server.Entities.Comment`
- **File**: `MiniPainterHub.Server/Entities/Comment.cs`
- **New fields**:
  - `DateTime? ModeratedUtc`
  - `string? ModeratedByUserId`
  - `string? ModerationReason`
  - `DateTime? SoftDeletedUtc`
- **DbContext changes**:
  - Configure optional FK from `Comment.ModeratedByUserId` to `ApplicationUser` with `DeleteBehavior.Restrict`.

## 3.4 Append-only audit entity
- **Entity**: `ModerationAuditLog` (new)
- **File**: `MiniPainterHub.Server/Entities/ModerationAuditLog.cs`
- **Fields (minimum)**:
  - `long Id`
  - `DateTime CreatedUtc`
  - `string ActorUserId`
  - `string ActorRole`
  - `string ActionType` (e.g., `PostHide`, `CommentRestore`, `UserSuspend`)
  - `string TargetType` (`Post`, `Comment`, `User`)
  - `string TargetId`
  - `string? Reason`
  - `string? MetadataJson`
- **DbContext changes**:
  - Add `DbSet<ModerationAuditLog> ModerationAuditLogs`
  - Add entity configuration: indexes on `CreatedUtc`, `TargetType+TargetId`, `ActorUserId`; required max lengths.

## 3.5 Maintenance mode settings
- **Options class**: `MaintenanceOptions` (new)
- **File**: `MiniPainterHub.Server/Options/MaintenanceOptions.cs`
- **Fields**:
  - `bool Enabled`
  - `string? Message`
  - `DateTime? PlannedEndUtc`
  - `bool AllowAdmins`
- **Config source**:
  - `appsettings*.json` + env vars via options binding in `Program.cs`.

### Query filter approach (consistent with current repo)
- Current code uses **explicit service-layer predicates** (`!IsDeleted`) rather than `HasQueryFilter`.
- MVP plan keeps that pattern and extends affected queries similarly:
  - feed and detail reads exclude moderated/deleted records unless admin/moderator endpoint explicitly asks for them.
- Optional future cleanup: a dedicated refactor to global query filters across `Post` + `Comment` only after parity tests are in place.

---

## 4) Services & responsibilities

## 4.1 New service: moderation domain
- **Interface**: `IModerationService`
- **File**: `MiniPainterHub.Server/Services/Interfaces/IModerationService.cs`
- **Implementation**: `ModerationService`
- **File**: `MiniPainterHub.Server/Services/ModerationService.cs`

### Planned methods
- `Task ModeratePostAsync(int postId, string actorUserId, bool hide, string? reason)`
- `Task ModerateCommentAsync(int commentId, string actorUserId, bool hide, string? reason)`
- `Task SuspendUserAsync(string targetUserId, string actorUserId, DateTime? suspendedUntilUtc, string? reason)`
- `Task UnsuspendUserAsync(string targetUserId, string actorUserId, string? reason)`
- `Task<PagedResult<ModerationAuditDto>> GetAuditAsync(ModerationAuditQueryDto query)`

Responsibilities:
- Validate actor permissions (role/ownership rules).
- Load and mutate entities.
- Write append-only `ModerationAuditLog` entries.
- Throw existing exception types for consistent ProblemDetails.

## 4.2 Extend existing services
- **`PostService`** (`MiniPainterHub.Server/Services/PostService.cs`)
  - Add moderation visibility predicates in list/detail reads.
  - Set `SoftDeletedUtc` when soft deleting.
- **`CommentService`** (`MiniPainterHub.Server/Services/CommentService.cs`)
  - Add moderation visibility predicates.
  - Set `SoftDeletedUtc` when soft deleting.
- **`AuthController` + token generation path**
  - Include role claims in JWT so client/UI and `[Authorize(Roles=...)]` are reliable.

## 4.3 New service: enforcement checks
- **Interface**: `IAccountRestrictionService`
- **Files**:
  - `MiniPainterHub.Server/Services/Interfaces/IAccountRestrictionService.cs`
  - `MiniPainterHub.Server/Services/AccountRestrictionService.cs`
- Method examples:
  - `Task EnsureCanLoginAsync(ApplicationUser user)`
  - `Task EnsureCanCreatePostAsync(string userId)`
  - `Task EnsureCanCommentAsync(string userId)`
- Called by `AuthController.Login`, `PostService.Create*`, `CommentService.CreateAsync`.

---

## 5) API endpoints plan

## 5.1 New moderation controller
- **File**: `MiniPainterHub.Server/Controllers/ModerationController.cs`
- **Base route**: `api/moderation`
- **Controller style**: thin, delegates to services.

### Endpoints (MVP)
- `POST /api/moderation/posts/{postId}/hide` — Roles: `Moderator,Admin`
- `POST /api/moderation/posts/{postId}/restore` — Roles: `Moderator,Admin`
- `POST /api/moderation/comments/{commentId}/hide` — Roles: `Moderator,Admin`
- `POST /api/moderation/comments/{commentId}/restore` — Roles: `Moderator,Admin`
- `POST /api/moderation/users/{userId}/suspend` — Roles: `Admin`
- `POST /api/moderation/users/{userId}/unsuspend` — Roles: `Admin`
- `GET /api/moderation/audit` — Roles: `Moderator,Admin`

## 5.2 DTOs and contracts
Place shared request/response contracts in `MiniPainterHub.Common/DTOs`:
- `ModerationActionRequestDto` (`Reason`)
- `SuspendUserRequestDto` (`SuspendedUntilUtc`, `Reason`)
- `ModerationAuditDto`
- `ModerationAuditQueryDto` (paging/filter)

## 5.3 Error handling
- Keep existing exception strategy:
  - `NotFoundException` -> 404
  - `ForbiddenException` -> 403
  - `DomainValidationException` -> 400 with `errors`
- Avoid ad-hoc response envelopes; return DTOs + ProblemDetails only.

---

## 6) Middleware & enforcement plan

## 6.1 Maintenance middleware placement
- **New middleware**: `MiniPainterHub.Server/Middleware/MaintenanceModeMiddleware.cs`
- **Pipeline placement in `Program.cs`**:
  - After `UseExceptionHandler()`
  - **After `UseAuthentication()` and before `UseAuthorization()`** so `HttpContext.User` is populated for optional admin bypass checks.
- Behavior:
  - If maintenance disabled: no-op.
  - If enabled:
    - allow `/healthz` and optional admin bypass when authenticated admin and `AllowAdmins=true`.
    - if `Accept: text/html` -> return maintenance HTML.
    - else -> return `503` ProblemDetails JSON.

## 6.2 Posting/login restriction checks
Use **both** points:
1. **Token issuance (login)**: block suspended users at login (`AuthController`).
2. **Per-request write checks**: enforce in service layer for create/update actions (`PostService`, `CommentService`) to handle already-issued tokens.

This dual-check minimizes bypass risk while matching existing service-centric authorization patterns.

---

## 7) Client (Blazor) admin UI plan (planning only)

## 7.1 Routes/pages
- Add folder `MiniPainterHub.WebApp/Pages/Admin/`:
  - `ModerationDashboard.razor` (`@page "/admin/moderation"`)
  - `AuditLog.razor` (`@page "/admin/audit"`)
  - `UserSuspensions.razor` (`@page "/admin/suspensions"`)

## 7.2 Service client
- New typed service:
  - `MiniPainterHub.WebApp/Services/Interfaces/IModerationService.cs`
  - `MiniPainterHub.WebApp/Services/ModerationService.cs`
- Reuse `ApiClient` and JWT auth handler pattern already used by `PostService`, `CommentService`.

## 7.3 Auth/UI gating
- Add role-aware checks based on JWT role claims parsed in `JwtAuthenticationStateProvider`.
- Navigation updates in `MiniPainterHub.WebApp/Layout/NavMenu.razor` to show admin/moderator links conditionally.
- Keep server as source of truth (UI hiding is only cosmetic).

---

## 8) Testing plan

## 8.1 Unit tests (server service layer)
Add/extend in `MiniPainterHub.Server.Tests/Services`:
- `ModerationServiceTests`
  - hide/restore post creates audit row
  - hide/restore comment creates audit row
  - moderator cannot suspend user
  - admin can suspend/unsuspend user
  - audit log is append-only from API surface
- `PostServiceTests`
  - soft delete sets `SoftDeletedUtc`
  - feed excludes moderated/deleted entries by default
- `CommentServiceTests`
  - create blocked for suspended user
- `AccountRestrictionServiceTests`
  - active user allowed
  - suspended-until-future blocked
  - expired suspension allowed

## 8.2 API tests (integration-style)
Add in `MiniPainterHub.Server.Tests/Controllers` using existing `IntegrationTestApplicationFactory`:
- `ModerationControllerTests`
  - role authorization matrix (`User`, `Moderator`, `Admin`)
  - success and 404/403 paths
  - audit retrieval paging/filtering
- `AuthControllerTests`
  - suspended user login returns 403/401 per final policy
- `PostsControllerTests` + `CommentsControllerTests`
  - suspended user cannot create content
- `ProgramStartupTests` / middleware tests
  - maintenance mode returns HTML for browser accept header
  - maintenance mode returns JSON 503 otherwise

## 8.3 WebApp tests (bUnit)
Add in `MiniPainterHub.WebApp.Tests/Pages` / `Layout`:
- admin nav link visibility based on claims
- moderation dashboard renders expected actions for moderator/admin
- non-privileged user is redirected/blocked on admin pages

---

## 9) Step-by-step execution sequence

## Phase 0 — preparatory refactors
**Files to touch**
- `MiniPainterHub.Server/Controllers/AuthController.cs`
- `MiniPainterHub.Server/Program.cs`
- `MiniPainterHub.WebApp/Services/JwtAuthenticationStateProvider.cs`

**Work**
- Standardize role claim emission/consumption so role-based authorization is reliable.
- Register new options/middleware/service DI placeholders without behavior switch-on yet.

**Acceptance criteria**
- Existing auth endpoints still pass current tests.
- JWT contains role claim(s) and client auth state can read them.

**Rollback notes**
- Revert role-claim changes only; keep current claim set and fallback to existing auth behavior.

## Phase 1 — data model + migrations
**Files to touch**
- `MiniPainterHub.Server/Identity/ApplicationUser.cs`
- `MiniPainterHub.Server/Entities/Post.cs`
- `MiniPainterHub.Server/Entities/Comment.cs`
- `MiniPainterHub.Server/Entities/ModerationAuditLog.cs` (new)
- `MiniPainterHub.Server/Data/AppDbContext.cs`
- `MiniPainterHub.Server/Migrations/*` (new migration files)

**Acceptance criteria**
- Schema supports suspension, moderation metadata, and append-only audit records.
- Existing read/write behaviors unchanged before service updates.

**Rollback notes**
- Roll back migration and remove new properties/entity if deployment fails.

## Phase 2 — services + business rules
**Files to touch**
- `MiniPainterHub.Server/Services/Interfaces/IModerationService.cs` (new)
- `MiniPainterHub.Server/Services/ModerationService.cs` (new)
- `MiniPainterHub.Server/Services/Interfaces/IAccountRestrictionService.cs` (new)
- `MiniPainterHub.Server/Services/AccountRestrictionService.cs` (new)
- `MiniPainterHub.Server/Services/PostService.cs`
- `MiniPainterHub.Server/Services/CommentService.cs`
- `MiniPainterHub.Server/Program.cs` (DI wiring)

**Acceptance criteria**
- Service-level tests cover permission and rule enforcement.
- Controllers remain thin.

**Rollback notes**
- Disable new DI registrations and endpoint wiring; old services continue to operate.

## Phase 3 — controllers + endpoints
**Files to touch**
- `MiniPainterHub.Server/Controllers/ModerationController.cs` (new)
- `MiniPainterHub.Server/Controllers/AuthController.cs`
- `MiniPainterHub.Server/Program.cs` (authorization policies, if named policies added)
- `MiniPainterHub.Common/DTOs/*Moderation*.cs` (new shared contracts)

**Acceptance criteria**
- Endpoint authorization matrix behaves as designed.
- ProblemDetails shape remains consistent for errors.

**Rollback notes**
- Remove moderation route mapping and controller; no impact on existing post/comment APIs.

## Phase 4 — client admin pages
**Files to touch**
- `MiniPainterHub.WebApp/Pages/Admin/*.razor` (new)
- `MiniPainterHub.WebApp/Services/Interfaces/IModerationService.cs` (new)
- `MiniPainterHub.WebApp/Services/ModerationService.cs` (new)
- `MiniPainterHub.WebApp/Layout/NavMenu.razor`
- `MiniPainterHub.WebApp/Program.cs` (service registration)

**Acceptance criteria**
- Admin/moderator users can access moderation pages and perform operations through API.
- Non-privileged users cannot access actions (server-enforced).

**Rollback notes**
- Hide/remove admin nav links and pages without affecting existing user flows.

## Phase 5 — tests + hardening
**Files to touch**
- `MiniPainterHub.Server.Tests/Services/*`
- `MiniPainterHub.Server.Tests/Controllers/*`
- `MiniPainterHub.Server.Tests/ProgramStartupTests.cs`
- `MiniPainterHub.WebApp.Tests/Pages/*` and/or layout tests

**Acceptance criteria**
- New tests pass with existing harness patterns.
- No regressions in feed ordering, auth behavior, and ProblemDetails contracts.

**Rollback notes**
- If flaky, isolate new tests by feature area and keep core regression tests green.

---

## 10) Risk list & mitigations (top 5)

1. **Role claims missing from JWT break `[Authorize(Roles=...)]` and admin UI gating**
   - Mitigation: add explicit role claims at login and integration tests asserting role authorization paths.

2. **Inconsistent soft-delete filtering leaks moderated content into feed/detail**
   - Mitigation: centralize predicates in service methods and add regression tests for list/detail queries.

3. **Maintenance middleware intercepts static assets or auth unexpectedly**
   - Mitigation: explicitly allow `/healthz` and static framework paths as needed; add startup/integration tests for pipeline behavior.

4. **Suspension checks only at login allow existing tokens to keep posting**
   - Mitigation: enforce restrictions in both login and write-service methods.

5. **Feed ordering regressions during moderation filter additions**
   - Mitigation: preserve existing `OrderByDescending(p => p.CreatedUtc)` semantics and lock with API tests.

---

## Previous-attempt divergence summary

- No concrete prior moderation implementation artifacts were found on current branch (no moderation controller/service/entity present, and commit history grep found no moderation-oriented implementation commit).
- Based on repo rules and patterns, likely divergence points to avoid in the next attempt:
  1. Implementing moderation logic in controllers instead of `Services`.
  2. Introducing global query filters only for new tables while current code uses explicit predicates.
  3. Creating server-only DTOs where the same contracts are needed by WebApp (should live in `MiniPainterHub.Common`).
  4. Returning ad-hoc error payloads instead of existing `ProblemDetails` exception handlers.
  5. Adding UI-only authorization checks without server-side role enforcement.
