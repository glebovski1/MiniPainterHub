---
name: "minipainterhub-ui-quality-gate"
description: "Use for any MiniPainterHub UI task involving Razor pages, CSS, layout, navigation, shared UI primitives, or responsive behavior. Enforces Playwright visual review, route/state coverage, and screenshot-based acceptance before UI work is considered done."
---

# MiniPainterHub UI Quality Gate

## When to use
- Any MiniPainterHub UI or UX task
- Any change to layout, spacing, typography, CSS, shell, forms, navigation, cards, states, or responsiveness

## Workflow
1. Read `ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md` for the repo policy.
2. Resolve UI review scope with `npm --prefix e2e run ui-review:scope`.
3. Run the correct Playwright review command:
   - `npm --prefix e2e run test:ui-review`
   - `npm --prefix e2e run test:ui-review:full`
4. Run `npm --prefix e2e run test:smoke`.
5. Review screenshots in `output/playwright/ui-review/`.
6. Fix any obvious visual issues that are in scope for the task.
7. Rerun the same Playwright review scope.
8. Repeat until the reviewed scope is clean or a real blocker is documented.

## References
- Visual review checklist: `references/visual-review-checklist.md`
- Route/state matrix: `references/route-state-matrix.md`

## Vault hooks
- Durable UI policy belongs in `ObsidianVault/30 Process/UI_QUALITY_PLAYBOOK.md`.
- Durable design artifacts belong in `ObsidianVault/40 Design/`.
- Portfolio or review screenshots belong in `ObsidianVault/50 Visual Assets/`.
- Use `ObsidianVault/00 Start Here/Agent Navigation.md` before widening a UI task into broader documentation work.
