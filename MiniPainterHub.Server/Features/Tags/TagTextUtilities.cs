using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MiniPainterHub.Server.Features.Tags;

internal static class TagTextUtilities
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string CollapseWhitespace(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return WhitespaceRegex.Replace(value.Trim(), " ");
    }

    public static string NormalizeText(string value) =>
        CollapseWhitespace(value).ToLowerInvariant();

    public static string CreateSlug(string value)
    {
        var normalized = NormalizeText(value);
        var builder = new StringBuilder(normalized.Length);
        var lastWasHyphen = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasHyphen = false;
                continue;
            }

            if ((char.IsWhiteSpace(ch) || ch is '-' or '_') && !lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static string ResolveUniqueSlug(string baseSlug, ISet<string> usedSlugs)
    {
        var candidate = baseSlug;
        var suffix = 2;
        while (usedSlugs.Contains(candidate))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
