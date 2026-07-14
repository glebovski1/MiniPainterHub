using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using MiniPainterHub.Server.Options;
using System.Collections.Generic;
using Xunit;

namespace MiniPainterHub.Server.Tests.Options;

public sealed class GoogleAuthenticationOptionsValidatorTests
{
    [Fact]
    public void Validate_DisabledProvider_AllowsMissingSecrets()
    {
        var validator = CreateValidator(Environments.Production, new Dictionary<string, string?>());

        var result = validator.Validate(Microsoft.Extensions.Options.Options.DefaultName, new GoogleAuthenticationOptions { Enabled = false });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_EnabledRealProvider_RequiresCredentialsOriginAndSupportEmail()
    {
        var validator = CreateValidator(Environments.Production, new Dictionary<string, string?>());

        var result = validator.Validate(Microsoft.Extensions.Options.Options.DefaultName, new GoogleAuthenticationOptions { Enabled = true });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ClientId");
    }

    [Fact]
    public void Validate_FakeProvider_IsAllowedOnlyInDevelopmentOrTest()
    {
        var configuration = new Dictionary<string, string?> { ["Site:SupportEmail"] = "support@example.test" };
        var options = new GoogleAuthenticationOptions
        {
            Enabled = true,
            UseFakeProvider = true,
            PublicOrigin = "https://localhost",
            CallbackPath = "/signin-google"
        };

        CreateValidator(Environments.Development, configuration)
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
        CreateValidator("Test", configuration)
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
        var production = CreateValidator(Environments.Production, configuration)
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, options);
        production.Failed.Should().BeTrue();
        production.FailureMessage.Should().Contain("Development or Test");
    }

    [Fact]
    public void Validate_EnabledProvider_AcceptsCompletePilotConfiguration()
    {
        var validator = CreateValidator(Environments.Production, new Dictionary<string, string?>
        {
            ["Site:SupportEmail"] = "support@example.test"
        });
        var options = new GoogleAuthenticationOptions
        {
            Enabled = true,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CallbackPath = "/signin-google",
            PublicOrigin = "https://example.test"
        };

        validator.Validate(Microsoft.Extensions.Options.Options.DefaultName, options).Succeeded.Should().BeTrue();
    }

    private static GoogleAuthenticationOptionsValidator CreateValidator(
        string environmentName,
        IDictionary<string, string?> values)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new GoogleAuthenticationOptionsValidator(environment.Object, configuration);
    }
}
