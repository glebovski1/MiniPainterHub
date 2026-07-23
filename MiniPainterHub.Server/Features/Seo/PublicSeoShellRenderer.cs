using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Features.Seo;

internal sealed class PublicSeoShellRenderer
{
    private const string TemplateCacheKey = "roll-and-paint:seo-shell-template";
    private const string SeoStartMarker = "<!--RP:SEO-START-->";
    private const string SeoEndMarker = "<!--RP:SEO-END-->";
    private const string SnapshotStartMarker = "<!--RP:APP-FALLBACK-START-->";
    private const string SnapshotEndMarker = "<!--RP:APP-FALLBACK-END-->";

    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _cache;

    public PublicSeoShellRenderer(IWebHostEnvironment environment, IMemoryCache cache)
    {
        _environment = environment;
        _cache = cache;
    }

    public async Task<string> RenderAsync(PublicSeoDocument document, string publicOrigin, CancellationToken cancellationToken)
    {
        var template = await GetTemplateAsync(cancellationToken);
        var canonicalUrl = BuildAbsoluteUrl(publicOrigin, document.CanonicalPath);
        var imageUrl = string.IsNullOrWhiteSpace(document.ImageUrl)
            ? null
            : BuildAbsoluteUrl(publicOrigin, document.ImageUrl);

        var seo = BuildSeoMarkup(document, canonicalUrl, imageUrl);
        var withSeo = ReplaceMarkedBlock(template, SeoStartMarker, SeoEndMarker, seo);
        return ReplaceMarkedBlock(withSeo, SnapshotStartMarker, SnapshotEndMarker, document.SnapshotHtml);
    }

    private async Task<string> GetTemplateAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(TemplateCacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var file = _environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!file.Exists)
        {
            throw new FileNotFoundException("The published Blazor index.html shell is unavailable.", file.PhysicalPath);
        }

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var template = await reader.ReadToEndAsync(cancellationToken);

        if (!template.Contains(SeoStartMarker, StringComparison.Ordinal)
            || !template.Contains(SeoEndMarker, StringComparison.Ordinal)
            || !template.Contains(SnapshotStartMarker, StringComparison.Ordinal)
            || !template.Contains(SnapshotEndMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Blazor index.html shell is missing Roll & Paint SEO markers.");
        }

        _cache.Set(TemplateCacheKey, template, TimeSpan.FromMinutes(5));
        return template;
    }

    private static string BuildSeoMarkup(PublicSeoDocument document, string canonicalUrl, string? imageUrl)
    {
        var encodedTitle = WebUtility.HtmlEncode(document.Title);
        var encodedDescription = WebUtility.HtmlEncode(document.Description);
        var encodedCanonical = WebUtility.HtmlEncode(canonicalUrl);
        var encodedRobots = WebUtility.HtmlEncode(document.Robots);
        var encodedType = WebUtility.HtmlEncode(document.OpenGraphType);

        var builder = new StringBuilder();
        builder.AppendLine($"    <title data-rp-seo=\"title\">{encodedTitle}</title>");
        builder.AppendLine($"    <meta name=\"description\" content=\"{encodedDescription}\" data-rp-seo=\"description\" />");
        builder.AppendLine("    <meta name=\"application-name\" content=\"Roll &amp; Paint\" />");
        builder.AppendLine($"    <meta name=\"robots\" content=\"{encodedRobots}\" data-rp-seo=\"robots\" />");
        builder.AppendLine($"    <link rel=\"canonical\" href=\"{encodedCanonical}\" data-rp-seo=\"canonical\" />");
        builder.AppendLine("    <meta property=\"og:site_name\" content=\"Roll &amp; Paint\" />");
        builder.AppendLine($"    <meta property=\"og:type\" content=\"{encodedType}\" data-rp-seo=\"og:type\" />");
        builder.AppendLine($"    <meta property=\"og:url\" content=\"{encodedCanonical}\" data-rp-seo=\"og:url\" />");
        builder.AppendLine($"    <meta property=\"og:title\" content=\"{encodedTitle}\" data-rp-seo=\"og:title\" />");
        builder.AppendLine($"    <meta property=\"og:description\" content=\"{encodedDescription}\" data-rp-seo=\"og:description\" />");
        builder.AppendLine("    <meta name=\"twitter:card\" content=\"summary_large_image\" />");
        builder.AppendLine($"    <meta name=\"twitter:title\" content=\"{encodedTitle}\" data-rp-seo=\"twitter:title\" />");
        builder.AppendLine($"    <meta name=\"twitter:description\" content=\"{encodedDescription}\" data-rp-seo=\"twitter:description\" />");

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var encodedImage = WebUtility.HtmlEncode(imageUrl);
            builder.AppendLine($"    <meta property=\"og:image\" content=\"{encodedImage}\" data-rp-seo=\"og:image\" />");
            builder.AppendLine($"    <meta name=\"twitter:image\" content=\"{encodedImage}\" data-rp-seo=\"twitter:image\" />");
        }

        builder.Append("    <script type=\"application/ld+json\" id=\"rp-structured-data\" data-rp-seo=\"structured-data\">")
            .Append(document.StructuredDataJson)
            .AppendLine("</script>");
        return builder.ToString().TrimEnd();
    }

    private static string ReplaceMarkedBlock(string source, string startMarker, string endMarker, string replacement)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException($"SEO shell marker pair {startMarker} / {endMarker} is invalid.");
        }

        var contentStart = start + startMarker.Length;
        return source[..contentStart]
            + Environment.NewLine
            + replacement
            + Environment.NewLine
            + source[end..];
    }

    internal static string BuildAbsoluteUrl(string publicOrigin, string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            {
                return absolute.ToString();
            }

            pathOrUrl = absolute.AbsolutePath;
        }

        return new Uri(new Uri(publicOrigin.TrimEnd('/') + "/", UriKind.Absolute), pathOrUrl.TrimStart('/')).ToString();
    }
}
