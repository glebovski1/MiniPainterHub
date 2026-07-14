using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Net.Mail;

namespace MiniPainterHub.Server.Options;

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "Authentication:Google";

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string CallbackPath { get; set; } = "/signin-google";
    public string? PublicOrigin { get; set; }
    public bool UseFakeProvider { get; set; }
    public string FakeEmail { get; set; } = "google-user@example.test";
    public string FakeSubject { get; set; } = "google-test-subject";
    public string FakeDisplayName { get; set; } = "Google Test User";
}

internal sealed class GoogleAuthenticationOptionsValidator : IValidateOptions<GoogleAuthenticationOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public GoogleAuthenticationOptionsValidator(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public ValidateOptionsResult Validate(string? name, GoogleAuthenticationOptions options)
    {
        if (options.UseFakeProvider && !_environment.IsDevelopment() && !_environment.IsEnvironment("Test"))
        {
            return ValidateOptionsResult.Fail("Authentication:Google:UseFakeProvider is permitted only in Development or Test.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.UseFakeProvider
            && (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret)))
        {
            return ValidateOptionsResult.Fail("Authentication:Google:ClientId and ClientSecret are required when Google authentication is enabled.");
        }

        if (!Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || origin.PathAndQuery != "/")
        {
            return ValidateOptionsResult.Fail("Authentication:Google:PublicOrigin must be an HTTPS origin without a path.");
        }

        if (string.IsNullOrWhiteSpace(options.CallbackPath) || !options.CallbackPath.StartsWith("/", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Authentication:Google:CallbackPath must begin with '/'.");
        }

        if (!MailAddress.TryCreate(_configuration["Site:SupportEmail"], out _))
        {
            return ValidateOptionsResult.Fail("Site:SupportEmail must be a valid email address when Google authentication is enabled.");
        }

        return ValidateOptionsResult.Success;
    }
}
