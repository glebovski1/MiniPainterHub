# Roll & Paint

> A focused social platform for miniature painters to share finished work, document projects, exchange techniques, and find inspiration.

MiniPainterHub is the repository behind **Roll & Paint**. It is an image-first community application built for the miniature-painting hobby: painters can publish work with paint recipes and project context, follow work in progress, discover guides and creators, talk directly, and help keep the community welcoming.

![The Roll & Paint home feed, showing painted miniatures and hobby activity](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/01-home-feed.png>)

## What painters can do

- **Share finished work and WIP updates** with photos, tags, descriptions, paint recipes, techniques, and links to a larger project.
- **Run hobby projects** as a dedicated space for a campaign, army, collection, or series of miniatures.
- **Publish and explore guides** for techniques, tools, and hobby knowledge alongside community posts.
- **Discover people and ideas** through cross-community search for posts, projects, painters, tags, and guides.
- **Connect directly** in a compact messaging experience designed around hobby conversations.
- **Moderate responsibly** with a consolidated report inbox and the tools needed to review community issues.

## Product tour

### Create a post with useful hobby context

The composer supports image uploads plus the details another painter needs: scale, system, model, paints, techniques, tags, a project link, and a recipe or build log.

![Roll & Paint post composer with miniature details, tags, project fields, and recipe controls](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/02-create-post.png>)

### Give each piece a story

Post pages keep the image, process notes, reactions, comments, recipe, and project context together.

![A Roll & Paint post detail page with a miniature photograph, comments, and hobby metadata](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/03-post-details.png>)

### Inspect the painting up close

An unobtrusive image viewer lets painters study brushwork and details without losing the context of the community feed.

![Dark Roll & Paint image viewer displaying a detailed painted miniature](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/04-rich-viewer.png>)

### Find a technique, project, or painter

Search brings posts, active projects, artists, tags, and guides into one discovery surface.

![Roll & Paint search results across posts, people, tags, and projects](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/05-search-discovery.png>)

### Keep conversations human

Direct messages provide a quieter place to exchange feedback, ideas, and hobby questions.

![Roll & Paint direct messages showing a miniature-painting conversation](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/06-messages.png>)

### Support a healthy community

Moderators can review reports through a single inbox, making it easier to act consistently and keep discussions welcoming.

![Roll & Paint moderation inbox listing reported community content](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/07-admin-reports.png>)

## Engineering highlights

- **Backend:** ASP.NET Core 10 API and static host with authentication, authorization, validation, and operational endpoints.
- **Frontend:** Blazor WebAssembly application with a responsive, image-led Roll & Paint interface.
- **Data:** Entity Framework Core with SQL Server, ASP.NET Core Identity, and JWT-based API access.
- **Real-time features:** SignalR-backed messaging and activity updates.
- **Media:** Image processing with ImageSharp; local development storage and Azure Blob Storage support for hosted environments.
- **Quality:** server, client, and browser-based UI-review coverage, including a full visual capture suite for shared layout changes.

## Run locally

Prerequisites: the .NET SDK specified in [global.json](global.json) and a SQL Server or LocalDB instance available through the development configuration.

```powershell
dotnet restore MiniPainterHub.sln
dotnet run --project MiniPainterHub.Server
```

For a populated local demonstration, use the provided development imagery from the server project directory:

```powershell
Push-Location MiniPainterHub.Server
dotnet run -- --seed-dev-content `
  --avatars-dir ..\assets\dev-seed\avatars `
  --post-images-dir ..\assets\dev-seed\post-images
Pop-Location
```

The development configuration contains the local database connection. The seed command is for development only and uses the repository's deliberately provided demo assets.

## Project documentation

- [Portfolio and detailed feature guide](<ObsidianVault/10 Project/README.md>)
- [Architecture](<ObsidianVault/20 Engineering/ARCHITECTURE.md>)
- [Deployment notes](<ObsidianVault/20 Engineering/DEPLOYMENT.md>)
- [Screenshot provenance and visual asset index](<ObsidianVault/50 Visual Assets/README.md>)
- [Full screenshot folder](<ObsidianVault/50 Visual Assets/Portfolio Screenshots/>)

For repository conventions and durable project context, start with [Agent Navigation](<ObsidianVault/00 Start Here/Agent Navigation.md>). Root compatibility pointers remain at [AGENT.md](AGENT.md) and [AGENTS.md](AGENTS.md).
