using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests;

public class ProgramStartupTests
{
    private const string ResetToken = "integration-reset-token";

    [Fact]
    public async Task Host_BootsInDevelopment_WithIsolatedInMemoryDatabase()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();

        var environment = factory.Services.GetRequiredService<IHostEnvironment>();
        environment.EnvironmentName.Should().Be(Environments.Development);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.ProviderName.Should().Contain("InMemory");
        }

        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = "seed-user",
                UserName = "seed-user",
                Email = "seed-user@example.test"
            });
            await db.Posts.AddAsync(new MiniPainterHub.Server.Entities.Post
            {
                Id = 99,
                Title = "Seeded",
                Content = "Seeded",
                CreatedById = "seed-user",
                CreatedUtc = System.DateTime.UtcNow,
                UpdatedUtc = System.DateTime.UtcNow,
                Status = MiniPainterHub.Server.Entities.ContentStatus.Active
            });
            await db.SaveChangesAsync();
        });

        using var secondFactory = new IntegrationTestApplicationFactory();
        await secondFactory.ResetDatabaseAsync();
        using var secondScope = secondFactory.Services.CreateScope();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await secondDb.Posts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var factory = new IntegrationTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ResetEndpoint_WhenFlagOrTokenMissing_IsDisabled(bool includeEnabledFlag, bool includeToken)
    {
        var config = new Dictionary<string, string?>();
        if (includeEnabledFlag)
        {
            config["TestSupport:ResetEnabled"] = "true";
        }

        if (includeToken)
        {
            config["TestSupport:ResetToken"] = ResetToken;
        }

        using var factory = new IntegrationTestApplicationFactory(config, IPAddress.Loopback);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/test-support/reset", content: null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ResetEndpoint_WhenTokenHeaderMissing_ReturnsUnauthorized()
    {
        using var factory = CreateResetEnabledFactory(IPAddress.Loopback);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/test-support/reset", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetEndpoint_WhenTokenHeaderInvalid_ReturnsUnauthorized()
    {
        using var factory = CreateResetEnabledFactory(IPAddress.Loopback);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Support-Token", "wrong-token");

        var response = await client.PostAsync("/api/test-support/reset", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetEndpoint_WhenRequestIsNotLoopback_ReturnsForbidden()
    {
        using var factory = CreateResetEnabledFactory(IPAddress.Parse("203.0.113.42"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Support-Token", ResetToken);

        var response = await client.PostAsync("/api/test-support/reset", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResetEndpoint_WhenTokenAndLoopbackValid_ReturnsOk()
    {
        using var factory = CreateResetEnabledFactory(IPAddress.Loopback);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Support-Token", ResetToken);

        var response = await client.PostAsync("/api/test-support/reset", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
    }

    private static IntegrationTestApplicationFactory CreateResetEnabledFactory(IPAddress remoteIp)
    {
        return new IntegrationTestApplicationFactory(
            new Dictionary<string, string?>
            {
                ["TestSupport:ResetEnabled"] = "true",
                ["TestSupport:ResetToken"] = ResetToken
            },
            remoteIp);
    }
}
