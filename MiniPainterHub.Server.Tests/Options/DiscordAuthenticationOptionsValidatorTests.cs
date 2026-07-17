using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace MiniPainterHub.Server.Tests.Options;

public sealed class DiscordAuthenticationOptionsValidatorTests
{
    [Fact]
    public void Validate_DisabledProvider_AllowsMissingSecrets()
    {
        var result = CreateValidator(Environments.Production, new Dictionary<string, string?>())
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, new DiscordAuthenticationOptions { Enabled = false });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_EnabledRealProvider_RequiresCredentials()
    {
        var result = CreateValidator(Environments.Production, new Dictionary<string, string?>())
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, new DiscordAuthenticationOptions { Enabled = true });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ClientId");
    }

    [Fact]
    public void Validate_FakeProvider_IsAllowedOnlyInDevelopmentOrTest()
    {
        var values = new Dictionary<string, string?> { ["Site:SupportEmail"] = "support@example.test" };
        var options = new DiscordAuthenticationOptions
        {
            Enabled = true,
            UseFakeProvider = true,
            PublicOrigin = "https://localhost",
            CallbackPath = "/signin-discord"
        };

        CreateValidator(Environments.Development, values).Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
        CreateValidator("Test", values).Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
        CreateValidator(Environments.Production, values).Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_EnabledProvider_AcceptsCompleteConfiguration()
    {
        var options = new DiscordAuthenticationOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CallbackPath = "/signin-discord",
            PublicOrigin = "https://example.test"
        };

        CreateValidator(Environments.Production, new Dictionary<string, string?>
        {
            ["Site:SupportEmail"] = "support@example.test"
        }).Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
    }

    private static DiscordAuthenticationOptionsValidator CreateValidator(
        string environmentName,
        IDictionary<string, string?> values)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new DiscordAuthenticationOptionsValidator(environment.Object, configuration);
    }
}
