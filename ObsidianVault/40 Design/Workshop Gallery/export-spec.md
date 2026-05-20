# MiniPainterHub Figma Export Spec

## Purpose
This mockup package is the clean export target for rebuilding the UI in Figma before a second implementation pass.

## Grid and spacing
- Base spacing unit: `8px`
- Shell padding: `24 / 32px`
- Section gaps: `24px`
- Card internals: `16 / 24px`

## Typography
- Display / page titles: `Instrument Serif`
- UI / body / metadata: `Manrope`
- Hierarchy:
  - `H1`: 54 to 70
  - `H2`: 40
  - `H3`: 28 to 32
  - `Body`: 15 to 17
  - `Caption`: 12 to 13

## Color roles
- Canvas: `#F3EFE6`
- Surface: `#FCFBF7`
- Line: `#D9D0C2`
- Text: `#172127`
- Muted text: `#5D676D`
- Primary: `#1F5B52`
- Accent: `#B86A43`
- Highlight: `#C49B48`

## Radius and elevation
- XL panels: `30`
- Large cards: `24`
- Controls and tabs: `14`
- Surface shadow: soft, low-contrast, editorial rather than dashboard-heavy

## Frame list for Figma
1. `00 Foundations`
2. `01 Component Kit`
3. `02 Home Feed`
4. `03 Search`
5. `04 Post Details`
6. `05 Messages`
7. `06 Profile`
8. `07 Admin Reports`

## Components to recreate as variants
- Primary button
- Secondary button
- Search/input field
- Tab / filter chip
- Sidebar item
- Stat pill
- Post card
- Utility panel
- Conversation row
- Report queue row

## Auto-layout guidance
- Use horizontal auto-layout for:
  - top bar
  - button groups
  - filter rows
  - stat pill rows
- Use vertical auto-layout for:
  - rail sections
  - content stacks
  - cards
  - result lists
  - queue lists
- Avoid absolute positioning except for decorative gradients.
