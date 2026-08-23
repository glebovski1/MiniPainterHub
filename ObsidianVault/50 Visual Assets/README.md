# Visual Assets Index

Purpose: document visual reference assets that support portfolio docs, UI review, and design work.

The portfolio screenshot set was refreshed on August 13, 2026 after the Roll & Paint shell, navigation, composer, viewer, discovery, messaging, and moderation surfaces were updated.

## Portfolio Screenshots

Current screenshot assets live in [Portfolio Screenshots](<Portfolio Screenshots/>).

Capture provenance: Playwright Chromium at a 1440x1000 desktop viewport against an isolated Development database seeded from `assets/dev-seed/avatars` and `assets/dev-seed/post-images`. These curated images use real miniature photography; checkerboard images remain reserved for UI regression fixtures.

These images are referenced by [Project README](<../10 Project/README.md>) and should be refreshed when the documented UI surfaces materially change.

Expected screenshot set:

- `01-home-feed.png`
- `02-create-post.png`
- `03-post-details.png`
- `04-rich-viewer.png`
- `05-search-discovery.png`
- `06-messages.png`
- `07-admin-reports.png`

The files intentionally keep stable names so the root README and portfolio README can link to them without churn.

## Maintenance Rules

- Keep generated screenshots out of source-of-truth process notes unless the note specifically owns visual review.
- Prefer deterministic Playwright capture over manual screenshots.
- Update linked docs in the same change when screenshot filenames change.
