# Agent Navigation

Use this note with `AGENTS.md` and `AGENT.md` when an agent needs durable project context.

## Fast Route

1. Start with `AGENTS.md` to set the minimal file scope.
2. Use this note to choose one vault source of truth.
3. Read only the selected note and directly referenced supporting notes.
4. If code and vault docs conflict, trust running code and update the affected vault note.

## Task Routes

| Task | Read first | Add only if needed |
| --- | --- | --- |
| Architecture, layering, persistence, contracts | [ARCHITECTURE.md](<../20 Engineering/ARCHITECTURE.md>) | [Engineering index](<../20 Engineering/README.md>) |
| Code style or local conventions | [CODE_STYLE.md](<../20 Engineering/CODE_STYLE.md>) | [ANTI_PATTERNS.md](<../30 Process/ANTI_PATTERNS.md>) |
| Workflow, verification, delivery format | [WORKFLOW_PLAYBOOK.md](<../30 Process/WORKFLOW_PLAYBOOK.md>) | [CONTRIBUTING.md](<../30 Process/CONTRIBUTING.md>) |
| UI, layout, visual review | [UI_QUALITY_PLAYBOOK.md](<../30 Process/UI_QUALITY_PLAYBOOK.md>) | [Workshop Gallery](<../40 Design/Workshop Gallery/>) |
| Tests and quality gates | [Engineering index](<../20 Engineering/README.md>) | [WORKFLOW_PLAYBOOK.md](<../30 Process/WORKFLOW_PLAYBOOK.md>) |
| Deployment or hosting | [DEPLOYMENT.md](<../20 Engineering/DEPLOYMENT.md>) | [CONTRIBUTING.md](<../30 Process/CONTRIBUTING.md>) |
| Planning or design artifacts | [Design index](<../40 Design/README.md>) | [Decisions](<../30 Process/Decisions/README.md>) |
| Documentation structure or vault refactor | [Vault Specification.md](<Vault Specification.md>) | [Knowledge Map.md](<Knowledge Map.md>) |
| Durable decisions or ADRs | [Decision index](<../30 Process/Decisions/README.md>) | [Architecture](<../20 Engineering/ARCHITECTURE.md>) |

## Update Rules

- Add durable knowledge to the smallest matching vault folder.
- Add or update a link in [Project Index.md](<Project Index.md>) when the note should be discoverable from the vault root.
- Update [Vault Specification.md](<Vault Specification.md>) before adding a new top-level vault category or changing the meaning of an existing category.
- Keep task-local notes out of the vault unless they are expected to guide future work.
- Archive stale or superseded notes instead of permanently deleting them.
- Log meaningful vault edits under `_logs/vault-changes/` and run a reflection pass afterward.
