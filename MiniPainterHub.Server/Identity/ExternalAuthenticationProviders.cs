using MiniPainterHub.Common.Auth;
using System;

namespace MiniPainterHub.Server.Identity;

public static class ExternalAuthenticationProviders
{
    public const string GoogleSlug = "google";
    public const string DiscordSlug = "discord";

    public static bool TryResolve(string? value, out string provider, out string slug)
    {
        if (string.Equals(value, GoogleSlug, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, ExternalAuthProviderNames.Google, StringComparison.OrdinalIgnoreCase))
        {
            provider = ExternalAuthProviderNames.Google;
            slug = GoogleSlug;
            return true;
        }

        if (string.Equals(value, DiscordSlug, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, ExternalAuthProviderNames.Discord, StringComparison.OrdinalIgnoreCase))
        {
            provider = ExternalAuthProviderNames.Discord;
            slug = DiscordSlug;
            return true;
        }

        provider = string.Empty;
        slug = string.Empty;
        return false;
    }

    public static bool IsAllowed(string? provider) => TryResolve(provider, out _, out _);

    public static string GetSlug(string provider) =>
        TryResolve(provider, out _, out var slug)
            ? slug
            : throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported external authentication provider.");
}
