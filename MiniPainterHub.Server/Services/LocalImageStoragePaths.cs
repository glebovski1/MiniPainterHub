using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MiniPainterHub.Server.Services;

internal static class LocalImageStoragePaths
{
    internal const string DefaultRequestPath = "/uploads/images";

    internal static LocalImageStorageLocation Resolve(IWebHostEnvironment env, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(config);

        var configuredPhysicalPath = config["ImageStorage:LocalPath"];
        var physicalPath = string.IsNullOrWhiteSpace(configuredPhysicalPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MiniPainterHub",
                "uploads",
                "images")
            : ResolveConfiguredPhysicalPath(env, configuredPhysicalPath);

        var configuredRequestPath = config["ImageStorage:RequestPath"];
        var requestPath = NormalizeRequestPath(configuredRequestPath);

        return new LocalImageStorageLocation(Path.GetFullPath(physicalPath), requestPath);
    }

    private static string ResolveConfiguredPhysicalPath(IWebHostEnvironment env, string configuredPhysicalPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configuredPhysicalPath.Trim());

        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return Path.Combine(env.WebRootPath, expanded);
    }

    private static string NormalizeRequestPath(string? configuredRequestPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRequestPath))
        {
            return DefaultRequestPath;
        }

        var normalized = configuredRequestPath.Trim().Replace("\\", "/");
        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
    }
}

internal sealed record LocalImageStorageLocation(string PhysicalPath, string RequestPath);
