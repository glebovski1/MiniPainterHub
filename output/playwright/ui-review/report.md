# UI Review Report

- Scope: `targeted`
- Groups: `shell, posts`
- Review command: `npm --prefix e2e run test:ui-review`
- Captures: `23`

## Reasons
- posts: MiniPainterHub.WebApp/Shared/Viewer/RichImageViewer.razor

## Captures
- shell-home-panel-open: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-open, desktop] -> `output/playwright/ui-review/01-shell-home-panel-open-desktop.png`
- shell-home-panel-collapsed: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-collapsed, desktop] -> `output/playwright/ui-review/02-shell-home-panel-collapsed-desktop.png`
- shell-home-mobile-panel-open: http://127.0.0.1:5176/ [mobile] [seed-user] [shell, community, mobile, panel-open] -> `output/playwright/ui-review/03-shell-home-mobile-panel-open-mobile.png`
- posts-new-auth-gated: http://127.0.0.1:5176/posts/new [desktop] [unauthenticated] [posts, desktop, auth-gated] -> `output/playwright/ui-review/04-posts-new-auth-gated-desktop.png`
- posts-new-composer: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, composer] -> `output/playwright/ui-review/05-posts-new-composer-desktop.png`
- posts-new-validation: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, validation] -> `output/playwright/ui-review/06-posts-new-validation-desktop.png`
- posts-detail-seeded: http://127.0.0.1:5176/posts/1 [desktop] [seed-user] [posts, desktop, details, populated] -> `output/playwright/ui-review/07-posts-detail-seeded-desktop.png`
- posts-detail-rich-viewer-closed: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, details, viewer-closed, populated] -> `output/playwright/ui-review/08-posts-detail-rich-viewer-closed-desktop.png`
- posts-detail-rich-viewer-open-portrait: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, portrait] -> `output/playwright/ui-review/09-posts-detail-rich-viewer-open-portrait-desktop.png`
- posts-detail-rich-viewer-open-fill: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, fill] -> `output/playwright/ui-review/10-posts-detail-rich-viewer-open-fill-desktop.png`
- posts-detail-rich-viewer-open-collapsed-rail: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, compact-rail] -> `output/playwright/ui-review/11-posts-detail-rich-viewer-open-collapsed-rail-desktop.png`
- posts-detail-rich-viewer-comments-tab: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, comments-tab] -> `output/playwright/ui-review/12-posts-detail-rich-viewer-comments-tab-desktop.png`
- posts-detail-rich-viewer-open-square: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, square] -> `output/playwright/ui-review/13-posts-detail-rich-viewer-open-square-desktop.png`
- posts-detail-rich-viewer-open-panorama: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, panorama] -> `output/playwright/ui-review/14-posts-detail-rich-viewer-open-panorama-desktop.png`
- posts-detail-rich-viewer-active-comment: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, active-comment-mark] -> `output/playwright/ui-review/15-posts-detail-rich-viewer-active-comment-desktop.png`
- posts-detail-rich-viewer-zoomed-pan: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, zoomed, panned] -> `output/playwright/ui-review/16-posts-detail-rich-viewer-zoomed-pan-desktop.png`
- posts-detail-rich-viewer-author-note-composer: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, author-note, composer] -> `output/playwright/ui-review/17-posts-detail-rich-viewer-author-note-composer-desktop.png`
- posts-detail-rich-viewer-loading: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, loading] -> `output/playwright/ui-review/18-posts-detail-rich-viewer-loading-desktop.png`
- posts-detail-rich-viewer-error: http://127.0.0.1:5176/posts/3 [desktop] [seed-user] [posts, desktop, viewer-open, error] -> `output/playwright/ui-review/19-posts-detail-rich-viewer-error-desktop.png`
- posts-detail-rich-viewer-closed-mobile: http://127.0.0.1:5176/posts/3 [mobile] [seed-user] [posts, mobile, details, viewer-closed, populated] -> `output/playwright/ui-review/20-posts-detail-rich-viewer-closed-mobile-mobile.png`
- posts-detail-rich-viewer-open-mobile-portrait: http://127.0.0.1:5176/posts/3 [mobile] [seed-user] [posts, mobile, viewer-open, portrait] -> `output/playwright/ui-review/21-posts-detail-rich-viewer-open-mobile-portrait-mobile.png`
- posts-detail-rich-viewer-open-mobile-panorama: http://127.0.0.1:5176/posts/3 [mobile] [seed-user] [posts, mobile, viewer-open, panorama] -> `output/playwright/ui-review/22-posts-detail-rich-viewer-open-mobile-panorama-mobile.png`
- posts-mine-desktop: http://127.0.0.1:5176/posts/mine [desktop] [seed-user] [posts, desktop, mine, populated] -> `output/playwright/ui-review/23-posts-mine-desktop-desktop.png`
