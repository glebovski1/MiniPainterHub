# MiniPainterHub

## Authentication API Notes

The authentication endpoints surface validation and credential errors through standard `ProblemDetails` payloads. Registration failures throw `DomainValidationException`, which is translated to a `400 Bad Request` response that includes an `errors` dictionary populated from ASP.NET Identity results. Login failures for bad credentials now raise `UnauthorizedAccessException`, producing a `401 Unauthorized` `ProblemDetails` response with a helpful error message.

## Comments API Notes

Requesting a comment that does not exist (for example, `GET /api/comments/{id}`) now surfaces a consistent `ProblemDetails` payload. The global exception handler translates the service's `NotFoundException` into a `404 Not Found` response with the title `"Not found"` and a `detail` of `"Comment not found."`, allowing clients to rely on the standardized error contract when comments have been deleted or never existed.

## Azure App Service configuration

When deploying MiniPainterHub to Azure App Service, ensure the blob storage settings use the hierarchical configuration keys that the application expects (`ImageStorage:AzureConnectionString` and `ImageStorage:AzureContainer`). If your existing configuration still uses flat keys such as `ImageStorageAzureConnectionString` or `ImageStorageAzureContainer`, update them on the App Service **Configuration → Application settings** blade to the new names (or use the double-underscore form `ImageStorage__AzureConnectionString` and `ImageStorage__AzureContainer` for environment-variable compatibility). Save the configuration and restart the App Service to reload the updated settings.

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
