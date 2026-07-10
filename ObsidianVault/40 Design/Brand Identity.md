# MiniPainterHub Brand Identity

Status: proposed logo system, ready for product review.

![MiniPainterHub logo system](<../50 Visual Assets/Brand/brand-board.svg>)

## Design idea

The identity is built from three product truths:

- **Miniature:** a simple display figure and plinth keep the subject recognizable without tying the brand to a specific game, faction, or sculpt.
- **Painter:** a fine brush rises through the composition and supplies the strongest directional movement.
- **Hub:** the circular orbit and three nodes frame the figure as part of a connected community.

The rounded atelier-green container makes the mark reliable as an avatar, navigation badge, launcher icon, or favicon. The editorial color balance keeps it aligned with the existing Workshop Gallery interface rather than making it feel like a separate product.

## Logo assets

| Use | Asset |
| --- | --- |
| Primary mark | [logo-mark.svg](<../50 Visual Assets/Brand/logo-mark.svg>) |
| Light-surface lockup | [logo-lockup.svg](<../50 Visual Assets/Brand/logo-lockup.svg>) |
| Dark-surface lockup | [logo-lockup-reversed.svg](<../50 Visual Assets/Brand/logo-lockup-reversed.svg>) |
| Browser and launcher icon | [favicon.svg](<../50 Visual Assets/Brand/favicon.svg>) |
| Review board | [brand-board.svg](<../50 Visual Assets/Brand/brand-board.svg>) |

SVG is the source of truth. Raster exports should be generated from these files only when a destination cannot use SVG.

## Color roles

| Role | Value | Logo use |
| --- | --- | --- |
| Atelier green | `#1F5B52` | primary container and the `Hub` wordmark |
| Clay | `#B86A43` | brush handle, miniature accent, and community node |
| Brass | `#C49B48` | ferrule, plinth, highlight, and reversed `Hub` wordmark |
| Ink | `#172127` | bristles, handle end, and primary wordmark |
| Surface | `#FCFBF7` | figure, orbit, and light reversed text |
| Canvas | `#F3EFE6` | recommended presentation background |

These values match the current product design tokens. Do not substitute more saturated greens, oranges, or golds for production use.

## Typography

The editable lockups use `Manrope` with `Inter` and `Segoe UI` fallbacks so the wordmark remains compatible with the product typography stack. Keep the name joined as `MiniPainterHub`; color may separate `Hub`, but spacing may not.

For a fixed external export, convert the wordmark to outlines after final approval. Keep the repository SVG source editable.

## Clear space and minimum size

Use at least one eighth of the mark width as clear space on every side. Nothing should overlap the rounded-square container.

- Primary mark: minimum `24px` digital size.
- Horizontal lockup: minimum `220px` digital width.
- Favicon: use the dedicated simplified asset at `16px`, `32px`, or `48px`.

At sizes below the lockup minimum, use the mark without the wordmark or tagline.

## Correct use

- Preserve the original aspect ratio.
- Use the primary lockup on light, low-detail surfaces.
- Use the reversed lockup on dark, low-detail surfaces.
- Keep the mark upright; the brush angle is part of the identity.
- Treat the mark as decorative when adjacent text already says `MiniPainterHub`; use an empty alt value in that case.
- Use meaningful alt text when the logo is the only visible identification.

## Avoid

- stretching, skewing, rotating, or cropping the mark,
- placing the primary wordmark on low-contrast backgrounds,
- recoloring individual elements,
- adding shadows, outlines, gradients, or game-specific miniature details,
- shrinking the full horizontal lockup into favicon or navigation-icon dimensions.

## Product integration after approval

1. Replace the temporary `MP` navbar badge with `logo-mark.svg` while retaining the adjacent accessible brand text.
2. Export or copy the approved browser icon into `MiniPainterHub.WebApp/wwwroot` and update `index.html` with an SVG favicon plus the existing PNG fallback.
3. Verify the shared shell at desktop and narrow viewports using the full UI review workflow.
4. Keep the asset paths stable after release so cached browser and documentation references do not break.

The first design PR intentionally keeps runtime integration separate from visual approval.
