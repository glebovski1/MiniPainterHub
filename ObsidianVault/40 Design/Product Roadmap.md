# Product Roadmap

Purpose: own the prioritized product feature roadmap and the durable outcome, rationale, boundaries, and delivery status for each item.

When to read: feature prioritization, product planning, roadmap status review, or before starting work on one of the listed initiatives.

Update triggers: a roadmap item changes priority or status, implementation materially changes an item's boundaries, a new initiative is accepted, or an item is removed or superseded.

Related notes: [Design index](<README.md>), [Architecture](<../20 Engineering/ARCHITECTURE.md>), and [Decisions](<../30 Process/Decisions/README.md>).

## Status

| Priority | Feature | Status |
| --- | --- | --- |
| 1 | Support Center | Implemented |
| 2 | Google authentication | Planned |
| 3 | Hobby Projects | Planned |
| 4 | AI Newsletter | Planned |

## 1. Support Center

**Intended outcome:** signed-in users can open threaded support tickets, read Admin replies, and continue or reopen a conversation from MiniPainterHub.

**Rationale:** direct feedback and issue resolution are especially valuable while the user base is small, and they reveal where registration, publishing, and account workflows need improvement.

**V1 boundaries:** authenticated users only; Admin access only; ticket categories and statuses; in-app unread state; plain-text messages. Excludes Moderators, anonymous submissions, attachments, assignment, internal notes, email, real-time notifications, and deletion.

## 2. Google Authentication

**Intended outcome:** reduce registration and sign-in friction through Google while retaining MiniPainterHub's existing account and authorization model.

**Rationale:** easier account creation can improve activation before investing in features that require a larger active community.

**Initial boundaries:** Google is the first external provider; external identity linking must avoid accidental account merges and issue the same application session used by password accounts. Discord authentication remains a later extension through the same provider model.

## 3. Hobby Projects

**Intended outcome:** combine painting diaries and army showcases into one project system with chronological progress and a curated completed view.

**Rationale:** the feature is useful to a single painter, encourages repeat posting, and creates a distinctive reason to return without splitting activity across separate diary and showcase content types.

**Initial boundaries:** project metadata, progress status, milestones, and links to existing posts; diary and showcase are views of the same project. Painting guides remain separate instructional content.

## 4. AI Newsletter

**Intended outcome:** publish a useful, source-linked hobby-news digest that keeps MiniPainterHub fresh before community publishing volume is high.

**Rationale:** a restrained autonomous newsletter can seed recurring value without depending on a large contributor base.

**Initial boundaries:** begin with a weekly digest; use trusted sources, deduplication, citations, visible AI-curation disclosure, audit history, publishing limits, and an emergency unpublish control. Increase frequency only when readership supports it. Autonomous moderation is a separate future capability.
