## Admin & Moderation MVP

### Overview
This change introduces role-based admin/moderator moderation, global feature flags, maintenance mode handling, audit logging, admin news/feed policy management, and feed scoring.

### Roles
- Admin: full access (including suspend users, hard delete, global flags).
- Moderator: content moderation + user restrictions; cannot suspend and cannot modify global flags.

### Endpoints
- Content: `POST /api/admin/content/{type}/{id}/hide|unhide|softdelete`, `DELETE /api/admin/content/{type}/{id}`.
- Users: `POST /api/admin/users/{userId}/restrict|lift|suspend|unsuspend`.
- Flags: `GET/PUT /api/admin/flags`.
- Feed policies: `GET/POST /api/admin/feed-policies`, `PUT /api/admin/feed-policies/{id}`, `PUT /api/admin/feed-policies/{id}/activate`.
- News: `GET/POST/PUT /api/admin/news`, `POST /api/admin/news/{id}/hide|unhide`, `DELETE /api/admin/news/{id}`.
- Audit: `GET /api/admin/audit`.

### Settings keys
- `SiteOnline`
- `RegistrationEnabled`
- `LoginEnabled`
- `PostingEnabled`
- `ImageUploadEnabled`
- `RetentionDays` (default 30)

### Status model
`ContentStatus`: `Active`, `Hidden`, `SoftDeleted`.
Applied to posts/comments/images and used by global query filters.

### Feed rules
- Pinned content appears first by `PinPriority DESC`.
- Non-pinned posts are scored by:
  `score = WRecency*decay + WLikes*log(1+likes) + WComments*log(1+comments) - WReportsPenalty*log(1+reports)`
  where `decay = exp(-ln(2)*ageHours/HalfLifeHours)`.
- Optional diversity by author via `MaxPerAuthorPerPage` when enabled.

### Retention
Soft-deleted content keeps `DeletedAt`; default retention is 30 days. Purge job is not implemented yet.

### Assumptions
- Admin news/feed-policy mutations are executed in dedicated services and audit-logged.
- User restrictions/suspensions honor `Until`; expired restrictions are lifted on access checks.
- Existing post/comment/image entities are moderated directly and staff listing uses `IgnoreQueryFilters()`.
- Reports count is not currently modeled, so report penalty is a no-op (`reports = 0`) in score.
- Admin UI pages are MVP scaffolds integrated with current WASM routing and API patterns.
