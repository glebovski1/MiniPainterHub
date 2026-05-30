using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Options;
using System;
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
            await db.Database.MigrateAsync();
            logger.LogInformation("EF migrations ok.");

            await DataSeeder.SeedAdminAsync(app);
            logger.LogInformation("Admin seed ok.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Startup failed in Production.");
            throw;
        }
    }
}
