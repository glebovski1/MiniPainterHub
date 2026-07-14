namespace MiniPainterHub.WebApp.Services.Auth;

public static class LocalReturnUrl
{
    public static string? GetQueryParameter(string currentUri, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(currentUri) || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        var queryStart = currentUri.IndexOf('?');
        if (queryStart < 0 || queryStart == currentUri.Length - 1)
        {
            return null;
        }

        var fragmentStart = currentUri.IndexOf('#', queryStart + 1);
        var query = fragmentStart >= 0
            ? currentUri[(queryStart + 1)..fragmentStart]
            : currentUri[(queryStart + 1)..];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var rawName = separator >= 0 ? pair[..separator] : pair;
            if (!string.Equals(Decode(rawName), parameterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return separator >= 0 ? Decode(pair[(separator + 1)..]) : string.Empty;
        }

        return null;
    }

    public static string Normalize(string? value, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        var candidate = value.Trim();
        if (candidate.Contains('\\') || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        // Check root-relative paths before absolute URI parsing. Browser/WASM URI
        // implementations may interpret '/profile' as an absolute file URI.
        if (candidate.StartsWith("/", StringComparison.Ordinal))
        {
            return candidate;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute))
        {
            if (!string.Equals(absolute.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(absolute.Authority, baseUri.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            candidate = absolute.PathAndQuery + absolute.Fragment;
        }

        if (!candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || candidate.Contains('\\'))
        {
            return "/";
        }

        return candidate;
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return string.Empty;
        }
    }
}
