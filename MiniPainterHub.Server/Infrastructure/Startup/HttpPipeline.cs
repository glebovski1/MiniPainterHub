using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Middleware;
using MiniPainterHub.Server.Realtime;
using MiniPainterHub.Server.Services;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MiniPainterHub;

public partial class Program
{
    private static void ConfigureMiniPainterHubPipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiniPainterHub API v1");
            });
        }
        else
        {
            app.UseHsts();
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        UseStaticAssetHeaderPolicy(app);
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<SiteActivityMiddleware>();
        app.UseMiddleware<MaintenanceModeMiddleware>();
        UsePublishedBootManifestStaticFile(app);
        app.UseBlazorFrameworkFiles();

        if (IsLocalToolingEnvironment(app.Environment))
        {
            var localImageStorage = LocalImageStoragePaths.Resolve(app.Environment, app.Configuration);
            Directory.CreateDirectory(localImageStorage.PhysicalPath);
            app.UseStaticFiles(CreateStaticFileOptions(
                new PhysicalFileProvider(localImageStorage.PhysicalPath),
                localImageStorage.RequestPath));
        }

        app.UseStaticFiles(CreateStaticFileOptions());
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat").RequireRateLimiting(RateLimitingPolicies.Realtime);
        app.MapFallbackToFile("index.html");
        MapHealthEndpoints(app);

        MapTestSupportResetEndpoint(app);
    }

    private static void MapTestSupportResetEndpoint(WebApplication app)
    {
        var resetToken = app.Configuration["TestSupport:ResetToken"];
        var resetEnabled = app.Configuration.GetValue<bool>("TestSupport:ResetEnabled");
        if (!IsLocalToolingEnvironment(app.Environment) || !resetEnabled || string.IsNullOrWhiteSpace(resetToken))
        {
            return;
        }

        app.MapPost("/api/test-support/reset", async (HttpContext context, AppDbContext db) =>
        {
            var requestToken = context.Request.Headers["X-Test-Support-Token"].ToString();
            if (string.IsNullOrWhiteSpace(requestToken))
            {
                return Results.Unauthorized();
            }

            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp is null)
            {
                return Results.Forbid();
            }

            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            if (!IPAddress.IsLoopback(remoteIp))
            {
                return Results.Forbid();
            }

            var expectedTokenBytes = Encoding.UTF8.GetBytes(resetToken);
            var requestTokenBytes = Encoding.UTF8.GetBytes(requestToken);
            var validToken = CryptographicOperations.FixedTimeEquals(expectedTokenBytes, requestTokenBytes);
            if (!validToken)
            {
                return Results.Unauthorized();
            }

            await db.Database.EnsureDeletedAsync();
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            await DataSeeder.SeedAsync(app.Services);

            return Results.Ok(new { ok = true });
        });
    }
}
