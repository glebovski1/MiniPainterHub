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
- Date: 2026-03-17
- PR/Commit: working tree
- Scope: AI workflow and UI verification
- What changed: Added a Playwright-first UI quality enforcement workflow, repo/global skills, route-state matrix, CI-aware UI review gating, and an explicit fix-and-rerun remediation loop for UI defects.
- Why: UI work was being declared complete without enough browser-level visual review, which let unacceptable presentation regressions through.
- Risk/Notes: The workflow adds more required validation for UI-affecting changes, depends on Playwright artifacts being generated and reviewed, and requires agents to keep fixing reviewed defects until the scoped review is clean or a real blocker is documented.
