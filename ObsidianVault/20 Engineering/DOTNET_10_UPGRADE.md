# .NET 10 Upgrade

MiniPainterHub now targets `net10.0` across server, client, shared contracts, and test projects.

## References

- [.NET lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core): .NET 8 LTS ends November 10, 2026; .NET 10 support runs through November 14, 2028.
- [.NET 10 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview): .NET 10 is an LTS release with ASP.NET Core, Blazor, EF Core, SDK, runtime, and library updates.
- [ASP.NET Core 9 to 10 migration](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-100?view=aspnetcore-10.0): update `global.json`, TFMs to `net10.0`, and Microsoft ASP.NET Core / EF Core / Extensions packages to `10.0.0` or later.

## Branch Inventory

- Local SDK: user-local `C:\Users\uslep\.dotnet` SDK `10.0.300`; repo `global.json` requires `10.0.100` with `latestFeature` roll-forward.
- TFMs: all project files target `net10.0`.
- CI/deploy: GitHub Actions `setup-dotnet` entries use `10.0.x`.
- Microsoft package band: `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.Caching.Memory`, and `System.Text.Json` are pinned to compatible `10.0.5` packages; the outdated inventory shows `10.0.8` patch updates are available.
- Blazor lazy loading: messaging assemblies remain lazy-loaded, but the form/validation stack and `System.IO.Pipelines` now load at boot because .NET 10 startup paths require `System.Linq.Expressions` and `System.Text.Json` requires `System.IO.Pipelines` before route-level lazy loading can run.
- Package compatibility was inventoried with `dotnet list MiniPainterHub.sln package --outdated --include-transitive`; notable non-Microsoft upgrade candidates are `Azure.Storage.Blobs`, `FluentValidation`, `SixLabors.ImageSharp`, `Swashbuckle.AspNetCore`, `bunit`, `FluentAssertions`, `coverlet.collector`, and `xunit.runner.visualstudio`, while `Microsoft.NET.Test.Sdk` should be evaluated with the test runner stack.

## Migration Risks

- Blazor WebAssembly: .NET 10 changes boot/static asset behavior, including inlined boot config, removed `BlazorCacheBootResources`, and framework assembly dependency timing; keep boot-critical JSON/form dependencies out of route lazy loading and verify published app assets plus service-worker behavior.
- EF Core: EF Core 10 can change translation and query behavior; keep service tests and LocalDB-backed E2E coverage in the gate. Run `dotnet ef migrations has-pending-model-changes` when changing entities or EF package versions; this branch includes an empty `Net10ModelSnapshot` migration to update EF snapshot metadata.
- Identity/JWT: ASP.NET Core Identity and JWT bearer packages moved to the 10.0 line; verify login, logout, expired-token cleanup, and authorized API/SignalR calls.
- Swashbuckle/OpenAPI: current Swashbuckle remains non-Microsoft and should be validated against ASP.NET Core 10 before production deployment.
- ImageSharp: ImageSharp 4.x is available but not adopted in this branch; keep image upload, processing, rollback, and delete-cleanup tests in the gate.
- bUnit: bUnit 2.x is available but not adopted in this branch; keep viewer/auth component tests in the gate before changing bUnit separately.
- CI/deploy runtime: App Service, local dev machines, and GitHub runners must have .NET 10 SDK/runtime availability before the branch can ship.

## Required Gates

- `dotnet restore MiniPainterHub.sln`
- `dotnet format MiniPainterHub.sln --verify-no-changes --verbosity minimal --no-restore`
- `dotnet build MiniPainterHub.sln --configuration Release --no-restore /warnaserror`
- `dotnet test MiniPainterHub.Server.Tests/MiniPainterHub.Server.Tests.csproj --configuration Release --no-build`
- `dotnet test MiniPainterHub.WebApp.Tests/MiniPainterHub.WebApp.Tests.csproj --configuration Release --no-build`
- `dotnet ef migrations has-pending-model-changes --project MiniPainterHub.Server/MiniPainterHub.Server.csproj --startup-project MiniPainterHub.Server/MiniPainterHub.Server.csproj`
- `npm --prefix e2e ci`
- `npm --prefix e2e run test:smoke`
- `npm --prefix e2e run test:ui-review`
- `dotnet list MiniPainterHub.sln package --vulnerable --include-transitive`
