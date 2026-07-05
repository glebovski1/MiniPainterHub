using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    private static async Task RunMiniPainterHubStartupAsync(
        WebApplication app,
        HostedStartupConfiguration? hostedStartupConfiguration)
    {
        if (IsLocalToolingEnvironment(app.Environment))
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            LogDatabaseTarget(db, startupLogger);
            if (db.Database.IsRelational())
            {
                await EnsureDevelopmentDatabaseAsync(db, startupLogger, app.Configuration);
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            await DataSeeder.SeedAsync(app.Services);
        }

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        if (hostedStartupConfiguration is not null)
        {
            HostedStartupConfigurationValidator.LogSummary(
                logger,
                hostedStartupConfiguration,
                app.Environment.EnvironmentName);
        }

        if (!app.Environment.IsProduction())
        {
            return;
        }

        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await EnsureProductionSchemaReadyAsync(app, db, logger);

            await DataSeeder.SeedAdminAsync(app);
            logger.LogInformation("Admin seed ok.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Startup failed in Production.");
            throw;
        }
    }

    private static async Task EnsureProductionSchemaReadyAsync(WebApplication app, AppDbContext db, ILogger logger)
    {
        if (!db.Database.IsRelational())
        {
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Non-relational production database initialized.");
            return;
        }

        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pendingMigrations.Length == 0)
        {
            logger.LogInformation("EF migrations ok; no pending migrations.");
            return;
        }

        if (app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup"))
        {
            logger.LogWarning(
                "Applying {Count} pending EF migrations during Production startup because Database:AutoMigrateOnStartup is enabled.",
                pendingMigrations.Length);
            await db.Database.MigrateAsync();
            logger.LogInformation("EF migrations ok.");
            return;
        }

        throw new InvalidOperationException(
            "Production database has pending EF migrations. Run the deployment migration step before starting the app, or set Database:AutoMigrateOnStartup=true only for an emergency single-instance rollout. Pending migrations: "
            + string.Join(", ", pendingMigrations));
    }
}
