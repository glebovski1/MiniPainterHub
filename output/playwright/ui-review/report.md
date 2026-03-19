# UI Review Report

- Scope: `targeted`
- Groups: `shell, posts`
- Review command: `npm --prefix e2e run test:ui-review`
- Captures: `8`

## Reasons
- posts: MiniPainterHub.WebApp/Pages/Posts/PostDetails.razor

## Captures
- shell-home-panel-open: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-open, desktop] -> `output/playwright/ui-review/01-shell-home-panel-open-desktop.png`
- shell-home-panel-collapsed: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-collapsed, desktop] -> `output/playwright/ui-review/02-shell-home-panel-collapsed-desktop.png`
- shell-home-mobile-panel-open: http://127.0.0.1:5176/ [mobile] [seed-user] [shell, community, mobile, panel-open] -> `output/playwright/ui-review/03-shell-home-mobile-panel-open-mobile.png`
- posts-new-auth-gated: http://127.0.0.1:5176/posts/new [desktop] [unauthenticated] [posts, desktop, auth-gated] -> `output/playwright/ui-review/04-posts-new-auth-gated-desktop.png`
- posts-new-composer: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, composer] -> `output/playwright/ui-review/05-posts-new-composer-desktop.png`
- posts-new-validation: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, validation] -> `output/playwright/ui-review/06-posts-new-validation-desktop.png`
- posts-detail-seeded: http://127.0.0.1:5176/posts/1 [desktop] [seed-user] [posts, desktop, details, populated] -> `output/playwright/ui-review/07-posts-detail-seeded-desktop.png`
- posts-mine-desktop: http://127.0.0.1:5176/posts/mine [desktop] [seed-user] [posts, desktop, mine, populated] -> `output/playwright/ui-review/08-posts-mine-desktop-desktop.png`
