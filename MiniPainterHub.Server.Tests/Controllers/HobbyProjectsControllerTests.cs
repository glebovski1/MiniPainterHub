using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class HobbyProjectsControllerTests
{
    [Fact]
    public async Task Create_WhenAnonymous_ReturnsUnauthorized()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", NewProject("Anonymous"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyProject_IsVisibleToOwnerButReturnsNotFoundToOthers()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "owner");
        await SeedUserAsync(factory, "visitor");
        using var owner = factory.CreateAuthenticatedClient("owner", "owner");
        var create = await owner.PostAsJsonAsync("/api/projects", NewProject("Owner project"));
        var project = await create.Content.ReadFromJsonAsync<HobbyProjectDto>();
        using var visitor = factory.CreateAuthenticatedClient("visitor", "visitor");

        (await owner.GetAsync($"/api/projects/{project!.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await visitor.GetAsync($"/api/projects/{project.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DuplicateLink_ReturnsConflictWithCurrentProjectReference()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "owner");
        await SeedPostAsync(factory, 41, "owner");
        using var client = factory.CreateAuthenticatedClient("owner", "owner");
        var create = await client.PostAsJsonAsync("/api/projects", NewProject("Owner project"));
        var project = await create.Content.ReadFromJsonAsync<HobbyProjectDto>();
        var link = new LinkHobbyProjectPostDto { PostId = 41 };
        (await client.PostAsJsonAsync($"/api/projects/{project!.Id}/posts", link)).StatusCode.Should().Be(HttpStatusCode.OK);

        var conflict = await client.PostAsJsonAsync($"/api/projects/{project.Id}/posts", link);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        var current = document.RootElement.GetProperty("currentProject");
        current.GetProperty("id").GetInt32().Should().Be(project.Id);
        current.GetProperty("title").GetString().Should().Be("Owner project");
        current.GetProperty("isPublic").GetBoolean().Should().BeTrue();
    }

    private static CreateHobbyProjectDto NewProject(string title) =>
        new()
        {
            Title = title,
            Description = "Project description",
            Kind = HobbyProjectKinds.Miniature
        };

    private static Task SeedUserAsync(IntegrationTestApplicationFactory factory, string userId) =>
        factory.ExecuteDbContextAsync(async db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                Email = $"{userId}@example.test",
                NormalizedEmail = $"{userId}@example.test".ToUpperInvariant()
            });
            await db.SaveChangesAsync();
        });

    private static Task SeedPostAsync(IntegrationTestApplicationFactory factory, int postId, string userId) =>
        factory.ExecuteDbContextAsync(async db =>
        {
            db.Posts.Add(new Post
            {
                Id = postId,
                CreatedById = userId,
                Title = "Progress post",
                Content = "Progress content",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
}
