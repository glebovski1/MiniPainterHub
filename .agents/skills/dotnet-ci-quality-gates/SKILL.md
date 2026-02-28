---
name: "dotnet-ci-quality-gates"
description: "Use when creating or hardening GitHub Actions quality gates for .NET repositories, including restore, build, tests, coverage, and artifact publication."
---

# .NET CI Quality Gates

## When to use
- Setting up or improving GitHub Actions CI for .NET repositories.
- Defining minimum merge gates for build and test quality.
- Adding coverage and artifact publication for diagnostics.

## Workflow
1. Establish baseline mandatory gates.
   - Restore, build, and test on pull requests.
2. Use deterministic toolchain.
   - Pin .NET SDK version and cache NuGet packages.
3. Add quality checks incrementally.
   - Coverage collection.
   - Formatting and analyzers.
   - Optional UI snapshot checks when relevant.
4. Publish diagnostics artifacts.
   - Test results and coverage outputs.
   - UI snapshots when generated.
5. Enforce through branch protection.
   - Require successful workflow checks before merge.

## Repo-focused defaults
- Build with `dotnet build MiniPainterHub.sln`.
- Run backend tests with `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj`.
- Optionally run UI snapshot tooling when WebApp visual behavior is changed.

## References
- `references/github-actions-template.md`
- `references/scaling-quality-gates.md`
