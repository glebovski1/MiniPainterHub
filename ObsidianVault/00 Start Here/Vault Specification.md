# Vault Specification

The Obsidian vault is the durable MiniPainterHub knowledge base. Root markdown files stay small because local agents and tools expect them, but long-lived project knowledge belongs here.

## Goals

- Make project knowledge discoverable without forcing agents to scan the repository.
- Separate source-of-truth rules from task-local notes, screenshots, and planning artifacts.
- Keep documentation easy to refactor as the project grows.
- Preserve Obsidian-friendly links while staying readable in GitHub and local editors.

## Current Assessment

The vault move is a good foundation: it separates project knowledge from source code, keeps root agent files stable, and gives large docs a clearer home. Its current limitation is that [Project Index.md](<Project Index.md>) is only a flat table of contents. Agents need a routing layer, folder ownership rules, and update triggers so the vault remains flexible instead of becoming another renamed `docs` folder.

This specification, [Agent Navigation.md](<Agent Navigation.md>), [Knowledge Map.md](<Knowledge Map.md>), and folder `README.md` indexes provide that layer.

## Source-Of-Truth Rules

- Running code wins over stale documentation.
- `AGENTS.md` owns context minimization rules.
- `AGENT.md` owns the root workflow contract and points into the vault.
- [Agent Navigation.md](<Agent Navigation.md>) owns task-to-note routing.
- [Knowledge Map.md](<Knowledge Map.md>) owns folder purpose and retrieval rules.
- This file owns vault structure and refactoring policy.
- Folder `README.md` files own local routing inside each major vault area.
- [Decision Index](<../30 Process/Decisions/README.md>) owns accepted ADR discovery.

## Note Contract

When creating or substantially refactoring a durable vault note, include enough of this contract to make the note self-routing:

- Purpose: what decision or workflow the note owns.
- When to read: task types that should start here.
- Update triggers: code, process, or design changes that require edits.
- Related notes: only the nearest source-of-truth links.

Existing notes do not need churn-only rewrites, but add these fields when the note is already being edited for substance.

## Refactoring Policy

- Add new knowledge to the smallest existing folder that owns it.
- Split a note only when it has multiple owners, repeated update conflicts, or unrelated audiences.
- Merge notes when they duplicate rules and one is not a clear source of truth.
- Add a new top-level folder only after updating this specification and [Project Index.md](<Project Index.md>).
- Prefer hub notes over duplicate summaries. Link to the source rather than copying policy text.
- Archive stale or superseded notes before removing active links to them.
- Use `_logs/vault-changes/` for meaningful vault edits and `_logs/reflections/` for post-edit self-reflection.
- Use `_templates/` for reusable note templates.

## Proposed Evolution

1. Keep `00 Start Here` as the routing layer for humans and agents.
2. Add the note contract opportunistically to high-traffic notes.
3. Split long mixed-purpose docs into source-of-truth policy plus archive/audit notes only when they become hard to maintain.
4. Keep accepted decisions in ADR form under `30 Process/Decisions/`.
5. Run a stale-link and stale-path check after any vault move.
