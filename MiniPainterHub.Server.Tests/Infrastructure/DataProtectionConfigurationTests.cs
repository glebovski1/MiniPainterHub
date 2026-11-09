using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public class DataProtectionConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("aaf4ecae-12ee-4b77-8bc9-9fb7c3b58f31")]
    public void AzureProvider_RegistersPersistenceAndEncryptionTogether(string? clientId)
    {
        var settings = SharedSettings();
        settings["DataProtection:ManagedIdentityClientId"] = clientId;
        // An inherited Windows path must not override an explicitly selected shared provider.
        settings["DataProtection:KeysPath"] = @"D:\home\data-protection-keys";
        using var services = Configure(settings);
        var options = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        options.XmlRepository.Should().NotBeNull();
        options.XmlRepository!.GetType().Name.Should().Be("AzureBlobXmlRepository");
        options.XmlEncryptor.Should().NotBeNull();
        options.XmlEncryptor!.GetType().Name.Should().Be("AzureKeyVaultXmlEncryptor");
        services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be("MiniPainterHub");
    }

    [Theory]
    [InlineData("DataProtection:BlobUri", null)]
    [InlineData("DataProtection:BlobUri", "https://testkeys.blob.core.windows.net/ring")]
    [InlineData("DataProtection:BlobUri", "http://testkeys.blob.core.windows.net/ring/keys.xml")]
    [InlineData("DataProtection:BlobUri", "https://testkeys.blob.core.windows.net.evil.test/ring/keys.xml")]
    [InlineData("DataProtection:BlobUri", "https://testkeys.blob.core.windows.net/ring/keys.xml?sig=private-test-value")]
    [InlineData("DataProtection:BlobUri", "https://user:private-test-value@testkeys.blob.core.windows.net/ring/keys.xml")]
    [InlineData("DataProtection:KeyVaultKeyIdentifier", null)]
    [InlineData("DataProtection:KeyVaultKeyIdentifier", "https://testvault.vault.azure.net/keys/wrapping/specific-version")]
    [InlineData("DataProtection:KeyVaultKeyIdentifier", "https://testvault.vault.azure.net/secrets/wrapping")]
    [InlineData("DataProtection:KeyVaultKeyIdentifier", "https://testvault.vault.azure.net/keys/wrapping#private-test-value")]
    [InlineData("DataProtection:KeyVaultKeyIdentifier", "https://testvault.vault.azure.net:444/keys/wrapping")]
    [InlineData("DataProtection:ManagedIdentityClientId", "private-test-value")]
    [InlineData("DataProtection:Provider", "AzureBlbo")]
    public void InvalidSharedConfiguration_FailsWithoutExposingValues(string key, string? value)
    {
        var settings = SharedSettings();
        settings[key] = value;
        Action configure = () => { using var provider = Configure(settings); };
        var error = configure.Should().Throw<InvalidOperationException>().Which;
        error.Message.Should().NotContain("private-test-value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("FileSystem")]
    public void PartialSharedConfiguration_CannotSilentlyCreateFilesystemRing(string? provider)
    {
        var settings = SharedSettings();
        settings["DataProtection:Provider"] = provider;
        Action configure = () => { using var services = Configure(settings); };
        configure.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FilesystemProvider_PreservesLegacyTokensThroughRestartAndNewKeyGeneration()
    {
        var directory = Directory.CreateTempSubdirectory("MiniPainterHub-KeyContinuity-");
        try
        {
            var settings = new Dictionary<string, string?> { ["DataProtection:KeysPath"] = directory.FullName };
            var legacyServices = new ServiceCollection().AddLogging();
            legacyServices.AddDataProtection().SetApplicationName("MiniPainterHub").PersistKeysToFileSystem(directory);
            string oldToken;
            using (var legacy = legacyServices.BuildServiceProvider())
            {
                oldToken = legacy.GetRequiredService<IDataProtectionProvider>().CreateProtector("migration-test")
                    .Protect("legacy test payload");
            }

            string newToken;
            using (var configured = Configure(settings))
            {
                configured.GetRequiredService<IDataProtectionProvider>().CreateProtector("migration-test")
                    .Unprotect(oldToken).Should().Be("legacy test payload");
                configured.GetRequiredService<IKeyManager>()
                    .CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
            }

            using (var restarted = Configure(settings))
            {
                restarted.GetRequiredService<IKeyManager>().GetAllKeys().Should().HaveCount(2);
                newToken = restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("migration-test")
                    .Protect("new test payload");
                restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("migration-test")
                    .Unprotect(oldToken).Should().Be("legacy test payload");
            }

            using var legacyRestarted = legacyServices.BuildServiceProvider();
            legacyRestarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("migration-test")
                .Unprotect(newToken).Should().Be("new test payload");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void NativeLocalTooling_DoesNotApplyHostedFilesystemPath()
    {
        using var services = Configure(new Dictionary<string, string?>
        {
            ["DataProtection:KeysPath"] = @"D:\home\data-protection-keys"
        }, isLocalTooling: true);
        var repository = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;
        if (repository is FileSystemXmlRepository filesystem)
        {
            filesystem.Directory.FullName.Should().NotBe(Path.GetFullPath(@"D:\home\data-protection-keys"));
        }
    }

    private static Dictionary<string, string?> SharedSettings() => new()
    {
        ["DataProtection:Provider"] = "AzureBlob",
        ["DataProtection:BlobUri"] = "https://testkeys.blob.core.windows.net/keyring/keys.xml",
        ["DataProtection:KeyVaultKeyIdentifier"] = "https://testvault.vault.azure.net/keys/wrapping"
    };

    private static ServiceProvider Configure(Dictionary<string, string?> settings, bool isLocalTooling = false)
    {
        var services = new ServiceCollection().AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        DataProtectionConfiguration.Configure(services, configuration, Path.GetTempPath(), isLocalTooling);
        return services.BuildServiceProvider();
    }
}
