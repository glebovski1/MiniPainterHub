# MiniPainterHub

## Codex Cloud test bootstrap

If your cloud run reports `dotnet` missing, run:

```bash
bash tools/cloud/bootstrap-dotnet-and-test.sh
```

The script installs a supported .NET SDK in `$HOME/.dotnet` (user-local, no sudo), then runs restore, Release build, and server tests.

## Authentication API Notes

The authentication endpoints surface validation and credential errors through standard `ProblemDetails` payloads. Registration failures throw `DomainValidationException`, which is translated to a `400 Bad Request` response that includes an `errors` dictionary populated from ASP.NET Identity results. Login failures for bad credentials now raise `UnauthorizedAccessException`, producing a `401 Unauthorized` `ProblemDetails` response with a helpful error message.

## Comments API Notes

Requesting a comment that does not exist (for example, `GET /api/comments/{id}`) now surfaces a consistent `ProblemDetails` payload. The global exception handler translates the service's `NotFoundException` into a `404 Not Found` response with the title `"Not found"` and a `detail` of `"Comment not found."`, allowing clients to rely on the standardized error contract when comments have been deleted or never existed.

## Azure App Service configuration

When deploying MiniPainterHub to Azure App Service, ensure the blob storage settings use the hierarchical configuration keys that the application expects (`ImageStorage:AzureConnectionString` and `ImageStorage:AzureContainer`). If your existing configuration still uses flat keys such as `ImageStorageAzureConnectionString` or `ImageStorageAzureContainer`, update them on the App Service **Configuration → Application settings** blade to the new names (or use the double-underscore form `ImageStorage__AzureConnectionString` and `ImageStorage__AzureContainer` for environment-variable compatibility). Save the configuration and restart the App Service to reload the updated settings.

## Development DB bootstrap note

When running against SQL Server in `Development`, the server applies EF migrations at startup. If the database has Identity tables (for example `AspNetRoles`) but no matching `__EFMigrationsHistory` rows, startup can hit a duplicate-object migration conflict. The app now recovers from this specific local conflict by recreating the development database and retrying migrations.

- Default behavior: enabled (`Database:RecreateOnSchemaConflict` defaults to `true` in Development logic)
- Opt out: set `Database:RecreateOnSchemaConflict=false`

## Development content commands

Use explicit one-off commands when working with seeded dev avatars and sample content:

```powershell
dotnet run --project MiniPainterHub.Server -- --seed-dev-content --avatars-dir C:\path\to\avatars
dotnet run --project MiniPainterHub.Server -- --generate-dev-avatars --avatars-dir C:\path\to\avatars
dotnet run --project MiniPainterHub.Server -- --seed-dev-content --avatars-dir C:\path\to\avatars --post-images-dir C:\path\to\post-images
```

- `--seed-dev-content`: destructive reset of the development database and local image storage, then recreates the seeded users, profiles, posts, and avatar assignments.
- `--seed-dev-content --post-images-dir <path>`: optional addition that attaches one seeded image to each seeded post. If the folder contains fewer than 20 images, files are reused in sorted order until all posts have an image.
- `--generate-dev-avatars`: imports just the seed-avatar files, refreshes avatar URLs for any existing seeded users/profiles, and leaves all other development data untouched so it is safe to rerun.
- Seeded social data: `--seed-dev-content` also creates follow relationships and direct-message conversations so the following feed, public-profile follow counts, and `/messages` UI have immediate development data.

## Admin functionality test checklist

Use this checklist to validate end-to-end admin capabilities after startup:

1. Start the server and WebApp in `Development`.
2. Sign in with seeded admin credentials:
   - Username: `admin`
   - Password: `P@ssw0rd!`
3. Confirm the left collapsible panel shows the `Admin` section:
   - `Moderation`
   - `Audit log`
   - `User suspensions`
4. On `Moderation`:
   - Load post/comment previews by id.
   - Hide and restore post/comment with a reason.
   - In post lists, use the `Visibility` filter (`Active only`, `Include hidden`, `Hidden only`) and restore hidden posts inline.
   - In post details comments, use the comment `Visibility` filter (`Active only`, `Include hidden`, `Hidden only`) and restore hidden comments inline.
5. On `User suspensions`:
   - Use `Find user`, select a user, suspend and unsuspend.
   - Leave the lookup query empty to list currently suspended users.
6. On public profiles:
   - Open `/users/{userId}` (for example from a post/comment author link).
   - As `Admin`, suspend/unsuspend is available directly on the profile page.
7. On `Audit log`:
   - Apply `Target type`, `Actor user id`, and `Action type` filters.
   - Verify pagination with previous/next controls.
8. Verify regular user navigation:
   - `My posts` should not redirect to login for authenticated users.
   - Post cards should attempt thumbnail first, then full image fallback on image load error.

## Codex Skills Tooling

This repo now includes helper scripts under `tools/skills`:

- `init-skill.py`: scaffold a new skill skeleton.
- `install-skill-from-github.py`: install a skill from GitHub with target scope support.

### Create a new skill skeleton

```powershell
python tools/skills/init-skill.py `
  --name my-skill `
  --description "Use when ..." `
  --target repo
```

Targets:

- `repo`: `./.agents/skills`
- `global`: `%CODEX_HOME%/skills` (defaults to `~/.codex/skills`)
- `both`: install/create in both locations

### Install a skill from GitHub

```powershell
python tools/skills/install-skill-from-github.py `
  --repo openai/skills `
  --path skills/.curated/doc `
  --target both
```

You can also pass `--url https://github.com/<owner>/<repo>/tree/<ref>/<path>` instead of `--repo` + `--path`.
