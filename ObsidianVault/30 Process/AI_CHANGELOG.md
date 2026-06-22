# AI Change Log

Status: legacy historical index. New vault maintenance entries should be written under `_logs/vault-changes/`; this file is retained only for pre-log project history.

Track notable changes (refactors, architectural updates, risky edits).
Git commits/PRs are the source of truth; this log is a human-friendly index.

## Entry Template
- Date:
- PR/Commit:
- Scope:
- What changed:
- Why:
- Risk/Notes:
- Prompt snapshot (optional):

## Entries
- Date: 2026-03-20
- PR/Commit: working tree
- Scope: Rich viewer recovery retrospective
- What changed: Added a decision note documenting why the rich viewer redesign took multiple passes, what mistakes were made during implementation, and what process rule now applies to UI-heavy work.
- Why: The viewer work exposed a gap between feature completeness and real rendered quality. The repo needs an explicit record that browser reproduction and screenshot review are mandatory before UI work is considered done.
- Risk/Notes: This is a documentation-only change, but it sets a stricter expectation for future UI tasks and reviews.

