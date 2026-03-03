using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class AdminModerationApiTests
{
    [Fact]
    public async Task Admin_CanHideAndUnhideContent()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Posts.Add(new Post { Id = 10, Title = "t", Content = "c", CreatedById = "u1" });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateAuthenticatedClient(role: "Admin");

        var hide = await client.PostAsJsonAsync("/api/admin/content/post/10/hide", new { Reason = "r" });
        hide.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unhide = await client.PostAsJsonAsync("/api/admin/content/post/10/unhide", new { Reason = "u" });
        unhide.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Moderator_CannotSuspend()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient(role: "Moderator");
        var response = await client.PostAsJsonAsync("/api/admin/users/u2/suspend", new SetSuspensionDto());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaintenanceMode_ReturnsJson503ForApi_AndHtmlForBrowser()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.AppSettings.Add(new AppSetting { Key = "SiteOnline", Value = "false", UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();
        var api = await client.GetAsync("/api/posts");
        api.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await api.Content.ReadAsStringAsync()).Should().Contain("503");

        var req = new HttpRequestMessage(HttpMethod.Get, "/home");
        req.Headers.Add("Accept", "text/html");
        var html = await client.SendAsync(req);
        html.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await html.Content.ReadAsStringAsync()).Should().Contain("Maintenance");
    }

    [Fact]
    public async Task PostingDisabled_PreventsPostCreation()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.AppSettings.Add(new AppSetting { Key = "PostingEnabled", Value = "false", UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/posts", new CreatePostDto { Title = "t", Content = "c" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestrictedUser_CannotComment_OrUpload()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Posts.Add(new Post { Id = 4, Title = "t", Content = "c", CreatedById = "owner" });
            db.UserRestrictions.Add(new UserRestriction { UserId = TestAuthHandler.DefaultUserId, CanComment = false, CanPostImages = false, CanPost = true });
            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient();
        var comment = await client.PostAsJsonAsync("/api/posts/4/comments", new CreateCommentDto { PostId = 4, Text = "x" });
        comment.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("title"), "Title");
        form.Add(new StringContent("body"), "Content");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("img")), "Images", "a.png");
        var upload = await client.PostAsync("/api/posts/with-image", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
