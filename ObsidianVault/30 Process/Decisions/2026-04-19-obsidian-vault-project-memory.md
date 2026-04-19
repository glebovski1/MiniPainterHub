---
type: adr
status: accepted
date: 2026-04-19
canonical: true
related_code:
  - .codex/vault-memory.json
  - .agents/skills/obsidian-vault-project-memory/SKILL.md
related_notes:
  - ../../00 Start Here/Project Index.md
  - ../../00 Start Here/Agent Navigation.md
  - ../../00 Start Here/Knowledge Map.md
  - ../../00 Start Here/Vault Specification.md
---

# ADR: Obsidian Vault Project Memory

## Context

MiniPainterHub uses an existing Obsidian vault for durable project knowledge. Future Codex sessions need a fast retrieval path that avoids whole-vault reads and separates current source-of-truth notes from historical plans, logs, and screenshots.

## Decision

Use `ObsidianVault/` as the repository's internal project memory.

Accepted vault operating model:

- `00 Start Here` owns routing, root indexes, folder ownership, and vault refactor policy.
- Each high-traffic folder has a lightweight `README.md` index.
- Durable architecture/process decisions live under `30 Process/Decisions/`.
- Stale or superseded notes are archived instead of permanently deleted.
- Meaningful vault edits are logged under `_logs/vault-changes/`.
- Post-edit reflection logs live under `_logs/reflections/`.
- Reusable note templates live under `_templates/`.
- Future Codex sessions should read the smallest useful note set and summarize retrieved memory before continuing.

## Consequences

- [Project Index](<../../00 Start Here/Project Index.md>) remains the human-facing root.
- [Agent Navigation](<../../00 Start Here/Agent Navigation.md>) remains the agent task router.
- [Knowledge Map](<../../00 Start Here/Knowledge Map.md>) remains the folder ownership map.
- [Vault Specification](<../../00 Start Here/Vault Specification.md>) remains the vault structure policy.
- New vault structure changes should update the relevant index and log the change.
