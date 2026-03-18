# UI Review Report

- Scope: `full`
- Groups: `shell, auth, community, search, posts, following, profile, messages, connections, admin`
- Review command: `npm --prefix e2e run test:ui-review:full`
- Captures: `31`

## Reasons
- Forced full UI review run.

## Captures
- shell-home-panel-open: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-open, desktop] -> `output/playwright/ui-review/01-shell-home-panel-open-desktop.png`
- shell-home-panel-collapsed: http://127.0.0.1:5176/ [desktop] [seed-user] [shell, community, panel-collapsed, desktop] -> `output/playwright/ui-review/02-shell-home-panel-collapsed-desktop.png`
- shell-home-mobile-panel-open: http://127.0.0.1:5176/ [mobile] [seed-user] [shell, community, mobile, panel-open] -> `output/playwright/ui-review/03-shell-home-mobile-panel-open-mobile.png`
- auth-login-desktop: http://127.0.0.1:5176/login [desktop] [unauthenticated] [auth, desktop, entry] -> `output/playwright/ui-review/04-auth-login-desktop-desktop.png`
- auth-login-error-desktop: http://127.0.0.1:5176/login [desktop] [unauthenticated] [auth, desktop, error] -> `output/playwright/ui-review/05-auth-login-error-desktop-desktop.png`
- auth-register-desktop: http://127.0.0.1:5176/register [desktop] [unauthenticated] [auth, desktop, entry] -> `output/playwright/ui-review/06-auth-register-desktop-desktop.png`
- auth-login-mobile: http://127.0.0.1:5176/login [mobile] [unauthenticated] [auth, mobile, entry] -> `output/playwright/ui-review/07-auth-login-mobile-mobile.png`
- community-home-desktop: http://127.0.0.1:5176/ [desktop] [seed-user] [community, desktop, populated] -> `output/playwright/ui-review/08-community-home-desktop-desktop.png`
- community-archive-desktop: http://127.0.0.1:5176/posts/all [desktop] [seed-user] [community, desktop, archive] -> `output/playwright/ui-review/09-community-archive-desktop-desktop.png`
- community-top-posts-desktop: http://127.0.0.1:5176/posts/top [desktop] [seed-user] [community, desktop, showcase] -> `output/playwright/ui-review/10-community-top-posts-desktop-desktop.png`
- search-post-results-desktop: http://127.0.0.1:5176/search?q=seeded&tab=posts [desktop] [seed-user] [search, desktop, posts-results] -> `output/playwright/ui-review/11-search-post-results-desktop-desktop.png`
- search-user-results-desktop: http://127.0.0.1:5176/search?q=user&tab=users [desktop] [seed-user] [search, desktop, users-results] -> `output/playwright/ui-review/12-search-user-results-desktop-desktop.png`
- posts-new-auth-gated: http://127.0.0.1:5176/posts/new [desktop] [unauthenticated] [posts, desktop, auth-gated] -> `output/playwright/ui-review/13-posts-new-auth-gated-desktop.png`
- posts-new-composer: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, composer] -> `output/playwright/ui-review/14-posts-new-composer-desktop.png`
- posts-new-validation: http://127.0.0.1:5176/posts/new [desktop] [seed-user] [posts, desktop, validation] -> `output/playwright/ui-review/15-posts-new-validation-desktop.png`
- posts-detail-seeded: http://127.0.0.1:5176/posts/1 [desktop] [seed-user] [posts, desktop, details, populated] -> `output/playwright/ui-review/16-posts-detail-seeded-desktop.png`
- posts-mine-desktop: http://127.0.0.1:5176/posts/mine [desktop] [seed-user] [posts, desktop, mine, populated] -> `output/playwright/ui-review/17-posts-mine-desktop-desktop.png`
- following-empty-desktop: http://127.0.0.1:5176/feed/following [desktop] [seed-user] [following, desktop, empty] -> `output/playwright/ui-review/18-following-empty-desktop-desktop.png`
- following-populated-desktop: http://127.0.0.1:5176/feed/following [desktop] [seed-user] [following, desktop, populated] -> `output/playwright/ui-review/19-following-populated-desktop-desktop.png`
- profile-empty-desktop: http://127.0.0.1:5176/profile [desktop] [seed-user] [profile, desktop, empty] -> `output/playwright/ui-review/20-profile-empty-desktop-desktop.png`
- profile-populated-desktop: http://127.0.0.1:5176/profile [desktop] [seed-user] [profile, desktop, populated] -> `output/playwright/ui-review/21-profile-populated-desktop-desktop.png`
- profile-public-desktop: http://127.0.0.1:5176/users/0c249dbc-198b-472c-b091-9b1296165b4b [desktop] [seed-user] [profile, desktop, public, populated] -> `output/playwright/ui-review/22-profile-public-desktop-desktop.png`
- messages-empty-desktop: http://127.0.0.1:5176/messages [desktop] [seed-user] [messages, desktop, empty] -> `output/playwright/ui-review/23-messages-empty-desktop-desktop.png`
- messages-populated-desktop: http://127.0.0.1:5176/messages [desktop] [seed-user] [messages, desktop, populated] -> `output/playwright/ui-review/24-messages-populated-desktop-desktop.png`
- messages-populated-mobile: http://127.0.0.1:5176/messages [mobile] [seed-user] [messages, mobile, populated] -> `output/playwright/ui-review/25-messages-populated-mobile-mobile.png`
- connections-populated-desktop: http://127.0.0.1:5176/connections [desktop] [seed-user] [connections, desktop, populated] -> `output/playwright/ui-review/26-connections-populated-desktop-desktop.png`
- admin-reports-empty-desktop: http://127.0.0.1:5176/admin/reports [desktop] [admin] [admin, desktop, reports, empty] -> `output/playwright/ui-review/27-admin-reports-empty-desktop-desktop.png`
- admin-moderation-desktop: http://127.0.0.1:5176/admin/moderation [desktop] [admin] [admin, desktop, moderation] -> `output/playwright/ui-review/28-admin-moderation-desktop-desktop.png`
- admin-audit-desktop: http://127.0.0.1:5176/admin/audit [desktop] [admin] [admin, desktop, audit] -> `output/playwright/ui-review/29-admin-audit-desktop-desktop.png`
- admin-suspensions-desktop: http://127.0.0.1:5176/admin/suspensions [desktop] [admin] [admin, desktop, suspensions] -> `output/playwright/ui-review/30-admin-suspensions-desktop-desktop.png`
- admin-reports-populated-desktop: http://127.0.0.1:5176/admin/reports [desktop] [admin] [admin, desktop, reports, populated] -> `output/playwright/ui-review/31-admin-reports-populated-desktop-desktop.png`
