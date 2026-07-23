using System;

namespace MiniPainterHub.Server.Features.Seo;

internal enum SeoRouteKind
{
    Public,
    PrivateOrUtility,
    NotFound
}

internal sealed record PublicSeoDocument(
    string Title,
    string Description,
    string CanonicalPath,
    string Robots,
    string OpenGraphType,
    string? ImageUrl,
    string StructuredDataJson,
    string SnapshotHtml,
    DateTime? LastModifiedUtc = null);

internal sealed record SeoRouteResolution(
    SeoRouteKind Kind,
    PublicSeoDocument Document,
    int StatusCode);
