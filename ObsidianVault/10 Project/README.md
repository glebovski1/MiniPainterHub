# Roll & Paint / MiniPainterHub

Roll & Paint is a full-stack social platform for miniature painters. MiniPainterHub is the repository and application name; Roll & Paint is the current product identity. The product combines image-first post publishing, paint recipes, hobby projects, guides, discovery, direct messaging, and admin moderation in one .NET 10 application.

This repository is portfolio-oriented as much as it is product-oriented: it demonstrates end-to-end work across API design, Blazor UI, authentication, real-time messaging, media processing, project workflows, moderation, SEO, and automated quality gates.

The screenshot tour was refreshed on August 13, 2026 with Playwright against an isolated development database seeded from the real `assets/dev-seed` avatars and miniature images. The curated images therefore show the current Roll & Paint shell and real seeded artwork, not the checkerboard fixtures used by some UI regression scenarios.

## What It Demonstrates

- Image-backed post publishing with technique tags, comments, likes, and richer post detail flows
- JWT authentication with ASP.NET Core Identity
- Public profiles, following graph, connections, and following feed
- Cross-entity search for posts, users, and tags
- Direct messaging with SignalR-backed conversation flows
- Reporting, inline moderation, audit logging, and user suspension tools
- Rich image viewing with anchored comment-mark context
- Deterministic development seeding for realistic demo data
- A test pyramid that spans unit, component, integration, and browser smoke coverage
- A browser-reviewed UI refresh across feed, discovery, messaging, and admin surfaces

## Screenshot Tour

The tour below reflects the current atelier shell, painter workflow, close-up viewer, discovery, messaging, and moderation surfaces.

### 1. Latest feed

The home feed uses the Roll & Paint atelier shell, a left studio hub, and image-first post cards with quick access to tags, comments, likes, and project context.

![Latest feed](<../50 Visual Assets/Portfolio Screenshots/01-home-feed.png>)

### 2. Post composer

The two-column composer captures the story, miniature, difficulty, time spent, techniques, paints used, tags, optional hobby project, and image set while keeping a live readability preview visible.

![Post composer](<../50 Visual Assets/Portfolio Screenshots/02-create-post.png>)

### 3. Post details

The post details page combines the artwork, recipe context, tag metadata, project context, close-up entry point, and inline conversation before opening the viewer overlay.

![Post details](<../50 Visual Assets/Portfolio Screenshots/03-post-details.png>)

### 4. Rich viewer

The rich viewer adds a dedicated dark stage, fit/fill/zoom controls, pan and fullscreen support, and an information/comments panel for post metadata and anchored discussion.

![Rich viewer](<../50 Visual Assets/Portfolio Screenshots/04-rich-viewer.png>)

### 5. Discovery search

Search pivots across posts, projects, users, and tags inside the same visual shell, with clearer grouping and result summaries for a technique query.

![Search results](<../50 Visual Assets/Portfolio Screenshots/05-search-discovery.png>)

### 6. Direct messages

The messaging surface pairs the inbox with a larger reading pane, unread state, thread separation, and an explicit empty state that guides painters into profile-based conversations.

![Direct messages](<../50 Visual Assets/Portfolio Screenshots/06-messages.png>)

### 7. Moderation reports queue

Admins can review posts, comments, and report signals from one moderation inbox, filter by time/type/state, inspect targets, hide content, mark signals reviewed, and manage author restrictions.

![Reports queue](<../50 Visual Assets/Portfolio Screenshots/07-admin-reports.png>)

## Feature Overview

### Painter experience

- Register and sign in with JWT-based authentication
- Create image-first posts with one or more images, paint recipes, and reusable technique tags
- Link ordinary updates to hobby projects and follow project diary/showcase progress
- Comment on posts and like or unlike them
- Browse posts through an image-first detail experience with stronger visual emphasis on artwork and a rich close-up viewer
- Read and publish painting guides and browse source-linked hobby news
- Browse public profiles with follower and following counts
- Follow painters and browse a following-only feed
- Manage your own profile, display name, bio, and avatar
- Open direct-message conversations with other users
- Search across posts, projects, users, and tags from global navigation

### Trust, safety, and admin workflows

- Report posts, comments, and user profiles
- Review posts, comments, and reports from a consolidated moderation inbox
- Hide and restore posts and comments without hard deletion
- Use inline moderation actions and visibility-aware review flows in the UI
- Suspend and unsuspend users
- Inspect an audit log of moderation actions
- Support maintenance-mode bypass flows for controlled access scenarios

### Engineering and platform capabilities

- ASP.NET Core API + Blazor WebAssembly frontend
- Entity Framework Core with SQL Server persistence
- ASP.NET Core Identity for account management
- JWT bearer auth between the WebAssembly client and the API
- SignalR for direct-message updates
- Image processing with max, preview, and thumbnail variants; local image storage in development and Azure Blob storage in non-development environments
- Development seed commands for realistic users, posts, profiles, follows, comments, avatars, and direct messages
- SEO metadata, canonical URLs, Open Graph, JSON-LD, and production deployment configuration
- Browser-reviewed UI changes backed by Playwright smoke and full UI-review coverage

## Architecture

### Stack

- Backend: ASP.NET Core 10, Web API controllers, SignalR, ProblemDetails, Identity
- Frontend: Blazor WebAssembly, Bootstrap-based UI, typed client services
- Data: Entity Framework Core, SQL Server, shared DTO contracts in `MiniPainterHub.Common`
- Media: ImageSharp-backed processing plus local or Azure-backed storage

### Solution layout

- `MiniPainterHub.Server`: API, auth, SignalR hub, EF Core, seeding, moderation logic
- `MiniPainterHub.WebApp`: Blazor WebAssembly client and page-level UI flows
- `MiniPainterHub.Common`: shared DTOs and contracts
- `MiniPainterHub.Server.Tests`: backend service and API-focused tests
- `MiniPainterHub.WebApp.Tests`: bUnit and client-service coverage
- `e2e`: Playwright browser smoke automation

For the full technical breakdown, see [ARCHITECTURE.md](<../20 Engineering/ARCHITECTURE.md>).

## Quality

The repository has automated coverage from service tests through browser automation:

- Unit, service, controller, integration-style, and bUnit coverage across the server and WebApp test projects
- Playwright smoke coverage for authentication, posting, search, profiles, messaging, projects, moderation, and reports
- A full UI-review matrix covering 101 current captures across shell, community, projects, auth, viewer, messaging, support, and admin states
- GitHub Actions quality gates for build, tests, coverage, and browser smoke on pull requests

## Local Run

### Prerequisites

- .NET 10 SDK
- Node.js
- SQL Server LocalDB or another SQL Server instance

### Start the app

```powershell
dotnet restore MiniPainterHub.sln
dotnet run --project MiniPainterHub.Server
```

The server hosts the API and serves the Blazor WebAssembly client.

### Seed portfolio-style demo content

```powershell
dotnet run --project MiniPainterHub.Server -- `
  --seed-dev-content `
  --avatars-dir ..\assets\dev-seed\avatars `
  --post-images-dir ..\assets\dev-seed\post-images
```

Useful seeded accounts:

- `admin` / `P@ssw0rd!`
- `user` / `User123!`
- `studiomod` / `StudioMod123!`
- Additional seeded painters use `Palette123!` during local development.

These deterministic credentials are development-only values from `appsettings.Development.json`. Non-development environments must provide JWT, database, image storage, and optional seed-admin secrets through external configuration.

## Test Commands

```powershell
dotnet test .\MiniPainterHub.sln --collect:"XPlat Code Coverage"
npm --prefix e2e run test:smoke
```

## Additional Docs

- [ARCHITECTURE.md](<../20 Engineering/ARCHITECTURE.md>)
- [DEPLOYMENT.md](<../20 Engineering/DEPLOYMENT.md>)
