using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MiniPainterHub.Server.Tests.Infrastructure;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Features;

public class PublicSeoEndpointsTests
{
    [Fact]
    public async Task Home_ReturnsServerRenderedCanonicalMetadataAndSnapshot()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<title data-rp-seo=\"title\">Roll &amp; Paint | Miniature Painting Community</title>");
        html.Should().Contain("<link rel=\"canonical\" href=\"https://rollandpaint.com/\"");
        html.Should().Contain("data-rp-seo-snapshot");
    }

    [Fact]
    public async Task PrivateRoute_ReturnsNoIndexHeadersAndMetadata()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/messages");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Robots-Tag").Should().ContainSingle("noindex, nofollow");
        html.Should().Contain("content=\"noindex,nofollow\"");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsRealNotFoundResponse()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/missing-paint-pot");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        html.Should().Contain("Page Not Found | Roll &amp; Paint");
    }

    [Fact]
    public async Task RobotsAndSitemap_UseCanonicalDomain()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var robots = await client.GetStringAsync("/robots.txt");
        var sitemap = await client.GetStringAsync("/sitemap.xml");

        robots.Should().Contain("Sitemap: https://rollandpaint.com/sitemap.xml");
        sitemap.Should().Contain("<loc>https://rollandpaint.com/</loc>");
        sitemap.Should().NotContain("azurewebsites.net");
    }

    [Fact]
    public async Task NonCanonicalPageHost_RedirectsPermanently_WhileAssetsStayAvailable()
    {
        using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
        {
            ["Site:CanonicalRedirectsEnabled"] = "true"
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var pageRequest = new HttpRequestMessage(HttpMethod.Get, "/projects");
        pageRequest.Headers.Host = "www.rollandpaint.com";
        var pageResponse = await client.SendAsync(pageRequest);

        pageResponse.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        pageResponse.Headers.Location.Should().Be("https://rollandpaint.com/projects");

        using var assetRequest = new HttpRequestMessage(HttpMethod.Get, "/brand/roll-and-paint-mark.svg");
        assetRequest.Headers.Host = "minipainterhub-dqandpbghpgbfgf3.canadacentral-01.azurewebsites.net";
        var assetResponse = await client.SendAsync(assetRequest);

        assetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
