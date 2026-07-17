# Product Roadmap

Purpose: own the prioritized product feature roadmap and the durable outcome, rationale, boundaries, and delivery status for each item.

When to read: feature prioritization, product planning, roadmap status review, or before starting work on one of the listed initiatives.

Update triggers: a roadmap item changes priority or status, implementation materially changes an item's boundaries, a new initiative is accepted, or an item is removed or superseded.

Related notes: [Design index](<README.md>), [Architecture](<../20 Engineering/ARCHITECTURE.md>), and [Decisions](<../30 Process/Decisions/README.md>).

## Status

| Priority | Feature | Status |
| --- | --- | --- |
| 1 | Support Center | Implemented |
| 2 | Google and Discord authentication | Implemented |
| 3 | Hobby Projects | Implemented |
| 4 | AI Newsletter | Planned |

## 1. Support Center

**Intended outcome:** signed-in users can open threaded support tickets, read Admin replies, and continue or reopen a conversation from MiniPainterHub.

**Rationale:** direct feedback and issue resolution are especially valuable while the user base is small, and they reveal where registration, publishing, and account workflows need improvement.

**V1 boundaries:** authenticated users only; Admin access only; ticket categories and statuses; in-app unread state; plain-text messages. Excludes Moderators, anonymous submissions, attachments, assignment, internal notes, email, real-time notifications, and deletion.

## 2. Google and Discord Authentication

**Intended outcome:** reduce registration and sign-in friction through Google and Discord while retaining MiniPainterHub's existing account and authorization model.

**Rationale:** easier account creation can improve activation before investing in features that require a larger active community.

**Status:** implemented and deploy-ready. Both providers remain disabled by default until the owner supplies credentials, configures the documented callbacks, and enables the corresponding production setting.

**V1 delivered boundaries:** Google and Discord issue the same role-bearing application session used by password accounts. New users complete username onboarding; matching email never auto-merges; linking is explicit and same-email only. Account security supports local-password setup and safe provider disconnection. The delivery includes provider-bound short-lived exchanges, Development/Test fake providers, Azure activation and rollback guidance, and automated/visual coverage. Discord guild membership, bots, provider avatars, One Tap, live activation, account deletion UI, and password recovery remain outside this version.

## 3. Hobby Projects

**Intended outcome:** combine painting diaries and army showcases into one project system with chronological progress and a curated completed view.

**Rationale:** the feature is useful to a single painter, encourages repeat posting, and creates a distinctive reason to return without splitting activity across separate diary and showcase content types.

**Status:** implemented and verified.

**V1 delivered boundaries:** single-owner projects reuse existing posts as the source of truth for images, tags, reactions, comments, recipes, and rich viewing. Each linked post belongs to at most one project; milestone labels turn diary posts into checkpoints; showcase entries are an owner-curated, manually ordered subset of image-bearing diary posts. Delivery includes metadata and lifecycle management, archive/restore, covers and fallbacks, project-aware publishing and atomic moves, public discovery/profile/post/search integration, reporting and moderation, responsive owner/public pages, deterministic fixtures, and complete relational, service, component, smoke, and visual coverage. Painting guides remain separate. Collaboration, communities, project-level social features, private/unlisted visibility, direct project uploads, duplicated media, per-image showcase selection, standalone task milestones, rosters/costs/points, AI recaps, and hard deletion remain outside this version.

## 4. AI Newsletter

**Intended outcome:** publish a useful, source-linked hobby-news digest that keeps MiniPainterHub fresh before community publishing volume is high.

**Rationale:** a restrained autonomous newsletter can seed recurring value without depending on a large contributor base.

**Initial boundaries:** begin with a weekly digest; use trusted sources, deduplication, citations, visible AI-curation disclosure, audit history, publishing limits, and an emergency unpublish control. Increase frequency only when readership supports it. Autonomous moderation is a separate future capability.
