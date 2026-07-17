using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Options;

internal static class HostedStartupConfigurationValidator
{
    public static HostedStartupConfiguration Validate(IConfiguration configuration, string environmentName)
    {
        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hosted startup configuration validation should not run in Development.");
        }

        var missingSettings = new List<string>();

        var defaultConnectionString = GetRequiredConnectionString(
            configuration,
            connectionName: "DefaultConnection",
            displayKey: "ConnectionStrings__DefaultConnection",
            missingSettings);
        var jwtKey = GetRequiredConfigurationValue(configuration, "Jwt:Key", "Jwt__Key", missingSettings);
        var jwtIssuer = GetRequiredConfigurationValue(configuration, "Jwt:Issuer", "Jwt__Issuer", missingSettings);
        var jwtAudience = GetRequiredConfigurationValue(configuration, "Jwt:Audience", "Jwt__Audience", missingSettings);
        var imageStorageConnectionString = GetRequiredConfigurationValue(
            configuration,
            "ImageStorage:AzureConnectionString",
            "ImageStorage__AzureConnectionString",
            missingSettings,
            legacyKeys: ["ImageStorageAzureConnectionString"]);
        var imageStorageContainer = GetRequiredConfigurationValue(
            configuration,
            "ImageStorage:AzureContainer",
            "ImageStorage__AzureContainer",
            missingSettings,
            legacyKeys: ["ImageStorageAzureContainer"]);

        if (configuration.GetValue<bool>("EmailConfirmation:Enabled"))
        {
            var provider = GetRequiredConfigurationValue(
                configuration,
                "EmailConfirmation:Provider",
                "EmailConfirmation__Provider",
                missingSettings);
            GetRequiredConfigurationValue(
                configuration,
                "EmailConfirmation:PublicOrigin",
                "EmailConfirmation__PublicOrigin",
                missingSettings);
            GetRequiredConfigurationValue(
                configuration,
                "EmailConfirmation:Endpoint",
                "EmailConfirmation__Endpoint",
                missingSettings);
            GetRequiredConfigurationValue(
                configuration,
                "EmailConfirmation:SenderAddress",
                "EmailConfirmation__SenderAddress",
                missingSettings);
            GetRequiredConfigurationValue(
                configuration,
                "EmailConfirmation:SenderDisplayName",
                "EmailConfirmation__SenderDisplayName",
                missingSettings);

            if (!string.IsNullOrWhiteSpace(provider)
                && !string.Equals(provider, EmailConfirmationProviders.AzureCommunicationServices, StringComparison.Ordinal))
            {
                missingSettings.Add("EmailConfirmation__Provider must be AzureCommunicationServices outside Development/Test");
            }
        }

        if (configuration.GetValue<bool>("Authentication:Google:Enabled"))
        {
            GetRequiredConfigurationValue(configuration, "Authentication:Google:ClientId", "Authentication__Google__ClientId", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Google:ClientSecret", "Authentication__Google__ClientSecret", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Google:CallbackPath", "Authentication__Google__CallbackPath", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Google:PublicOrigin", "Authentication__Google__PublicOrigin", missingSettings);
            GetRequiredConfigurationValue(configuration, "Site:SupportEmail", "Site__SupportEmail", missingSettings);

            if (configuration.GetValue<bool>("Authentication:Google:UseFakeProvider"))
            {
                missingSettings.Add("Authentication__Google__UseFakeProvider must be false outside Development/Test");
            }

            if (string.Equals(configuration["AllowedHosts"], "*", StringComparison.Ordinal))
            {
                missingSettings.Add("AllowedHosts must restrict the public Google authentication host");
            }
        }

        if (configuration.GetValue<bool>("Authentication:Discord:Enabled"))
        {
            GetRequiredConfigurationValue(configuration, "Authentication:Discord:ClientId", "Authentication__Discord__ClientId", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Discord:ClientSecret", "Authentication__Discord__ClientSecret", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Discord:CallbackPath", "Authentication__Discord__CallbackPath", missingSettings);
            GetRequiredConfigurationValue(configuration, "Authentication:Discord:PublicOrigin", "Authentication__Discord__PublicOrigin", missingSettings);
            GetRequiredConfigurationValue(configuration, "Site:SupportEmail", "Site__SupportEmail", missingSettings);

            if (configuration.GetValue<bool>("Authentication:Discord:UseFakeProvider"))
            {
                missingSettings.Add("Authentication__Discord__UseFakeProvider must be false outside Development/Test");
            }

            if (string.Equals(configuration["AllowedHosts"], "*", StringComparison.Ordinal))
            {
                missingSettings.Add("AllowedHosts must restrict the public Discord authentication host");
            }
        }

        var seedAdminEnabled =
            string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase)
            && configuration.GetValue<bool>("SeedAdmin:Enabled");
        string? seedAdminEmail = null;
        string? seedAdminPassword = null;

        if (seedAdminEnabled)
        {
            seedAdminEmail = GetRequiredConfigurationValue(configuration, "SeedAdmin:Email", "SeedAdmin__Email", missingSettings);
            seedAdminPassword = GetRequiredConfigurationValue(configuration, "SeedAdmin:Password", "SeedAdmin__Password", missingSettings);
        }

        if (missingSettings.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required non-development configuration settings: {string.Join(", ", missingSettings)}.");
        }

        return new HostedStartupConfiguration(
            defaultConnectionString!,
            new JwtStartupConfiguration(jwtKey!, jwtIssuer!, jwtAudience!),
            new AzureBlobStorageStartupConfiguration(imageStorageConnectionString!, imageStorageContainer!),
            seedAdminEnabled,
            seedAdminEmail,
            seedAdminPassword);
    }

    public static void LogSummary(ILogger logger, HostedStartupConfiguration configuration, string environmentName)
    {
        logger.LogInformation(
            "Validated {Environment} startup configuration. SqlTarget={SqlTarget}; BlobContainer={BlobContainer}; SeedAdminEnabled={SeedAdminEnabled}.",
            environmentName,
            DescribeConnectionTarget(configuration.DefaultConnectionString),
            configuration.AzureBlobStorage.ContainerName,
            configuration.SeedAdminEnabled);
    }

    private static string? GetRequiredConnectionString(
        IConfiguration configuration,
        string connectionName,
        string displayKey,
        ICollection<string> missingSettings)
    {
        var value = configuration.GetConnectionString(connectionName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        missingSettings.Add(displayKey);
        return null;
    }

    private static string? GetRequiredConfigurationValue(
        IConfiguration configuration,
        string configurationKey,
        string displayKey,
        ICollection<string> missingSettings,
        params string[] legacyKeys)
    {
        var value = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        foreach (var legacyKey in legacyKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration[legacyKey]))
            {
                missingSettings.Add($"{displayKey} (legacy key {legacyKey} is set; rename it)");
                return null;
            }
        }

        missingSettings.Add(displayKey);
        return null;
    }

    private static string DescribeConnectionTarget(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var dataSource = string.IsNullOrWhiteSpace(builder.DataSource) ? "unknown-server" : builder.DataSource;
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "unknown-database" : builder.InitialCatalog;
            return $"{dataSource}/{database}";
        }
        catch (ArgumentException)
        {
            return "configured";
        }
    }
}

internal sealed record HostedStartupConfiguration(
    string DefaultConnectionString,
    JwtStartupConfiguration Jwt,
    AzureBlobStorageStartupConfiguration AzureBlobStorage,
    bool SeedAdminEnabled,
    string? SeedAdminEmail,
    string? SeedAdminPassword);

internal sealed record JwtStartupConfiguration(string Key, string Issuer, string Audience);

internal sealed record AzureBlobStorageStartupConfiguration(string ConnectionString, string ContainerName);
