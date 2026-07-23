using Microsoft.Extensions.Options;
using System;

namespace MiniPainterHub.Server.Options;

public sealed class SiteBrandOptions
{
    public const string SectionName = "Site";

    public string BrandName { get; set; } = "Roll & Paint";

    public string PublicOrigin { get; set; } = "https://rollandpaint.com";

    public string DefaultTitle { get; set; } = "Roll & Paint | Miniature Painting Community";

    public string DefaultDescription { get; set; } =
        "A miniature painting community for sharing painted miniatures, works in progress, paint recipes, techniques, and thoughtful critique.";

    public string DefaultSocialImagePath { get; set; } = "/brand/roll-and-paint-social.png";

    public string? SupportEmail { get; set; }

    public bool CanonicalRedirectsEnabled { get; set; }
}

public sealed class SiteBrandOptionsValidator : IValidateOptions<SiteBrandOptions>
{
    public ValidateOptionsResult Validate(string? name, SiteBrandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BrandName))
        {
            return ValidateOptionsResult.Fail("Site:BrandName is required.");
        }

        if (!Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin)
            || !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || origin.PathAndQuery != "/"
            || !string.IsNullOrEmpty(origin.Fragment)
            || !string.IsNullOrEmpty(origin.UserInfo))
        {
            return ValidateOptionsResult.Fail("Site:PublicOrigin must be an HTTPS origin without a path.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultTitle)
            || string.IsNullOrWhiteSpace(options.DefaultDescription))
        {
            return ValidateOptionsResult.Fail("Site:DefaultTitle and Site:DefaultDescription are required.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultSocialImagePath)
            || !options.DefaultSocialImagePath.StartsWith("/", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("Site:DefaultSocialImagePath must be an app-relative path beginning with '/'.");
        }

        return ValidateOptionsResult.Success;
    }
}
