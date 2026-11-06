using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace MiniPainterHub.Server.Infrastructure.Security;

internal static class DataProtectionConfiguration
{
    internal const string ApplicationName = "MiniPainterHub";

    internal static void Configure(IServiceCollection services, IConfiguration configuration,
        string contentRootPath, bool isLocalTooling)
    {
        var provider = configuration["DataProtection:Provider"] ?? "FileSystem";
        var blobValue = configuration["DataProtection:BlobUri"];
        var keyValue = configuration["DataProtection:KeyVaultKeyIdentifier"];
        var clientId = configuration["DataProtection:ManagedIdentityClientId"];

        if (provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(blobValue) || !string.IsNullOrWhiteSpace(keyValue)
                || !string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("Shared Data Protection settings require DataProtection:Provider=AzureBlob.");
            }

            var builder = services.AddDataProtection().SetApplicationName(ApplicationName);
            if (!isLocalTooling)
            {
                // Preserve the existing native Windows/local-tooling behavior until explicitly switched.
                var keyPath = configuration["DataProtection:KeysPath"]
                    ?? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? contentRootPath, "data-protection-keys");
                builder.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            }

            return;
        }

        if (!provider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DataProtection:Provider must be FileSystem or AzureBlob.");
        }

        var blobUri = RequiredAzureUri(blobValue, "DataProtection:BlobUri", @"^[a-z0-9]{3,24}\.blob\.core\.windows\.net$");
        if (blobUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            throw new InvalidOperationException("DataProtection:BlobUri must identify a container and key-ring blob.");
        }

        var keyUri = RequiredAzureUri(keyValue, "DataProtection:KeyVaultKeyIdentifier", @"^[a-z0-9-]{3,24}\.vault\.azure\.net$");
        if (!Regex.IsMatch(keyUri.AbsolutePath, @"^/keys/[a-zA-Z0-9-]+$"))
        {
            throw new InvalidOperationException("DataProtection:KeyVaultKeyIdentifier must be a versionless /keys/<name> URI.");
        }

        if (!string.IsNullOrWhiteSpace(clientId) && !Guid.TryParse(clientId, out _))
        {
            throw new InvalidOperationException("DataProtection:ManagedIdentityClientId must be a GUID when supplied.");
        }

        TokenCredential credential = isLocalTooling
            ? new DefaultAzureCredential()
            : string.IsNullOrWhiteSpace(clientId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));

        // Configure persistence and encryption together. Never fall back to a new local ring.
        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToAzureBlobStorage(blobUri, credential)
            .ProtectKeysWithAzureKeyVault(keyUri, credential);
    }

    private static Uri RequiredAzureUri(string? value, string setting, string hostPattern)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath.Contains('%')
            || !Regex.IsMatch(uri.Host, hostPattern, RegexOptions.IgnoreCase))
        {
            // Do not echo the input: a mistaken connection string or SAS may contain credentials.
            throw new InvalidOperationException($"{setting} must be a public-Azure HTTPS resource URI without credentials, query or fragment.");
        }

        return uri;
    }
}
