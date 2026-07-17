using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Net.Mail;

namespace MiniPainterHub.Server.Options;

public sealed class DiscordAuthenticationOptions
{
    public const string SectionName = "Authentication:Discord";

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string CallbackPath { get; set; } = "/signin-discord";
    public string? PublicOrigin { get; set; }
    public bool UseFakeProvider { get; set; }
    public string FakeEmail { get; set; } = "discord-user@example.test";
    public string FakeSubject { get; set; } = "discord-test-subject";
    public string FakeDisplayName { get; set; } = "Discord Test User";
}

internal sealed class DiscordAuthenticationOptionsValidator : IValidateOptions<DiscordAuthenticationOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public DiscordAuthenticationOptionsValidator(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, DiscordAuthenticationOptions options)
    {
        if (options.UseFakeProvider && !_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
        {
            return ValidateOptionsResult.Fail("Authentication:Discord:UseFakeProvider is permitted only in Development or Test.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.UseFakeProvider
            && (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret)))
        {
            return ValidateOptionsResult.Fail("Authentication:Discord:ClientId and ClientSecret are required when Discord authentication is enabled.");
        }

        if (!Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || origin.PathAndQuery != "/")
        {
            return ValidateOptionsResult.Fail("Authentication:Discord:PublicOrigin must be an HTTPS origin without a path.");
        }

        if (string.IsNullOrWhiteSpace(options.CallbackPath) || !options.CallbackPath.StartsWith("/", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Authentication:Discord:CallbackPath must begin with '/'.");
        }

        if (!MailAddress.TryCreate(_configuration["Site:SupportEmail"], out _))
        {
            return ValidateOptionsResult.Fail("Site:SupportEmail must be a valid email address when Discord authentication is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
