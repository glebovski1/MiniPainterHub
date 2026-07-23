using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MiniPainterHub.Server.Features.Seo;

internal sealed partial class PublicSeoDocumentService
{
    private const string SitemapCacheKey = "roll-and-paint:public-sitemap";
    private const string IndexRobots = "index,follow,max-image-preview:large";
    private const string NoIndexRobots = "noindex,nofollow";

    private static readonly IReadOnlyDictionary<string, StaticPageDefinition> StaticPages =
        new Dictionary<string, StaticPageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["/"] = new(
                "Roll & Paint | Miniature Painting Community",
                "Share painted miniatures, works in progress, paint recipes, techniques, and thoughtful critique with a community of miniature painters.",
                "Miniature painting community",
                "Discover painted miniatures, practical paint recipes, works in progress, hobby projects, and generous critique from painters across the community."),
            ["/posts/all"] = new(
                "Explore Miniature Painting Posts | Roll & Paint",
                "Browse painted miniatures, works in progress, paint recipes, techniques, and painting notes shared by the Roll & Paint community.",
                "Explore miniature painting posts",
                "Browse the community archive and discover how other painters approach miniatures, recipes, techniques, and works in progress."),
            ["/posts/top"] = new(
                "Top Miniature Painting Posts | Roll & Paint",
                "See standout painted miniatures and popular painting projects selected by the Roll & Paint community.",
                "Top miniature painting posts",
                "Explore highly rated recent miniatures and painting projects from the community."),
            ["/home"] = new(
                "Community Highlights | Roll & Paint",
                "Discover recent miniature painting highlights, standout models, and community favourites from Roll & Paint.",
                "Community painting highlights",
                "A rotating showcase of recent miniatures and community favourites."),
            ["/projects"] = new(
                "Miniature Painting Projects | Roll & Paint",
                "Follow miniature painting projects from planning and base coats through finished models, armies, terrain, and dioramas.",
                "Miniature painting projects",
                "Follow project diaries, milestones, paint recipes, and finished showcases from miniature painters."),
            ["/guides"] = new(
                "Miniature Painting Guides | Roll & Paint",
                "Read practical miniature painting guides covering paints, techniques, materials, and step-by-step workflows.",
                "Miniature painting guides",
                "Learn from step-by-step painting guides created by the community."),
            ["/news"] = new(
                "Miniature Painting News | Roll & Paint",
                "Read Roll & Paint community updates and source-linked miniature painting hobby news.",
                "Miniature painting news",
                "Community announcements and source-linked updates from the miniature painting hobby."),
            ["/about"] = new(
                "About Roll & Paint",
                "Roll & Paint is a shared workbench where miniature painters show their process, exchange useful critique, and help each other improve.",
                "About Roll & Paint",
                "A shared workbench for miniature painters to show the work behind the finished model."),
            ["/privacy"] = new(
                "Privacy | Roll & Paint",
                "Read how Roll & Paint handles account, profile, community, and service data.",
                "Privacy at Roll & Paint",
                "How Roll & Paint handles information used to operate the miniature painting community."),
            ["/terms"] = new(
                "Terms | Roll & Paint",
                "Read the terms for using the Roll & Paint miniature painting community.",
                "Roll & Paint terms",
                "The rules and responsibilities that apply when using Roll & Paint.")
        };

    private readonly AppDbContext _db;
    private readonly SiteBrandOptions _site;
    private readonly IMemoryCache _cache;

    public PublicSeoDocumentService(
        AppDbContext db,
        IOptions<SiteBrandOptions> site,
        IMemoryCache cache)
    {
        _db = db;
        _site = site.Value;
        _cache = cache;
    }

    public async Task<SeoRouteResolution> ResolveAsync(string? requestPath, CancellationToken cancellationToken)
    {
        var path = NormalizePath(requestPath);

        if (StaticPages.TryGetValue(path, out var staticPage))
        {
            return Public(await BuildStaticPageAsync(path, staticPage, cancellationToken));
        }

        if (TryReadIntRoute(path, "/posts/", out var postId))
        {
            return await ResolvePostAsync(postId, cancellationToken);
        }

        if (TryReadIntRoute(path, "/projects/", out var projectId))
        {
            return await ResolveProjectAsync(projectId, cancellationToken);
        }

        if (TryReadIntRoute(path, "/guides/", out var guideId))
        {
            return await ResolveGuideAsync(guideId, cancellationToken);
        }

        if (TryReadIntRoute(path, "/news/", out var newsId))
        {
            return await ResolveNewsAsync(newsId, cancellationToken);
        }

        if (TryReadStringRoute(path, "/users/", out var userId)
            && !path.EndsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveProfileAsync(userId, cancellationToken);
        }

        if (IsPrivateOrUtilityRoute(path))
        {
            return PrivateOrUtility(path);
        }

        return NotFound(path);
    }

    public string GetRobotsText() => string.Join(
        "\n",
        "User-agent: *",
        "Allow: /",
        "Disallow: /api/",
        "Disallow: /hubs/",
        "Disallow: /healthz",
        string.Empty,
        $"Sitemap: {BuildAbsoluteUrl("/sitemap.xml")}",
        string.Empty);

    public async Task<string> GetSitemapAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(SitemapCacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var entries = StaticPages.Keys
            .Select(path => new SitemapEntry(path, null, null))
            .ToList();

        entries.AddRange(await _db.Posts
            .AsNoTracking()
            .Where(post => !post.IsDeleted)
            .OrderBy(post => post.Id)
            .Select(post => new SitemapEntry(
                $"/posts/{post.Id}",
                post.UpdatedUtc,
                post.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault()))
            .ToListAsync(cancellationToken));

        entries.AddRange(await _db.HobbyProjects
            .AsNoTracking()
            .Where(project => !project.IsHidden
                && project.ArchivedUtc == null
                && project.Entries.Any(entry => !entry.Post.IsDeleted))
            .OrderBy(project => project.Id)
            .Select(project => new SitemapEntry(
                $"/projects/{project.Id}",
                project.UpdatedUtc,
                project.CoverPost != null && !project.CoverPost.IsDeleted
                    ? project.CoverPost.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault()
                    : null))
            .ToListAsync(cancellationToken));

        entries.AddRange(await _db.PaintingGuides
            .AsNoTracking()
            .Where(guide => !guide.IsDeleted)
            .OrderBy(guide => guide.Id)
            .Select(guide => new SitemapEntry(
                $"/guides/{guide.Id}",
                guide.UpdatedUtc,
                guide.Steps.OrderBy(step => step.SortOrder).Select(step => step.ImageUrl).FirstOrDefault()))
            .ToListAsync(cancellationToken));

        entries.AddRange(await _db.NewsAnnouncements
            .AsNoTracking()
            .Where(news => !news.IsDeleted)
            .OrderBy(news => news.Id)
            .Select(news => new SitemapEntry($"/news/{news.Id}", news.UpdatedUtc, null))
            .ToListAsync(cancellationToken));

        var publicProfiles = await _db.Users
            .AsNoTracking()
            .Where(user =>
                _db.Posts.Any(post => post.CreatedById == user.Id && !post.IsDeleted)
                || _db.HobbyProjects.Any(project => project.OwnerUserId == user.Id
                    && !project.IsHidden
                    && project.ArchivedUtc == null
                    && project.Entries.Any(entry => !entry.Post.IsDeleted)))
            .OrderBy(user => user.Id)
            .Select(user => new { user.Id, user.DateJoined, user.AvatarUrl })
            .ToListAsync(cancellationToken);
        entries.AddRange(publicProfiles.Select(user =>
            new SitemapEntry($"/users/{Uri.EscapeDataString(user.Id)}", user.DateJoined, user.AvatarUrl)));

        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace image = "http://www.google.com/schemas/sitemap-image/1.1";
        var root = new XElement(sitemap + "urlset", new XAttribute(XNamespace.Xmlns + "image", image));

        foreach (var entry in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            var element = new XElement(sitemap + "url", new XElement(sitemap + "loc", BuildAbsoluteUrl(entry.Path)));
            if (entry.LastModifiedUtc.HasValue)
            {
                element.Add(new XElement(sitemap + "lastmod", entry.LastModifiedUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")));
            }

            var imageUrl = ResolveImageUrl(entry.ImageUrl);
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                element.Add(new XElement(image + "image", new XElement(image + "loc", imageUrl)));
            }

            root.Add(element);
        }

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        var xml = document.Declaration + Environment.NewLine + document.ToString(SaveOptions.DisableFormatting);
        _cache.Set(SitemapCacheKey, xml, TimeSpan.FromMinutes(15));
        return xml;
    }

    private async Task<PublicSeoDocument> BuildStaticPageAsync(
        string path,
        StaticPageDefinition definition,
        CancellationToken cancellationToken)
    {
        var links = path switch
        {
            "/" or "/posts/all" => await GetPostLinksAsync(top: false, cancellationToken),
            "/posts/top" or "/home" => await GetPostLinksAsync(top: true, cancellationToken),
            "/projects" => await GetProjectLinksAsync(cancellationToken),
            "/guides" => await GetGuideLinksAsync(cancellationToken),
            "/news" => await GetNewsLinksAsync(cancellationToken),
            _ => Array.Empty<SnapshotLink>()
        };

        var snapshot = BuildSnapshot(definition.Heading, definition.Introduction, links);
        var schemaType = path == "/" ? "WebSite" : "CollectionPage";
        if (path is "/about" or "/privacy" or "/terms")
        {
            schemaType = "WebPage";
        }

        return CreateDocument(
            definition.Title,
            definition.Description,
            path,
            schemaType,
            _site.DefaultSocialImagePath,
            snapshot);
    }

    private async Task<SeoRouteResolution> ResolvePostAsync(int postId, CancellationToken cancellationToken)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .Where(item => item.Id == postId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Content,
                item.CreatedUtc,
                item.UpdatedUtc,
                AuthorName = item.CreatedBy.DisplayName ?? item.CreatedBy.UserName ?? "Painter",
                AuthorId = item.CreatedById,
                ImageUrl = item.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (post is null)
        {
            return NotFound($"/posts/{postId}");
        }

        var path = $"/posts/{post.Id}";
        var description = Summarize(post.Content, 158, $"A miniature painting post by {post.AuthorName} on Roll & Paint.");
        var snapshot = BuildArticleSnapshot(
            post.Title,
            $"Painted by {post.AuthorName}",
            description,
            new SnapshotLink($"/users/{Uri.EscapeDataString(post.AuthorId)}", $"More from {post.AuthorName}"));
        var extra = new Dictionary<string, object?>
        {
            ["author"] = new Dictionary<string, object?>
            {
                ["@type"] = "Person",
                ["name"] = post.AuthorName,
                ["url"] = BuildAbsoluteUrl($"/users/{Uri.EscapeDataString(post.AuthorId)}")
            },
            ["datePublished"] = post.CreatedUtc.ToUniversalTime(),
            ["dateModified"] = post.UpdatedUtc.ToUniversalTime()
        };

        return Public(CreateDocument(
            $"{post.Title} | Roll & Paint",
            description,
            path,
            "SocialMediaPosting",
            post.ImageUrl,
            snapshot,
            post.UpdatedUtc,
            extra));
    }

    private async Task<SeoRouteResolution> ResolveProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _db.HobbyProjects
            .AsNoTracking()
            .Include(item => item.OwnerUser)
            .Include(item => item.CoverPost)!
                .ThenInclude(post => post!.Images)
            .Include(item => item.Entries)
                .ThenInclude(entry => entry.Post)
                .ThenInclude(post => post.Images)
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);

        if (project is null
            || project.IsHidden
            || project.ArchivedUtc is not null
            || !project.Entries.Any(entry => !entry.Post.IsDeleted))
        {
            return NotFound($"/projects/{projectId}");
        }

        var ownerName = project.OwnerUser.DisplayName ?? project.OwnerUser.UserName ?? "Painter";
        var imageUrl = project.CoverPost is { IsDeleted: false }
            ? project.CoverPost.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault()
            : project.Entries
                .Where(entry => !entry.Post.IsDeleted)
                .SelectMany(entry => entry.Post.Images)
                .OrderBy(image => image.Id)
                .Select(image => image.ImageUrl)
                .FirstOrDefault();
        var description = Summarize(project.Description, 158, $"A miniature painting project by {ownerName} on Roll & Paint.");
        var path = $"/projects/{project.Id}";
        var snapshot = BuildArticleSnapshot(
            project.Title,
            $"A {project.Kind.ToLowerInvariant()} project by {ownerName}",
            description,
            new SnapshotLink($"/users/{Uri.EscapeDataString(project.OwnerUserId)}", $"More from {ownerName}"));

        return Public(CreateDocument(
            $"{project.Title} | Roll & Paint",
            description,
            path,
            "CreativeWork",
            imageUrl,
            snapshot,
            project.UpdatedUtc,
            new Dictionary<string, object?>
            {
                ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = ownerName },
                ["dateCreated"] = project.CreatedUtc.ToUniversalTime(),
                ["dateModified"] = project.UpdatedUtc.ToUniversalTime()
            }));
    }

    private async Task<SeoRouteResolution> ResolveGuideAsync(int guideId, CancellationToken cancellationToken)
    {
        var guide = await _db.PaintingGuides
            .AsNoTracking()
            .Include(item => item.CreatedBy)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Id == guideId && !item.IsDeleted, cancellationToken);

        if (guide is null)
        {
            return NotFound($"/guides/{guideId}");
        }

        var authorName = guide.CreatedBy.DisplayName ?? guide.CreatedBy.UserName ?? "Painter";
        var imageUrl = guide.Steps.OrderBy(step => step.SortOrder).Select(step => step.ImageUrl).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
        var description = Summarize(guide.Summary, 158, $"A miniature painting guide by {authorName} on Roll & Paint.");
        var path = $"/guides/{guide.Id}";
        var snapshot = BuildArticleSnapshot(
            guide.Title,
            $"A step-by-step guide by {authorName}",
            description,
            new SnapshotLink("/guides", "Browse more painting guides"));

        return Public(CreateDocument(
            $"{guide.Title} | Roll & Paint",
            description,
            path,
            "Article",
            imageUrl,
            snapshot,
            guide.UpdatedUtc,
            new Dictionary<string, object?>
            {
                ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = authorName },
                ["datePublished"] = guide.CreatedUtc.ToUniversalTime(),
                ["dateModified"] = guide.UpdatedUtc.ToUniversalTime()
            }));
    }

    private async Task<SeoRouteResolution> ResolveNewsAsync(int newsId, CancellationToken cancellationToken)
    {
        var news = await _db.NewsAnnouncements
            .AsNoTracking()
            .Include(item => item.CreatedBy)
            .SingleOrDefaultAsync(item => item.Id == newsId && !item.IsDeleted, cancellationToken);

        if (news is null)
        {
            return NotFound($"/news/{newsId}");
        }

        var authorName = news.CreatedBy.DisplayName ?? news.CreatedBy.UserName ?? "Roll & Paint";
        var description = Summarize(news.Summary, 158, "A Roll & Paint community update.");
        var path = $"/news/{news.Id}";
        var snapshot = BuildArticleSnapshot(
            news.Title,
            $"Published by {authorName}",
            description,
            new SnapshotLink("/news", "Browse more community news"));

        return Public(CreateDocument(
            $"{news.Title} | Roll & Paint",
            description,
            path,
            "Article",
            _site.DefaultSocialImagePath,
            snapshot,
            news.UpdatedUtc,
            new Dictionary<string, object?>
            {
                ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = authorName },
                ["datePublished"] = news.PublishedUtc.ToUniversalTime(),
                ["dateModified"] = news.UpdatedUtc.ToUniversalTime()
            }));
    }

    private async Task<SeoRouteResolution> ResolveProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound($"/users/{Uri.EscapeDataString(userId)}");
        }

        var hasPosts = await _db.Posts.AnyAsync(post => post.CreatedById == userId && !post.IsDeleted, cancellationToken);
        var hasProjects = await _db.HobbyProjects.AnyAsync(project => project.OwnerUserId == userId
            && !project.IsHidden
            && project.ArchivedUtc == null
            && project.Entries.Any(entry => !entry.Post.IsDeleted), cancellationToken);

        if (!hasPosts && !hasProjects && string.IsNullOrWhiteSpace(user.Profile?.Bio))
        {
            return NotFound($"/users/{Uri.EscapeDataString(userId)}");
        }

        var name = user.Profile?.DisplayName ?? user.DisplayName ?? user.UserName ?? "Miniature painter";
        var description = Summarize(
            user.Profile?.Bio,
            158,
            $"See miniature painting posts and hobby projects by {name} on Roll & Paint.");
        var path = $"/users/{Uri.EscapeDataString(user.Id)}";
        var snapshot = BuildArticleSnapshot(
            name,
            "Miniature painter on Roll & Paint",
            description,
            new SnapshotLink("/posts/all", "Explore community painting posts"));

        return Public(CreateDocument(
            $"{name} | Roll & Paint",
            description,
            path,
            "profile",
            user.Profile?.AvatarUrl ?? user.AvatarUrl,
            snapshot,
            user.DateJoined,
            new Dictionary<string, object?>
            {
                ["mainEntity"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = name }
            },
            structuredDataType: "ProfilePage"));
    }

    private async Task<IReadOnlyList<SnapshotLink>> GetPostLinksAsync(bool top, CancellationToken cancellationToken)
    {
        var query = _db.Posts.AsNoTracking().Where(post => !post.IsDeleted);
        query = top
            ? query.OrderByDescending(post => post.Likes.Count).ThenByDescending(post => post.CreatedUtc)
            : query.OrderByDescending(post => post.CreatedUtc);

        return await query.Take(12)
            .Select(post => new SnapshotLink($"/posts/{post.Id}", post.Title))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SnapshotLink>> GetProjectLinksAsync(CancellationToken cancellationToken) =>
        await _db.HobbyProjects.AsNoTracking()
            .Where(project => !project.IsHidden
                && project.ArchivedUtc == null
                && project.Entries.Any(entry => !entry.Post.IsDeleted))
            .OrderByDescending(project => project.UpdatedUtc)
            .Take(12)
            .Select(project => new SnapshotLink($"/projects/{project.Id}", project.Title))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<SnapshotLink>> GetGuideLinksAsync(CancellationToken cancellationToken) =>
        await _db.PaintingGuides.AsNoTracking()
            .Where(guide => !guide.IsDeleted)
            .OrderByDescending(guide => guide.UpdatedUtc)
            .Take(12)
            .Select(guide => new SnapshotLink($"/guides/{guide.Id}", guide.Title))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<SnapshotLink>> GetNewsLinksAsync(CancellationToken cancellationToken) =>
        await _db.NewsAnnouncements.AsNoTracking()
            .Where(news => !news.IsDeleted)
            .OrderByDescending(news => news.PublishedUtc)
            .Take(12)
            .Select(news => new SnapshotLink($"/news/{news.Id}", news.Title))
            .ToListAsync(cancellationToken);

    private PublicSeoDocument CreateDocument(
        string title,
        string description,
        string canonicalPath,
        string openGraphType,
        string? imageUrl,
        string snapshotHtml,
        DateTime? lastModifiedUtc = null,
        IReadOnlyDictionary<string, object?>? structuredDataExtra = null,
        string? structuredDataType = null)
    {
        var canonicalUrl = BuildAbsoluteUrl(canonicalPath);
        var resolvedImage = ResolveImageUrl(imageUrl ?? _site.DefaultSocialImagePath);
        var structuredData = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = structuredDataType ?? openGraphType,
            ["name"] = title,
            ["headline"] = title,
            ["description"] = description,
            ["url"] = canonicalUrl,
            ["image"] = resolvedImage
        };

        if (structuredDataExtra is not null)
        {
            foreach (var item in structuredDataExtra)
            {
                structuredData[item.Key] = item.Value;
            }
        }

        return new PublicSeoDocument(
            title,
            description,
            canonicalPath,
            IndexRobots,
            openGraphType,
            resolvedImage,
            JsonSerializer.Serialize(structuredData),
            snapshotHtml,
            lastModifiedUtc);
    }

    private SeoRouteResolution PrivateOrUtility(string path)
    {
        var document = new PublicSeoDocument(
            _site.DefaultTitle,
            _site.DefaultDescription,
            path,
            NoIndexRobots,
            "website",
            ResolveImageUrl(_site.DefaultSocialImagePath),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebPage",
                ["name"] = _site.DefaultTitle,
                ["url"] = BuildAbsoluteUrl(path)
            }),
            BuildSnapshot("Roll & Paint", "This part of the community is available in the interactive application.", Array.Empty<SnapshotLink>()));
        return new SeoRouteResolution(SeoRouteKind.PrivateOrUtility, document, StatusCodes.Status200OK);
    }

    private SeoRouteResolution NotFound(string path)
    {
        var document = new PublicSeoDocument(
            "Page Not Found | Roll & Paint",
            "The requested Roll & Paint page could not be found.",
            path,
            NoIndexRobots,
            "website",
            ResolveImageUrl(_site.DefaultSocialImagePath),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebPage",
                ["name"] = "Page Not Found"
            }),
            BuildSnapshot(
                "Page not found",
                "The trail ran cold. Return to the community feed and keep exploring.",
                new[] { new SnapshotLink("/", "Return to Roll & Paint") }));
        return new SeoRouteResolution(SeoRouteKind.NotFound, document, StatusCodes.Status404NotFound);
    }

    private static SeoRouteResolution Public(PublicSeoDocument document) =>
        new(SeoRouteKind.Public, document, StatusCodes.Status200OK);

    private static string BuildSnapshot(string heading, string introduction, IReadOnlyList<SnapshotLink> links)
    {
        var builder = new StringBuilder();
        builder.Append("<main class=\"seo-snapshot\" data-rp-seo-snapshot><h1>")
            .Append(WebUtility.HtmlEncode(heading))
            .Append("</h1><p>")
            .Append(WebUtility.HtmlEncode(introduction))
            .Append("</p>");

        if (links.Count > 0)
        {
            builder.Append("<nav aria-label=\"Featured public content\"><ul>");
            foreach (var link in links)
            {
                builder.Append("<li><a href=\"")
                    .Append(WebUtility.HtmlEncode(link.Path))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(link.Label))
                    .Append("</a></li>");
            }

            builder.Append("</ul></nav>");
        }

        builder.Append("</main>");
        return builder.ToString();
    }

    private static string BuildArticleSnapshot(string heading, string byline, string introduction, SnapshotLink link) =>
        "<main class=\"seo-snapshot\" data-rp-seo-snapshot><article><h1>"
        + WebUtility.HtmlEncode(heading)
        + "</h1><p>"
        + WebUtility.HtmlEncode(byline)
        + "</p><p>"
        + WebUtility.HtmlEncode(introduction)
        + "</p><p><a href=\""
        + WebUtility.HtmlEncode(link.Path)
        + "\">"
        + WebUtility.HtmlEncode(link.Label)
        + "</a></p></article></main>";

    private string BuildAbsoluteUrl(string path) =>
        new Uri(new Uri(_site.PublicOrigin.TrimEnd('/') + "/", UriKind.Absolute), path.TrimStart('/')).ToString();

    private string? ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            return absolute.ToString();
        }

        return BuildAbsoluteUrl(imageUrl);
    }

    private static string Summarize(string? text, int maxLength, string fallback)
    {
        var normalized = WhitespaceRegex().Replace(text ?? string.Empty, " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var shortened = normalized[..maxLength];
        var lastSpace = shortened.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
        {
            shortened = shortened[..lastSpace];
        }

        return shortened.TrimEnd(' ', '.', ',', ';', ':', '-') + "…";
    }

    private static string NormalizePath(string? path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/" : path;
        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            value = "/" + value;
        }

        return value.Length > 1 ? value.TrimEnd('/') : value;
    }

    private static bool TryReadIntRoute(string path, string prefix, out int id)
    {
        id = 0;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path[prefix.Length..];
        return !remainder.Contains('/') && int.TryParse(remainder, out id) && id > 0;
    }

    private static bool TryReadStringRoute(string path, string prefix, out string value)
    {
        value = string.Empty;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path[prefix.Length..];
        if (string.IsNullOrWhiteSpace(remainder) || remainder.Contains('/'))
        {
            return false;
        }

        value = Uri.UnescapeDataString(remainder);
        return true;
    }

    private static bool IsPrivateOrUtilityRoute(string path) =>
        path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/connections", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/confirm-email", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/feed/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/messages", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/profile", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/register", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/resend-confirmation", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/search", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/support", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/posts/mine", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/posts/new", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/posts/user/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/projects/mine", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/projects/new", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/guides/new", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/edit", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/admin", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record StaticPageDefinition(string Title, string Description, string Heading, string Introduction);

    private sealed record SnapshotLink(string Path, string Label);

    private sealed record SitemapEntry(string Path, DateTime? LastModifiedUtc, string? ImageUrl);
}
