# Visual Assets Index

Purpose: document visual reference assets that support portfolio docs, UI review, and design work.

When to read: refreshing portfolio screenshots, checking visual asset provenance, or updating docs that embed screenshots.

Update triggers: screenshot refreshes, asset folder additions, or changes to the deterministic capture workflow.

## Brand Assets

The proposed MiniPainterHub logo system lives in [Brand](<Brand/>).

It includes the primary emblem, light and reversed horizontal lockups, a favicon-scale variant, a review board, and export guidance. Usage and implementation rules live in [Brand Identity](<../40 Design/Brand Identity.md>).

## Portfolio Screenshots

Current screenshot assets live in [Portfolio Screenshots](<Portfolio Screenshots/>).

These images are referenced by [Project README](<../10 Project/README.md>) and should be refreshed when the documented UI surfaces materially change.

Expected screenshot set:

- `01-home-feed.png`
- `02-create-post.png`
- `03-post-details.png`
- `04-rich-viewer.png`
- `05-search-discovery.png`
- `06-messages.png`
- `07-admin-reports.png`

## Maintenance Rules

- Keep generated screenshots out of source-of-truth process notes unless the note specifically owns visual review.
- Prefer deterministic Playwright capture over manual screenshots.
- Update linked docs in the same change when screenshot filenames change.
