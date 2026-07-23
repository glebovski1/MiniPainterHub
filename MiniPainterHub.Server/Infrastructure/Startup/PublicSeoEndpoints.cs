using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Features.Seo;
using MiniPainterHub.Server.Options;
using System;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub;

public partial class Program
{
    private static void UseCanonicalHostRedirect(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var site = context.RequestServices.GetRequiredService<IOptions<SiteBrandOptions>>().Value;
            var methodCanRedirect = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
            if (site.CanonicalRedirectsEnabled
                && methodCanRedirect
                && !IsInfrastructurePath(context.Request.Path)
                && Uri.TryCreate(site.PublicOrigin, UriKind.Absolute, out var publicOrigin)
                && !string.Equals(context.Request.Host.Host, publicOrigin.Host, StringComparison.OrdinalIgnoreCase))
            {
                var destination = site.PublicOrigin.TrimEnd('/')
                    + context.Request.PathBase
                    + context.Request.Path
                    + context.Request.QueryString;
                context.Response.Redirect(destination, permanent: true, preserveMethod: true);
                return;
            }

            await next();
        });
    }

    private static void MapPublicSeoEndpoints(WebApplication app)
    {
        app.MapGet("/robots.txt", (PublicSeoDocumentService seo) =>
            Results.Text(seo.GetRobotsText(), "text/plain", Encoding.UTF8));

        app.MapGet("/sitemap.xml", async (PublicSeoDocumentService seo, HttpContext context) =>
            Results.Text(
                await seo.GetSitemapAsync(context.RequestAborted),
                "application/xml",
                Encoding.UTF8));

        app.MapFallback(async context =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var seo = context.RequestServices.GetRequiredService<PublicSeoDocumentService>();
            var renderer = context.RequestServices.GetRequiredService<PublicSeoShellRenderer>();
            var site = context.RequestServices.GetRequiredService<IOptions<SiteBrandOptions>>().Value;
            var resolution = await seo.ResolveAsync(context.Request.Path.Value, context.RequestAborted);

            context.Response.StatusCode = resolution.StatusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            if (resolution.Kind != SeoRouteKind.Public)
            {
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            }

            if (resolution.Document.LastModifiedUtc.HasValue)
            {
                context.Response.Headers.LastModified = resolution.Document.LastModifiedUtc.Value.ToUniversalTime().ToString("R");
            }

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                var html = await renderer.RenderAsync(resolution.Document, site.PublicOrigin, context.RequestAborted);
                await context.Response.WriteAsync(html, context.RequestAborted);
            }
        });
    }

    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/api")
        || path.StartsWithSegments("/hubs")
        || path.StartsWithSegments("/healthz")
        || path.StartsWithSegments("/readyz")
        || path.StartsWithSegments("/swagger")
        || path.StartsWithSegments("/_framework")
        || path.StartsWithSegments("/_content")
        || path.StartsWithSegments("/brand")
        || path.StartsWithSegments("/css")
        || path.StartsWithSegments("/js")
        || path.StartsWithSegments("/JSHelpers")
        || path.StartsWithSegments("/uploads")
        || System.IO.Path.HasExtension(path.Value);
}
