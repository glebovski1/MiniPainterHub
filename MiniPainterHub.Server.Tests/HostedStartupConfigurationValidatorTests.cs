using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MiniPainterHub.Server.Options;
using Xunit;

namespace MiniPainterHub.Server.Tests;

public class HostedStartupConfigurationValidatorTests
{
    [Fact]
    public void Validate_WhenRequiredSettingsMissing_ThrowsWithExpectedAzureKeys()
    {
        var configuration = CreateConfiguration();

        Action act = () => HostedStartupConfigurationValidator.Validate(configuration, Environments.Production);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*ConnectionStrings__DefaultConnection*Jwt__Key*Jwt__Issuer*Jwt__Audience*ImageStorage__AzureConnectionString*ImageStorage__AzureContainer*");
    }

    [Fact]
    public void Validate_WhenLegacyImageStorageKeysArePresent_ThrowsRenameGuidance()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=tcp:test.database.windows.net;Initial Catalog=MiniPainterHub;",
                ["Jwt:Key"] = "12345678901234567890123456789012",
                ["Jwt:Issuer"] = "MiniPainterHubApi",
                ["Jwt:Audience"] = "MiniPainterHubClient",
                ["ImageStorageAzureConnectionString"] = "UseDevelopmentStorage=true",
                ["ImageStorageAzureContainer"] = "images"
            });

        Action act = () => HostedStartupConfigurationValidator.Validate(configuration, Environments.Production);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*ImageStorage__AzureConnectionString (legacy key ImageStorageAzureConnectionString is set; rename it)*ImageStorage__AzureContainer (legacy key ImageStorageAzureContainer is set; rename it)*");
    }

    [Fact]
    public void Validate_WhenSeedAdminEnabledWithoutCredentials_ThrowsWithSeedAdminKeys()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=tcp:test.database.windows.net;Initial Catalog=MiniPainterHub;",
                ["Jwt:Key"] = "12345678901234567890123456789012",
                ["Jwt:Issuer"] = "MiniPainterHubApi",
                ["Jwt:Audience"] = "MiniPainterHubClient",
                ["ImageStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
                ["ImageStorage:AzureContainer"] = "images",
                ["SeedAdmin:Enabled"] = "true"
            });

        Action act = () => HostedStartupConfigurationValidator.Validate(configuration, Environments.Production);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*SeedAdmin__Email*SeedAdmin__Password*");
    }

    [Fact]
    public void Validate_WhenRequiredSettingsPresent_ReturnsNormalizedConfiguration()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=tcp:test.database.windows.net;Initial Catalog=MiniPainterHub;",
                ["Jwt:Key"] = "12345678901234567890123456789012",
                ["Jwt:Issuer"] = "MiniPainterHubApi",
                ["Jwt:Audience"] = "MiniPainterHubClient",
                ["ImageStorage:AzureConnectionString"] = "UseDevelopmentStorage=true",
                ["ImageStorage:AzureContainer"] = "images",
                ["SeedAdmin:Enabled"] = "true",
                ["SeedAdmin:Email"] = "admin@example.com",
                ["SeedAdmin:Password"] = "P@ssw0rd!"
            });

        var result = HostedStartupConfigurationValidator.Validate(configuration, Environments.Production);

        result.DefaultConnectionString.Should().Contain("test.database.windows.net");
        result.Jwt.Key.Should().Be("12345678901234567890123456789012");
        result.Jwt.Issuer.Should().Be("MiniPainterHubApi");
        result.Jwt.Audience.Should().Be("MiniPainterHubClient");
        result.AzureBlobStorage.ConnectionString.Should().Be("UseDevelopmentStorage=true");
        result.AzureBlobStorage.ContainerName.Should().Be("images");
        result.SeedAdminEnabled.Should().BeTrue();
        result.SeedAdminEmail.Should().Be("admin@example.com");
        result.SeedAdminPassword.Should().Be("P@ssw0rd!");
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string?>? values = null)
    {
        var configurationValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (values is not null)
        {
            foreach (var pair in values)
            {
                configurationValues[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
    }
}
