## Agent Workflow Contract

This root file stays in place because local agents and tools expect it here. Durable project guidance lives in the Obsidian vault.

## Read Order

1. `AGENTS.md` for repo-wide context minimization rules.
2. This file for the root workflow contract.
3. [Agent navigation](<ObsidianVault/00 Start Here/Agent Navigation.md>) to choose the smallest matching vault source.
4. [Vault specification](<ObsidianVault/00 Start Here/Vault Specification.md>) when changing documentation structure.
5. [Architecture](<ObsidianVault/20 Engineering/ARCHITECTURE.md>)
6. [Code style](<ObsidianVault/20 Engineering/CODE_STYLE.md>)
7. [Best practices](<ObsidianVault/30 Process/BEST_PRACTICES.md>)
8. [Anti-patterns](<ObsidianVault/30 Process/ANTI_PATTERNS.md>)
9. [Contributing](<ObsidianVault/30 Process/CONTRIBUTING.md>)
10. [Workflow playbook](<ObsidianVault/30 Process/WORKFLOW_PLAYBOOK.md>) when a task needs more detail.

If docs conflict with running code, trust the code and update the affected vault note in the same change.

## Default Workflow

For non-trivial work:

1. Scope with `AGENTS.md` and `.agents/skills/context-scope-guard`.
2. Use [Agent navigation](<ObsidianVault/00 Start Here/Agent Navigation.md>) when durable project knowledge is needed.
3. Read only files inside the current scope.
4. Make the smallest coherent change.
5. Verify with the strongest relevant command set.
6. Summarize changed files, validation, and follow-ups.

For UI work, also follow [UI quality playbook](<ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md>) and `.codex/skills/ui-iteration-guard/SKILL.md` for complex UI changes.

## Core Architecture Rules

- Controllers are transport/orchestration only.
- Business logic belongs in `MiniPainterHub.Server/Services`.
- Persistence flows through `MiniPainterHub.Server/Data/AppDbContext.cs`.
- Shared API contracts belong in `MiniPainterHub.Common` unless there is a strong reason otherwise.
- Preserve middleware order in `MiniPainterHub.Server/Program.cs`: `UseAuthentication()` before `UseAuthorization()`.

## Verification

Docs-only changes:
- Ensure links point to existing files.

Non-doc code changes:
- `dotnet build MiniPainterHub.sln`
- At least one affected automated test command.

Server changes:
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`

Web/UI changes:
- Build the solution.
- Run the relevant Playwright smoke or UI review commands from the vault playbooks.
- Review screenshots before claiming UI completion.

## Documentation Maintenance

When behavior changes, update the matching vault note:

- Architecture or layering: [ARCHITECTURE.md](<ObsidianVault/20 Engineering/ARCHITECTURE.md>)
- Style or conventions: [CODE_STYLE.md](<ObsidianVault/20 Engineering/CODE_STYLE.md>)
- Recommended workflow or pattern: [BEST_PRACTICES.md](<ObsidianVault/30 Process/BEST_PRACTICES.md>)
- Pitfall discovered: [ANTI_PATTERNS.md](<ObsidianVault/30 Process/ANTI_PATTERNS.md>)
- Contributor process: [CONTRIBUTING.md](<ObsidianVault/30 Process/CONTRIBUTING.md>)
- Vault structure or routing: [Vault Specification.md](<ObsidianVault/00 Start Here/Vault Specification.md>) and [Project Index.md](<ObsidianVault/00 Start Here/Project Index.md>)
