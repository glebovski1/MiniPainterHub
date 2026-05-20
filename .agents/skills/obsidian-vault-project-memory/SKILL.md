---
name: obsidian-vault-project-memory
description: Use this skill when working in a repository that uses an existing Obsidian vault as an internal project knowledge base. The skill helps Codex navigate the vault, retrieve only task-relevant knowledge, save durable project knowledge, adapt/refactor the vault structure, merge overlapping Markdown files, archive stale files, and maintain vault change logs with self-reflection to detect recurring bad patterns.
---

# Obsidian Vault Project Memory

Use this skill to treat an existing Obsidian vault as repository-local project memory while keeping context focused. Prefer plain Markdown and filesystem operations. Use the helper scripts in `scripts/` when they save time or reduce risk.

For detailed examples and structure patterns, read `references/vault-patterns.md` only when planning vault organization, merges, archive policy, or style cleanup.

## A. Vault Discovery

Never create a new vault unless no vault can be found and the user explicitly wants one. When no vault path is given, locate the vault in this order:

1. Read `.codex/vault-memory.json`.
2. Look for a folder containing `.obsidian/`.
3. Check common folders: `knowledge`, `vault`, `notes`, `docs/vault`, `obsidian`, `project-memory`.
4. Check README, AGENTS.md, AGENT.md, or docs that mention a vault path.

Use `scripts/vault_find.py` from the repository root for a safe first pass:

```bash
python .agents/skills/obsidian-vault-project-memory/scripts/vault_find.py
```

If multiple candidates exist, choose the one with `.obsidian/` only when it is clear. Otherwise report candidates and do not modify vault files until the correct vault is clear.

## B. Fast Navigation Workflow

For any non-trivial task:

1. Find the vault root.
2. Read only index, map, or navigation files first.
3. Search filenames, headings, and frontmatter for relevant terms.
4. Read only the smallest useful set of notes.
5. Create a compact retrieved-memory summary.
6. Continue the task using the summary, not pasted note contents.

Default maximum initial notes to read: 3-7. Do not read the whole vault unless the user asks for a full audit.

## C. Retrieval Workflow

Retrieve in this priority order:

1. ADRs and decisions.
2. Architecture maps.
3. Component or feature notes.
4. Workflow and testing notes.
5. Anti-patterns and best practices.
6. Session logs.
7. Scratch or research notes.

When retrieving:

- Search exact task terms.
- Search synonyms.
- Search related component names.
- Search related code paths.
- Prefer canonical notes over old session notes.
- Summarize relevant knowledge compactly.

## D. Saving Workflow

Before writing to the vault:

1. Decide whether the information is durable.
2. Search for an existing note to update.
3. Prefer updating or patching over creating new notes.
4. Preserve human-authored decisions.
5. Use Obsidian-style links if the vault uses them.
6. Add related links and code paths where useful.
7. Update indexes or maps only when necessary.
8. Log meaningful vault changes.

Save only durable knowledge:

- Architecture decisions.
- Domain rules.
- Feature behavior.
- Testing rules.
- Repo conventions.
- Known pitfalls.
- Integration setup.
- Important debugging lessons.
- Durable project constraints.
- Significant implementation summaries.

Do not save:

- Temporary thoughts.
- Every command run.
- Full chat transcripts.
- Noisy status updates.
- Small code edits with no future value.

## E. Structure Adaptation

Suggest or apply vault structure improvements only when useful. Possible folders:

- `_map/`
- `_architecture/`
- `_decisions/`
- `_workflows/`
- `_components/`
- `_features/`
- `_domain/`
- `_testing/`
- `_integrations/`
- `_research/`
- `_logs/`
- `_logs/vault-changes/`
- `_logs/reflections/`
- `_archive/`
- `_templates/`

Do not force this structure if the existing vault already has a clear, useful structure. Before changing structure, inspect the current index or map notes and preserve existing conventions.

## F. Markdown Refactoring

When refactoring Markdown files:

- Preserve meaning.
- Preserve human decisions.
- Improve headings only when they make retrieval easier.
- Remove duplicate wording.
- Keep links working.
- Update index or map files after moves or merges.
- Use small patches instead of full rewrites when possible.
- Log changed files.

Run `scripts/vault_check_links.py` after moves, merges, or broad link edits.

## G. Merge Workflow

When notes overlap:

1. Identify candidate notes.
2. Choose a canonical note using this priority:
   - explicitly marked canonical
   - linked from main index
   - newest verified durable note
   - most complete note
   - most stable filename
3. Extract unique durable content.
4. Merge useful content into the canonical note.
5. Mark unresolved contradictions instead of hiding them.
6. Archive duplicates or replace them with redirect stubs.
7. Update backlinks and indexes if possible.
8. Log the merge.

Redirect stub format:

```markdown
---
type: redirect
status: merged
merged_into: "[[Canonical Note]]"
date: YYYY-MM-DD
---

# Old Note Title

This note was merged into [[Canonical Note]] on YYYY-MM-DD.
```

## H. Delete And Archive Workflow

Never permanently delete by default. Default behavior is to move old or deleted notes to:

```text
_archive/deleted/YYYY-MM-DD/<original-relative-path>.md
```

Add this header to archived files:

```markdown
> Archived by Codex on YYYY-MM-DD.
> Reason: duplicate/stale/superseded by [[Canonical Note]].
```

Use `scripts/vault_archive_note.py` for archive moves. Permanent deletion requires explicit user instruction.

## I. Logging

Maintain logs under the vault:

- `_logs/vault-changes/YYYY-MM-DD.md`
- `_logs/reflections/YYYY-MM-DD.md`

If the vault already has a log convention, use the existing convention.

Use `scripts/vault_log_change.py` to append vault change entries. Format entries like this:

```markdown
## HH:mm — <short change title>

- Type: create | update | merge | archive | rename | structure | reflection
- Files changed:
  - `relative/path.md`
- Reason:
  - <why the change was needed>
- Summary:
  - <what changed>
- Source:
  - user request | code change | vault cleanup | task completion | conflict resolution
- Follow-up:
  - <optional>
```

## J. Self-Reflection Workflow

After meaningful vault edits, run a self-reflection pass. If Codex subagents or custom agents are available and the user explicitly asks for subagents, use a focused self-reflection subagent. Otherwise perform the reflection directly.

Reflection checks:

1. Did I create duplicate notes instead of updating existing ones?
2. Did I save temporary information as durable memory?
3. Did I overwrite human-authored decisions?
4. Did I read too much of the vault?
5. Did I miss index or map updates?
6. Did I archive instead of permanent-delete?
7. Did I preserve links and backlinks?
8. Did I create useful future memory?
9. Did I leave contradictions unresolved without marking them?
10. Did I follow vault style?

Use `scripts/vault_reflect.py` to create the reflection log when practical. Reflection log format:

```markdown
## HH:mm — Reflection on <task/change>

### What went well
- ...

### Possible bad patterns detected
- ...

### Fixes applied now
- ...

### Fixes recommended later
- ...

### Context pollution score
- 1-5, where 1 = focused and 5 = too much context loaded

### Memory quality score
- 1-5, where 1 = noisy and 5 = durable/useful
```

## K. Obsidian CLI

Use plain filesystem operations by default. Check if Obsidian CLI exists before using it. Use Obsidian CLI only when it clearly helps with:

- Obsidian-native search.
- Reading the active file.
- Listing tags.
- Creating notes from Obsidian templates.
- Appending to daily notes.
- Checking Obsidian-specific behavior.

If unavailable, fall back to normal file operations.

## L. Output Format

When using this skill, report:

```markdown
## Vault memory used
- Read: `path/a.md`, `path/b.md`
- Key constraints found:
  - ...

## Changes made
- Updated: `path/x.md`
- Created: `path/y.md`
- Archived: `path/z.md`

## Reflection
- Bad patterns found: none | list
- Context pollution score: 1-5
- Memory quality score: 1-5

## Follow-ups
- ...
```

If no vault files changed, say so. If the vault path is unclear, report candidates and stop before modifying vault files.
