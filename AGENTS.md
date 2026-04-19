# Context Control

This file adds repo-wide context minimization rules. Follow `AGENT.md` for the broader workflow, verification, and architecture guidance.

## Default

- Minimize context by default.
- Before broad planning or implementation on any non-trivial task, use `$context-scope-guard` at `.agents/skills/context-scope-guard`.
- Identify the minimal relevant file set first, then work inside that scope.
- Prefer the nearest relevant files over broad discovery.
- Do not scan the whole repo unless the user explicitly asks for it or a concrete blocker makes it clearly necessary.
- Give an explicit justification before expanding scope.
- Treat repeated over-reading as behavior to avoid and correct by shrinking scope.

## Vault Knowledge Base

- Durable project knowledge lives in `ObsidianVault/`.
- Use `ObsidianVault/00 Start Here/Agent Navigation.md` to route docs and knowledge-base questions.
- Use `ObsidianVault/00 Start Here/Vault Specification.md` before changing vault structure, adding a top-level category, or refactoring documentation ownership.
- Do not read the whole vault unless the user explicitly asks for broad documentation review or a specific blocker proves the routing notes are insufficient.
- When adding durable knowledge, update `ObsidianVault/00 Start Here/Project Index.md` if the note should be discoverable from the vault root.

## Complex UI Overlay

- After scoping a complex UI/UX task, use `$ui-iteration-guard` at `.codex/skills/ui-iteration-guard`.
- Do not do large UI changes in one pass; plan first and move layer by layer.
- Visually validate each layer before continuing.
- If a major regression appears, step back to the last stable layer before moving forward again.

## When To Use The Skill

- UI work
- feature work
- refactors
- test work that is not already confined to one explicit file
- documentation-only work that is not already confined to one explicit file
- debugging when the affected area is not already obvious

Skip the skill only for tiny single-file edits where the file path and scope are already explicit.

## How To Use The Skill

- Run the skill first.
- Produce the minimal scope with: task type, likely entry points, allowed now, conditionally allowed if needed, out of scope, why this scope is sufficient, and when scope expansion is permitted.
- Plan and implement only within `Allowed now`.
- Expand into `Conditionally allowed if needed` only when a specific blocker appears and you state that blocker explicitly.
- Ask before widening beyond that scoped set unless the user already requested broader exploration.

## UI Launch

- For local UI testing in this repo, launch the ASP.NET host at `MiniPainterHub.Server/MiniPainterHub.Server.csproj`.
- Do not assume a page URL is reachable until that server is running.
- Treat the UI as dependent on the API/server host for manual browser checks unless a task explicitly says otherwise.

## Blocker Reporting

- If a major step fails or a debugging path is blocked, report it immediately in a short status update.
- State what you tried, why it failed, and what fallback you are using next.
- Do not silently switch to a fallback path when the failed step materially affects confidence, speed, or verification quality.
