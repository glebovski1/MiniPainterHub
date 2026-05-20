# Knowledge Map

This map explains what belongs where in the MiniPainterHub vault.

## Folder Roles

| Folder | Owns | Does not own |
| --- | --- | --- |
| `00 Start Here` | Vault entry points, routing, structure rules, agent navigation | Product or implementation details |
| `10 Project` | Product overview, portfolio README, current project structure, broad analysis | Process policy or source-level architecture rules |
| `20 Engineering` | Architecture, code style, deployment, audits, testing gaps | Task plans and design explorations |
| `30 Process` | Contribution workflow, best practices, anti-patterns, UI/process playbooks, decisions, change log | Long-lived product specs |
| `40 Design` | Feature plans, mockups, design exports, implementation plans | General engineering policy |
| `50 Visual Assets` | Screenshots and visual reference assets | Written specs unless tightly coupled to the asset folder |
| `_templates` | Reusable note templates | Project-specific decisions or source-of-truth content |
| `_logs` | Vault change logs and reflection logs | Canonical project knowledge |
| `_archive` | Preserved stale, merged, or superseded notes | Active guidance |

## Retrieval Pattern

- Route by task, not by folder curiosity.
- Prefer one source-of-truth note and one supporting note.
- Follow links only when the current note names them as relevant to the active task.
- Do not read the entire vault to answer a local code or docs question.

## Maintenance Pattern

- Keep stable rules in playbooks or engineering notes.
- Keep one-off investigation output out of source-of-truth notes unless it becomes reusable guidance.
- Move stale project commentary into an audit or changelog rather than leaving it in active workflow docs.
- Prefer relative markdown links so the vault works in Obsidian, GitHub, and local editor previews.
- Prefer folder `README.md` notes as local indexes.
- Add frontmatter to new ADRs and high-traffic routing notes when it improves retrieval.
- Archive first; permanent deletion requires an explicit user request.
