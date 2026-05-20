---
type: adr
status: accepted
date: 2026-03-20
canonical: true
related_code:
  - MiniPainterHub.WebApp/Pages
  - MiniPainterHub.WebApp/Shared
  - e2e
related_notes:
  - ../UI_QUALITY_PLAYBOOK.md
---

# ADR: Rich Viewer Browser Validation

## Context
The post rich viewer redesign took longer than expected and needed multiple recovery passes before it reached acceptable quality. The first implementation pass had the right feature checklist on paper, but the real browser experience was still weak:

- the modal shell could scroll instead of the viewer comments panel
- portrait and panoramic images could open with broken centering
- scoped CSS for the stage used incorrect deep-selector syntax, so important fitbox rules were not consistently applied
- browser tests covered seeded happy paths, but did not yet assert the layout failures the user was actually seeing

## Decision
For UI-heavy work in MiniPainterHub, "feature exists" is not considered complete. Real browser validation and screenshot review are mandatory acceptance criteria for finishing the work.

The working rule is:

- verify the exact failing route or a deterministic reproduction before claiming progress
- treat layout, overlap, clipping, scrolling, and visual hierarchy defects as first-class failures
- use Playwright screenshots and DOM geometry checks early, not only after implementation
- add regression coverage for the exact browser behavior that failed, especially for aspect-ratio handling, panel scrolling, and active interaction states

## Consequences
This recovery produced a better viewer, but it also exposed the process mistake:

- the initial pass over-weighted code-level completeness and under-weighted rendered quality
- real browser measurement was introduced too late
- cross-cutting UI work that touches modal layout, transforms, marks, comments, and responsive behavior needs a tighter debug loop than logic-only features

Going forward, UI tasks of this kind should start with browser reproduction and should not be declared done until the rendered artifacts are visibly clean in the reviewed states.
