---
name: "context-scope-guard"
description: "Analyze a MiniPainterHub task and produce the smallest justified repository file scope before planning or implementation. Use for non-trivial bug fixes, UI changes, feature work, refactors, test work, or unclear debugging when the affected files are not already explicit. Creates Allowed now, Conditionally allowed if needed, and Out of scope buckets and requires explicit justification before scope expansion."
---

# Context Scope Guard

Use this skill to minimize context before planning or implementation.

This workflow strongly guides context usage. It is not a guaranteed low-level sandbox unless external tooling enforces one.

## Activation Guidance

Use this skill when:
- the task is non-trivial
- the request could touch multiple files, layers, or projects
- the affected file is not already explicit
- the task is UI work, feature work, refactoring, test work, documentation-only work, or unclear debugging

Skip this skill only for tiny single-file edits where the file path and change surface are already explicit.

## Procedure

1. Classify the task:
   - bug fix
   - UI change
   - refactor
   - test work
   - feature work
   - documentation-only work
2. Inspect only enough repo structure to route the task.
   - Start with the user prompt and any explicitly named files.
   - If no file is named, inspect only the smallest relevant top-level area or run a targeted file search.
   - For documentation or knowledge-base work, route through `ObsidianVault/00 Start Here/Agent Navigation.md` and `ObsidianVault/00 Start Here/Vault Specification.md` before opening individual vault notes.
   - Do not start with broad docs, whole-project reads, or repo-wide code scanning.
3. Identify likely entry points.
   - Prefer the nearest route, page, component, controller, service, test, or doc that appears to own the behavior.
   - In this repo, prefer:
     - `MiniPainterHub.WebApp/Pages`, `MiniPainterHub.WebApp/Shared`, `MiniPainterHub.WebApp/Layout`, and `MiniPainterHub.WebApp/wwwroot` for UI work
     - `MiniPainterHub.Server/Controllers`, `MiniPainterHub.Server/Services`, `MiniPainterHub.Server/Data`, and `MiniPainterHub.Server/Program.cs` for server work
     - `MiniPainterHub.Common` for shared contracts
     - `MiniPainterHub.WebApp.Tests`, `MiniPainterHub.Server.Tests`, and `e2e` only when the task needs test or UI verification context
4. Build a minimal candidate scope.
   - Start with the most likely entry file.
   - Add only directly supporting files required to understand or change the behavior:
     - nearest component or partial
     - nearest service or interface pair
     - local style file
     - matching test file
     - exact shared DTO or contract used by the flow
   - Bias toward 1-5 files, not whole folders.
5. Bucket the files:
   - `Allowed now`: files needed immediately
   - `Conditionally allowed if needed`: files that may be opened only if a specific blocker appears
   - `Out of scope`: files, folders, or projects that should not be read for this task
6. State why the current scope is sufficient.
7. Work only inside `Allowed now`.
8. Expand scope only when blocked.
   - Before reading another file, write a short justification that names:
     - the blocker
     - the exact file needed next
     - why the current scope is insufficient
   - If expansion crosses into a new layer, project, or broad doc set, ask the user before widening unless the request already called for broader exploration.
9. Reissue the scope after every expansion.
   - Keep the new scope minimal.
   - Remove files that are no longer needed.

## Output Format

Use this exact format before planning or implementation:

```text
Task type: <bug fix | UI change | refactor | test work | feature work | documentation-only work>

Likely entry points:
- <path>
- <path>

Allowed now:
- <path> - <why needed now>
- <path> - <why needed now>

Conditionally allowed if needed:
- <path> - only if <specific blocker>
- <path> - only if <specific blocker>

Out of scope:
- <path or area> - <why out of scope>
- <path or area> - <why out of scope>

Why this scope is sufficient:
- <1-3 concrete reasons>

When scope expansion is permitted:
- <explicit rule 1>
- <explicit rule 2>
```

## Forbidden Behavior

- Do not start with repo-wide scanning.
- Do not read entire projects or large docs "just in case".
- Do not open multiple sibling files before proving they are part of the active flow.
- Do not pull in test suites, migrations, or architecture docs unless the task requires them.
- Do not treat `Conditionally allowed if needed` as pre-approved.
- Do not keep stale files in scope after the blocker is resolved.
- Treat unnecessary context expansion as an error and shrink the scope.

## Escalation Rules

- Move a file from `Conditionally allowed if needed` to `Allowed now` only after stating the blocker.
- Ask before reading beyond both lists when:
  - the next file is in a different project or layer
  - the next step would require broad scanning
  - the task is shifting from local change to architecture exploration
- Repo-wide search or architecture exploration is allowed only when the user explicitly asks for it or when a named blocker cannot be resolved from the nearest relevant files and no smaller search surface exists.

## Examples

### Bug Fix

Request: "Fix avatar upload returning 500."

```text
Task type: bug fix

Likely entry points:
- MiniPainterHub.Server/Controllers/<matching upload controller>
- MiniPainterHub.Server/Services/Images/<matching upload service>

Allowed now:
- MiniPainterHub.Server/Controllers/<matching upload controller> - owns the failing endpoint
- MiniPainterHub.Server/Services/Images/<matching upload service> - likely contains the broken behavior
- MiniPainterHub.Server.Tests/<matching test> - nearest verification target if one exists

Conditionally allowed if needed:
- MiniPainterHub.Common/<matching DTO> - only if request or response shape is involved
- MiniPainterHub.Server/Program.cs - only if middleware or configuration is implicated

Out of scope:
- MiniPainterHub.WebApp/Pages - UI is not part of a server-side 500 unless evidence says otherwise
- e2e - browser flow is not needed until the server fix path is understood

Why this scope is sufficient:
- The failing behavior should route through the endpoint and its service.
- The nearest server test is enough to verify the fix before widening scope.

When scope expansion is permitted:
- Expand to the shared DTO only if the controller or service points to a contract mismatch.
- Ask before pulling in client code or broad configuration files.
```

### UI Change

Request: "Tighten spacing on the gallery details page."

```text
Task type: UI change

Likely entry points:
- MiniPainterHub.WebApp/Pages/<matching gallery page>
- MiniPainterHub.WebApp/Shared/<matching nested component>

Allowed now:
- MiniPainterHub.WebApp/Pages/<matching gallery page> - primary UI entry point
- MiniPainterHub.WebApp/Shared/<matching nested component> - direct render dependency if the spacing lives there
- MiniPainterHub.WebApp/wwwroot/<matching style file> - nearest style source if the page uses shared CSS

Conditionally allowed if needed:
- MiniPainterHub.WebApp/Layout/<matching layout file> - only if spacing is inherited from layout
- e2e/<matching review spec> - only if verification needs route coverage or responsive confirmation
- MiniPainterHub.WebApp.Tests/<matching component test> - only if a render-state test needs adjustment

Out of scope:
- MiniPainterHub.Server - server logic is unrelated to a local spacing change
- unrelated pages and components - no broad UI sweep without evidence

Why this scope is sufficient:
- The page, its direct component, and its nearest style source normally explain local spacing.
- Verification can stay local unless layout inheritance or route coverage becomes the blocker.

When scope expansion is permitted:
- Expand to layout only if the page-level files do not own the spacing.
- Ask before widening to unrelated UI areas.
```

### Documentation-Only Work

Request: "Update the contributor note for UI review."

```text
Task type: documentation-only work

Likely entry points:
- ObsidianVault/00 Start Here/Agent Navigation.md
- ObsidianVault/00 Start Here/Vault Specification.md
- ObsidianVault/<matching target note>
- ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md

Allowed now:
- ObsidianVault/00 Start Here/Agent Navigation.md - route the docs task to the correct source of truth
- ObsidianVault/00 Start Here/Vault Specification.md - verify ownership rules before changing vault structure
- ObsidianVault/<matching target note> - file being edited
- ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md - nearest referenced source of truth if needed

Conditionally allowed if needed:
- AGENT.md - only if the doc statement needs a workflow cross-check

Out of scope:
- source code projects - not needed unless the doc makes a code-level claim

Why this scope is sufficient:
- The target doc and one workflow reference are enough for a wording or policy update.

When scope expansion is permitted:
- Expand to AGENT.md only if the documentation change needs wording alignment.
- Ask before reading code to support a docs-only task.
```
