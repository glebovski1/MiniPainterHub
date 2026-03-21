---
name: "ui-iteration-guard"
description: "Use for complex UI/UX implementation in MiniPainterHub: redesigns, layout refactors, responsive fixes, modal/viewer/panel/sidebar work, overlap/alignment/overflow bugs, or implementing screens from mockups or screenshots. Forces layered implementation, visual verification after each layer, and rollback to the last stable layer when regressions appear."
---

# UI Iteration Guard

Use this skill for difficult UI work that should not be implemented in one pass.

In this repo, pair it with `AGENT.md` and `.agents/skills/minipainterhub-ui-quality-gate/SKILL.md`.

## When to use

- Complex UI/UX implementation
- Redesigns or layout refactors
- Responsive fixes across desktop and narrow viewports
- Modal, viewer, sidebar, drawer, inspector, or panel work
- Overlap, alignment, clipping, overflow, or unintended scroll issues
- Building UI from mockups, screenshots, or detailed visual references

## Rules

- Plan before coding.
- Build in small layers.
- Visually verify every layer before moving on.
- If a major regression appears, stop forward progress and return to the last stable layer.
- Do not mark the task complete from code inspection alone.

## Workflow

1. Plan the screen before editing.
   - Identify the target screen and major layout regions.
   - Name the likely files to edit.
   - Break the work into the smallest useful layers.
   - Start with the first layer that creates a stable shell.
2. Build in layers.
   - Layer 1: structural shell
   - Layer 2: sizing and constraints
   - Layer 3: primary alignment and spacing
   - Layer 4: core interactions
   - Layer 5: visual polish
   - Layer 6: edge cases and responsive cleanup
3. Verify after every layer.
   - Prefer the repo Playwright flow:
     - `npm --prefix e2e run ui-review:scope`
     - `npm --prefix e2e run test:ui-review`
     - `npm --prefix e2e run test:ui-review:full`
     - `npm --prefix e2e run test:smoke`
   - Review screenshots in `output/playwright/ui-review/`.
   - If Playwright is unavailable or blocked, use screenshots or the nearest project visual verification method and say what blocked the normal flow.
   - Check for overlap, clipping, unintended scroll, broken centering, panel collapse, inaccessible controls, and layout shifts caused by hover, focus, or animation.
4. Stop on regressions.
   - Do not keep layering new UI on top of a broken state.
   - Identify the last stable layer.
   - Decide whether the regression came from structure, constraints, interaction, or polish.
   - Revert or rewrite only the problematic layer.
   - Re-verify before resuming forward work.
5. Keep progress updates concise.
   - Include the current layer.
   - State what changed.
   - State what was visually checked.
   - State whether a regression was found.
   - State whether you are proceeding or stepping back.

## Completion criteria

- The requested UI is implemented.
- Critical interactions work.
- Major layout and responsive issues are resolved.
- Visual verification happened during intermediate layers and at the end.
- Any major regression was fixed by returning to the last stable layer before continuing.
