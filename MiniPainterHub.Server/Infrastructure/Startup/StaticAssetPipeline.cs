using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    private static void UsePublishedBootManifestStaticFile(WebApplication app)
    {
        if (string.IsNullOrWhiteSpace(app.Environment.WebRootPath))
        {
            return;
        }

        var bootManifestPath = Path.Combine(app.Environment.WebRootPath, "_framework", "blazor.boot.json");
        if (!File.Exists(bootManifestPath))
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            if (!string.Equals(
                    context.Request.Path.Value,
                    "/_framework/blazor.boot.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            context.Response.ContentType = "application/json";
            ApplyStaticAssetHeaders(context);
            await context.Response.SendFileAsync(bootManifestPath);
        });
    }

    private static StaticFileOptions CreateStaticFileOptions() =>
        new()
        {
            OnPrepareResponse = context => ApplyStaticAssetHeaders(context.Context)
        };

    private static StaticFileOptions CreateStaticFileOptions(IFileProvider fileProvider, PathString requestPath) =>
        new()
        {
            FileProvider = fileProvider,
            RequestPath = requestPath,
            OnPrepareResponse = context => ApplyStaticAssetHeaders(context.Context)
        };

    private static void UseStaticAssetHeaderPolicy(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (app.Environment.IsProduction() && IsPortableDebugSymbol(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.OnStarting(() =>
            {
                ApplySecurityHeaders(context);
                ApplyApiResponseHeaders(context);
                ApplyStaticAssetHeaders(context);
                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static void ApplyStaticAssetHeaders(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsManagedStaticAssetPath(path))
        {
            return;
        }

        var cacheControl = ResolveStaticAssetCacheControl(path);
        if (!string.IsNullOrWhiteSpace(cacheControl))
        {
            context.Response.Headers["Cache-Control"] = cacheControl;
        }

        ApplySecurityHeaders(context);
    }

    private static void ApplySecurityHeaders(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private static void ApplyApiResponseHeaders(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
    }

    private static string ResolveStaticAssetCacheControl(string path)
    {
        var fileName = GetRequestFileName(path);
        if (IsAlwaysRevalidatedAsset(path, fileName))
        {
            return "no-cache";
        }

        if (HasFingerprintInFileName(fileName))
        {
            return $"public, max-age={StaticAssetOneYearSeconds}, immutable";
        }

        if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
        {
            return $"public, max-age={StaticAssetOneWeekSeconds}";
        }

        if (IsImageAsset(path))
        {
            return $"public, max-age={StaticAssetOneWeekSeconds}";
        }

        if (IsFontAsset(path) || IsCssOrScriptAsset(path))
        {
            return $"public, max-age={StaticAssetOneDaySeconds}";
        }

        return $"public, max-age={StaticAssetOneDaySeconds}";
    }

    private static bool IsManagedStaticAssetPath(string path) =>
        path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/JSHelpers/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
        || IsKnownRootStaticAsset(path);

    private static bool IsKnownRootStaticAsset(string path)
    {
        var fileName = GetRequestFileName(path);
        return string.Equals(path, "/", StringComparison.Ordinal)
            || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "favicon.png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "manifest.webmanifest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "service-worker.js", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "service-worker-assets.js", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "MiniPainterHub.WebApp.styles.css", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlwaysRevalidatedAsset(string path, string fileName) =>
        string.Equals(path, "/", StringComparison.Ordinal)
        || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "blazor.boot.json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "service-worker.js", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "service-worker-assets.js", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, "manifest.webmanifest", StringComparison.OrdinalIgnoreCase);

    private static bool IsPortableDebugSymbol(PathString path) =>
        path.Value?.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) == true;

    private static string GetRequestFileName(string path)
    {
        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        var pathOnly = queryStart >= 0 ? path[..queryStart] : path;
        var lastSlash = pathOnly.LastIndexOf('/');
        return lastSlash >= 0 ? pathOnly[(lastSlash + 1)..] : pathOnly;
    }

    private static bool IsCssOrScriptAsset(string path) =>
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

    private static bool IsFontAsset(string path) =>
        path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageAsset(string path) =>
        path.EndsWith(".avif", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);

    private static bool HasFingerprintInFileName(string fileName)
    {
        var tokenLength = 0;
        for (var index = 0; index <= fileName.Length; index++)
        {
            var current = index < fileName.Length ? fileName[index] : '.';
            if (IsHexDigit(current))
            {
                tokenLength++;
                continue;
            }

            if (tokenLength >= 8)
            {
                return true;
            }

            tokenLength = 0;
        }

        return false;
    }

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9'
        || value is >= 'a' and <= 'f'
        || value is >= 'A' and <= 'F';
}
