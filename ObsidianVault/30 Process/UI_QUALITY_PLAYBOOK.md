# UI Quality Playbook

This playbook is the source of truth for UI review in MiniPainterHub.

It normalizes the ideas from `C:/Users/uslep/Downloads/UI_Agent_Playwright_Guide.md` into an enforceable repo workflow.

## 1) What Counts as a UI Change

Treat the task as a `UI Change` when it edits any of these:
- `MiniPainterHub.WebApp/Pages/**/*.razor`
- `MiniPainterHub.WebApp/Layout/**/*`
- `MiniPainterHub.WebApp/Shared/**/*`
- `MiniPainterHub.WebApp/wwwroot/css/**/*`
- `MiniPainterHub.WebApp/wwwroot/index.html`
- any design token, spacing, typography, navigation, panel, form, or responsive behavior

UI changes are not complete when the page merely compiles or renders.

## 2) Required Workflow

For every UI change:
1. Identify the page goal, main actions, and primary visual hierarchy before editing.
2. Implement the change with layout discipline and existing product constraints.
3. Resolve the required review scope with `npm --prefix e2e run ui-review:scope`.
4. Run the correct Playwright UI review command:
   - localized review: `npm --prefix e2e run test:ui-review`
   - full app sweep: `npm --prefix e2e run test:ui-review:full`
5. Run behavioral verification with `npm --prefix e2e run test:smoke`.
6. Review generated screenshots and manifest in `output/playwright/ui-review/`.
7. Fix obvious visual problems immediately.
8. Rerun the same Playwright review scope.
9. Repeat the fix-and-rerun loop until no obvious problems remain in the reviewed scope or a real blocker is documented.

Optional diagnostics:
- `./.venv/Scripts/python tools/ui_snapshot_panel.py`
- `python tools/ui_audit_browser_use.py`

These diagnostics do not replace Playwright review.

## 3) Scope Rules

Default localized review:
- changed route groups
- shared shell
- desktop and at least one narrow viewport

Automatic full-app sweep triggers:
- `MiniPainterHub.WebApp/wwwroot/css/app.css`
- `MiniPainterHub.WebApp/wwwroot/index.html`
- `MiniPainterHub.WebApp/Layout/**`
- shared shell or shared UI primitives
- auth shell or top-level navigation changes

The machine-readable trigger map lives in `e2e/ui-review.matrix.json`.

## 4) Route and State Matrix

Baseline shared shell and community:
- `/`
- `/posts/all`
- `/posts/top`
- `/search`
- `/feed/following`
- `/messages`
- `/profile`

Auth:
- `/login`
- `/register`

Post and content flows:
- `/posts/new`
- seeded `/posts/{id}`
- `/posts/mine`
- `/users/{id}`
- `/connections`

Admin:
- `/admin/moderation`
- `/admin/reports`
- `/admin/audit`
- `/admin/suspensions`

Required shell states:
- desktop with left panel open
- desktop with left panel collapsed
- mobile with the personal panel opened

Required state-specific review when the relevant area changes:
- empty
- populated
- auth-gated
- error or validation state when the change affects error handling or failed-user paths

## 5) Review Checklist

Check every reviewed screen for:
- layout balance and proportional shell sizing
- spacing rhythm and consistent grouping
- readable typography and clear hierarchy
- consistent button, card, form, and panel behavior
- no clipping, overlap, truncation, or accidental whitespace
- responsive behavior at the required viewports
- focus visibility and basic accessibility cues
- visual polish that looks intentional rather than incidental

If the page looks embarrassing or off-balance in the screenshot, it is not done.
If the issue is in scope for the current task, the default action is to fix it, not just report it.

## 6) Required Output

Every UI review run must produce:
- screenshots in `output/playwright/ui-review/`
- `manifest.json`
- `report.md`

Every UI delivery summary must include:
- routes, states, and viewports reviewed
- commands run
- whether the review was localized or full-app
- confirmation that the fix-and-rerun loop was completed
- any remaining visual follow-up worth doing, but only for items intentionally deferred or blocked
