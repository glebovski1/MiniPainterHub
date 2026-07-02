using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Controllers;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class NewsAnnouncementServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenValidAdminUser_PersistsAnnouncementAndReturnsDto()
    {
        await using var context = AppDbContextFactory.Create();
        var admin = TestData.CreateUser("admin-1", "admin");
        admin.Profile = TestData.CreateProfile(admin.Id, "Studio Admin");
        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();
        var service = new NewsAnnouncementService(context);
        var dto = CreateDto();

        var result = await service.CreateAsync(admin.Id, dto);

        result.Title.Should().Be(dto.Title);
        result.Summary.Should().Be(dto.Summary);
        result.Body.Should().Be(dto.Body);
        result.AuthorName.Should().Be("Studio Admin");

        var stored = await context.NewsAnnouncements.SingleAsync();
        stored.CreatedById.Should().Be(admin.Id);
        stored.PublishedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAnnouncementsNewestFirst()
    {
        await using var context = AppDbContextFactory.Create();
        var admin = TestData.CreateUser("admin-1", "admin");
        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();
        var service = new NewsAnnouncementService(context);

        var older = await service.CreateAsync(admin.Id, CreateDto("Older update"));
        var newer = await service.CreateAsync(admin.Id, CreateDto("Newer update"));

        var result = await service.GetAllAsync(1, 10);

        result.Items.Select(item => item.Id).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public void CreateEndpoint_IsRestrictedToAdmins()
    {
        var method = typeof(NewsAnnouncementsController).GetMethod(nameof(NewsAnnouncementsController.Create));

        var authorize = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be("Admin");
    }

    private static CreateNewsAnnouncementDto CreateDto(string title = "Release notes") =>
        new()
        {
            Title = title,
            Summary = "A concise admin update.",
            Body = "Long-form announcement body for the community."
        };
}
