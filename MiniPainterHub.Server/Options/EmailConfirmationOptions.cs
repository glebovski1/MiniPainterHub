using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Net.Mail;

namespace MiniPainterHub.Server.Options;

public static class EmailConfirmationProviders
{
    public const string AzureCommunicationServices = "AzureCommunicationServices";
    public const string DevelopmentLog = "DevelopmentLog";
}

public sealed class EmailConfirmationOptions
{
    public const string SectionName = "EmailConfirmation";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = EmailConfirmationProviders.DevelopmentLog;
    public string? PublicOrigin { get; set; }
    public string? Endpoint { get; set; }
    public string? SenderAddress { get; set; }
    public string SenderDisplayName { get; set; } = "MiniPainterHub";
}

internal sealed class EmailConfirmationOptionsValidator : IValidateOptions<EmailConfirmationOptions>
{
    private readonly IHostEnvironment _environment;

    public EmailConfirmationOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, EmailConfirmationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var isLocalTooling = _environment.IsDevelopment()
            || _environment.IsEnvironment("Test")
            || _environment.IsEnvironment("Lighthouse");
        if (string.Equals(options.Provider, EmailConfirmationProviders.DevelopmentLog, StringComparison.Ordinal))
        {
            return isLocalTooling
                ? ValidatePublicOrigin(options, allowHttpLocalhost: true)
                : ValidateOptionsResult.Fail("EmailConfirmation:Provider DevelopmentLog is permitted only in local tooling environments.");
        }

        if (!string.Equals(options.Provider, EmailConfirmationProviders.AzureCommunicationServices, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("EmailConfirmation:Provider must be AzureCommunicationServices or DevelopmentLog.");
        }

        var publicOriginResult = ValidatePublicOrigin(options, allowHttpLocalhost: false);
        if (publicOriginResult.Failed)
        {
            return publicOriginResult;
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || endpoint.PathAndQuery != "/"
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return ValidateOptionsResult.Fail("EmailConfirmation:Endpoint must be an HTTPS origin without a path.");
        }

        if (!MailAddress.TryCreate(options.SenderAddress, out _))
        {
            return ValidateOptionsResult.Fail("EmailConfirmation:SenderAddress must be a valid email address.");
        }

        return string.IsNullOrWhiteSpace(options.SenderDisplayName)
            ? ValidateOptionsResult.Fail("EmailConfirmation:SenderDisplayName is required.")
            : ValidateOptionsResult.Success;
    }

    private static ValidateOptionsResult ValidatePublicOrigin(EmailConfirmationOptions options, bool allowHttpLocalhost)
    {
        if (!Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
            || origin.PathAndQuery != "/"
            || !string.IsNullOrEmpty(origin.Fragment)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || (!string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !(allowHttpLocalhost
                    && string.Equals(origin.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && origin.IsLoopback)))
        {
            return ValidateOptionsResult.Fail("EmailConfirmation:PublicOrigin must be an HTTPS origin without a path.");
        }

        return ValidateOptionsResult.Success;
    }
}
