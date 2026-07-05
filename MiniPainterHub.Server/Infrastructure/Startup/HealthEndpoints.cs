using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    private static void MapHealthEndpoints(WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok("OK"));
        app.MapGet("/healthz/live", () => Results.Ok(new { status = "Healthy" }));
        app.MapGet("/healthz/ready", (CancellationToken cancellationToken) =>
            EvaluateReadinessAsync(app, cancellationToken));
    }

    private static async Task<IResult> EvaluateReadinessAsync(WebApplication app, CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, HealthCheckResult>(StringComparer.OrdinalIgnoreCase);

        await CheckSqlAsync(app, checks, cancellationToken);
        await CheckImageStorageAsync(app, checks, cancellationToken);
        CheckHostedConfiguration(app, checks);

        var healthy = checks.Values.All(check => check.IsHealthy);
        var payload = new
        {
            status = healthy ? "Healthy" : "Unhealthy",
            checks
        };

        return healthy
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task CheckSqlAsync(
        WebApplication app,
        IDictionary<string, HealthCheckResult> checks,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                checks["sql"] = HealthCheckResult.Fail("Database connection failed.");
                return;
            }

            if (db.Database.IsRelational())
            {
                var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
                checks["migrations"] = pendingMigrations.Length == 0
                    ? HealthCheckResult.Pass("No pending EF migrations.")
                    : HealthCheckResult.Fail("Pending EF migrations: " + string.Join(", ", pendingMigrations));
            }
            else
            {
                checks["migrations"] = HealthCheckResult.Pass("Non-relational provider does not use EF migrations.");
            }

            checks["sql"] = HealthCheckResult.Pass("Database connection ok.");
        }
        catch (Exception ex)
        {
            checks["sql"] = HealthCheckResult.Fail(ex.Message);
        }
    }

    private static async Task CheckImageStorageAsync(
        WebApplication app,
        IDictionary<string, HealthCheckResult> checks,
        CancellationToken cancellationToken)
    {
        try
        {
            var blobContainer = app.Services.GetService<BlobContainerClient>();
            if (blobContainer is not null)
            {
                var exists = await blobContainer.ExistsAsync(cancellationToken);
                checks["imageStorage"] = exists.Value
                    ? HealthCheckResult.Pass("Azure Blob container reachable.")
                    : HealthCheckResult.Fail("Azure Blob container does not exist or is not reachable.");
                return;
            }

            var localImageStorage = LocalImageStoragePaths.Resolve(app.Environment, app.Configuration);
            checks["imageStorage"] = Directory.Exists(localImageStorage.PhysicalPath)
                ? HealthCheckResult.Pass("Local image storage reachable.")
                : HealthCheckResult.Fail("Local image storage directory is missing.");
        }
        catch (Exception ex)
        {
            checks["imageStorage"] = HealthCheckResult.Fail(ex.Message);
        }
    }

    private static void CheckHostedConfiguration(WebApplication app, IDictionary<string, HealthCheckResult> checks)
    {
        if (IsLocalToolingEnvironment(app.Environment))
        {
            checks["configuration"] = HealthCheckResult.Pass("Local tooling configuration.");
            return;
        }

        try
        {
            HostedStartupConfigurationValidator.Validate(app.Configuration, app.Environment.EnvironmentName);
            checks["configuration"] = HealthCheckResult.Pass("Hosted configuration ok.");
        }
        catch (Exception ex)
        {
            checks["configuration"] = HealthCheckResult.Fail(ex.Message);
        }
    }

    private sealed record HealthCheckResult(bool IsHealthy, string Detail)
    {
        public static HealthCheckResult Pass(string detail) => new(true, detail);

        public static HealthCheckResult Fail(string detail) => new(false, detail);
    }
}
