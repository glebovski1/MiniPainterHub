# AI Change Log

Track notable AI-assisted changes (refactors, architectural updates, risky edits).
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

- Date: 2026-03-17
- PR/Commit: working tree
- Scope: AI workflow and UI verification
- What changed: Added a Playwright-first UI quality enforcement workflow, repo/global skills, route-state matrix, CI-aware UI review gating, and an explicit fix-and-rerun remediation loop for UI defects.
- Why: UI work was being declared complete without enough browser-level visual review, which let unacceptable presentation regressions through.
- Risk/Notes: The workflow adds more required validation for UI-affecting changes, depends on Playwright artifacts being generated and reviewed, and requires agents to keep fixing reviewed defects until the scoped review is clean or a real blocker is documented.
