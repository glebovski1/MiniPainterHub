using FluentAssertions;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class FeedServiceTests
{
    [Fact]
    public async Task Feed_OrdersPinnedFirst_ThenScore_WithDiversityCap()
    {
        using var db = AppDbContextFactory.Create();
        db.FeedPolicies.Add(new FeedPolicy { Name = "p", IsActive = true, WRecency = 1, WLikes = 5, WComments = 1, WReportsPenalty = 1, HalfLifeHours = 24, DiversityByAuthor = true, MaxPerAuthorPerPage = 1 });
        db.Posts.AddRange(
            new Post { Id = 1, Title = "Pinned", Content = "x", CreatedById = "a", CreatedUtc = DateTime.UtcNow.AddHours(-5), IsPinned = true, PinPriority = 10 },
            new Post { Id = 2, Title = "A1", Content = "x", CreatedById = "a", CreatedUtc = DateTime.UtcNow.AddHours(-1) },
            new Post { Id = 3, Title = "A2", Content = "x", CreatedById = "a", CreatedUtc = DateTime.UtcNow.AddHours(-2) },
            new Post { Id = 4, Title = "B1", Content = "x", CreatedById = "b", CreatedUtc = DateTime.UtcNow.AddHours(-1) }
        );
        db.Likes.AddRange(new Like { PostId = 2, UserId = "u1" }, new Like { PostId = 2, UserId = "u2" });
        await db.SaveChangesAsync();

        var sut = new FeedService(db);
        var result = await sut.GetFeedAsync(1, 10);

        result.Items.First().Id.Should().Be(1);
        result.Items.Count(x => x.Type == "Post" && (x.Title == "A1" || x.Title == "A2")).Should().Be(1);
    }

    [Fact]
    public async Task Feed_IncludesNonPinnedNews()
    {
        using var db = AppDbContextFactory.Create();
        db.FeedPolicies.Add(new FeedPolicy { Name = "p", IsActive = true, WRecency = 1, WLikes = 1, WComments = 1, WReportsPenalty = 1, HalfLifeHours = 24, DiversityByAuthor = false, MaxPerAuthorPerPage = 10 });
        db.NewsItems.Add(new NewsItem { Title = "General News", BodyMarkdown = "body", PublishAt = DateTime.UtcNow.AddMinutes(-1), IsPinned = false, PinPriority = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Status = ContentStatus.Active });
        await db.SaveChangesAsync();

        var sut = new FeedService(db);
        var result = await sut.GetFeedAsync(1, 10);

        result.Items.Should().Contain(x => x.Type == "News" && x.Title == "General News");
    }
}
