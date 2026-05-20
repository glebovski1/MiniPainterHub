# Vault Patterns

This reference gives optional patterns for project-memory vaults. Use it only when the task needs vault organization, merges, archive decisions, or examples. Do not load it for routine retrieval when index notes are enough.

## Recommended Organization

Prefer the vault's existing structure when it is clear and searchable. If structure is weak or missing, these folders are useful:

- `_map/` for map-of-content notes and routing.
- `_architecture/` for architecture summaries and system maps.
- `_decisions/` for ADRs and durable choices.
- `_workflows/` for repeatable engineering and maintenance procedures.
- `_components/` for component or subsystem notes.
- `_features/` for feature behavior and product rules.
- `_domain/` for domain language and business rules.
- `_testing/` for test strategy, fixtures, and known verification paths.
- `_integrations/` for external services and setup.
- `_research/` for evaluated options that may still be useful.
- `_logs/vault-changes/` for meaningful vault edits.
- `_logs/reflections/` for Codex self-reflection after vault edits.
- `_archive/` for stale, duplicate, or superseded material.
- `_templates/` for reusable note templates.

Do not rename or move an existing convention just to match these names. Structure changes need a concrete retrieval, duplication, or maintenance problem.

## What To Save

Save durable information that a future contributor or Codex session will likely need:

- Architecture decisions and their tradeoffs.
- Domain rules and canonical vocabulary.
- Feature behavior, invariants, and edge cases.
- Testing strategy and stable verification commands.
- Repo conventions and local workflow rules.
- Known pitfalls and debugging lessons.
- Integration setup details that are not obvious from code.
- Significant implementation summaries after meaningful changes.

## What Not To Save

Do not save information that creates noise:

- Full chat transcripts.
- Every command run.
- Temporary hypotheses that were not validated.
- Status updates with no future value.
- Small implementation details already clear in nearby code.
- Secrets, credentials, tokens, or private runtime data.

## Merge Example

Scenario: `Gallery Upload Notes.md` and `Upload Pipeline.md` both describe durable image upload behavior.

1. Check which note is linked from the main index.
2. Pick the linked or most complete note as canonical.
3. Move unique durable behavior into the canonical note.
4. Preserve contradictions under a heading such as `## Open Contradictions`.
5. Replace the duplicate with a redirect stub or archive it.
6. Update index and backlinks when feasible.
7. Log the merge.

Redirect stub:

```markdown
---
type: redirect
status: merged
merged_into: "[[Upload Pipeline]]"
date: 2026-04-19
---

# Gallery Upload Notes

This note was merged into [[Upload Pipeline]] on 2026-04-19.
```

## Archive Example

Use archive-first deletion for stale notes:

```text
_archive/deleted/2026-04-19/old/path/Note.md
```

Archived file header:

```markdown
> Archived by Codex on 2026-04-19.
> Reason: stale; superseded by [[Upload Pipeline]].
```

Permanent deletion requires explicit user instruction.

## Logging Example

```markdown
## 14:35 — Merge upload behavior notes

- Type: merge
- Files changed:
  - `_features/Upload Pipeline.md`
  - `_archive/deleted/2026-04-19/_research/Gallery Upload Notes.md`
- Reason:
  - Two notes described the same upload pipeline with partial overlap.
- Summary:
  - Consolidated durable behavior into the feature note and archived the duplicate.
- Source:
  - vault cleanup
- Follow-up:
  - Recheck backlinks after next vault audit.
```

## Context Pollution Control

Use a retrieve-summarize-continue loop:

1. Find the vault root.
2. Read one navigation or index note.
3. Search filenames, headings, and frontmatter before reading bodies.
4. Open 3-7 likely notes at first.
5. Summarize durable facts into a compact retrieved-memory summary.
6. Work from the summary unless a precise quote or edit target is needed.

Bad pattern: reading every note in a folder because one note might matter.

Good pattern: search for exact task terms, related code paths, synonyms, and component names; open only the highest-signal notes.

## Good And Bad Note Updates

Good update:

- Adds a short `## Known Pitfall` section to an existing canonical note.
- Links the affected code path.
- Mentions the verification command that proved the behavior.
- Logs the update.

Bad update:

- Creates a new session note with a long transcript.
- Duplicates a decision already captured in an ADR.
- Rewrites a human-authored decision without marking why it changed.
- Leaves broken links after moving a note.

## Prompt Examples

Use this skill to retrieve project memory:

```text
Use $obsidian-vault-project-memory to check the vault for existing decisions about image upload storage before changing the server service.
```

Use this skill after meaningful code changes:

```text
Use $obsidian-vault-project-memory to save durable testing guidance from this fix. Update existing notes if possible and log the vault change.
```

Use this skill to audit structure:

```text
Use $obsidian-vault-project-memory to audit the existing vault structure. Read only map/index files first, report duplicate or stale areas, and propose changes before editing.
```

Use this skill to merge overlap:

```text
Use $obsidian-vault-project-memory to merge overlapping notes about gallery filtering. Preserve decisions, archive duplicates by default, and run reflection afterward.
```
