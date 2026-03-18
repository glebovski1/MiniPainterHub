using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class ShellPagesTests : TestContext
{
    [Fact]
    public void About_RendersPrimaryCallsToAction()
    {
        var cut = RenderComponent<About>();

        cut.Markup.Should().Contain("MiniPainterHub");
        cut.Markup.Should().Contain("Roadmap highlights");
        cut.Markup.Should().Contain("Profiles, follows, and messaging (Live)");
        cut.FindAll("a").Select(link => link.GetAttribute("href")).Should().Contain("/posts/all");
        cut.FindAll("a").Select(link => link.GetAttribute("href")).Should().Contain("/profile");
    }

    [Fact]
    public void Home_RendersTopPostsSection()
    {
        Services.AddSingleton<MiniPainterHub.WebApp.Services.Interfaces.IPostService>(
            new StubPostService
            {
                GetTopPostsHandler = (_, _) => Task.FromResult<IEnumerable<PostSummaryDto>>(
                    new[]
                    {
                        new PostSummaryDto
                        {
                            Id = 55,
                            Title = "Featured dragon",
                            ImageUrl = "/images/dragon.webp"
                        }
                    })
            });

        var cut = RenderComponent<Home>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Discover the Community's Favourites");
            cut.Markup.Should().Contain("Top Posts");
            cut.Markup.Should().Contain("Featured dragon");
            cut.Markup.Should().Contain("Open ranked showcase");
        });
    }

    [Fact]
    public void TopPosts_RendersRankedShowcaseAndSlides()
    {
        Services.AddSingleton<MiniPainterHub.WebApp.Services.Interfaces.IPostService>(
            new StubPostService
            {
                GetTopPostsHandler = (_, _) => Task.FromResult<IEnumerable<PostSummaryDto>>(
                    new[]
                    {
                        new PostSummaryDto
                        {
                            Id = 77,
                            Title = "Golden knight",
                            ImageUrl = "/images/golden-knight.webp"
                        }
                    })
            });

        var cut = RenderComponent<TopPosts>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Top posts");
            cut.Markup.Should().Contain("Ranked community work");
            cut.Markup.Should().Contain("Golden knight");
        });
    }

    [Fact]
    public void Index_RendersLatestPostsHeadingAndCards()
    {
        this.AddTestAuthorization();
        this.AddModerationStub();
        this.AddPostStub(new StubPostService
        {
            GetAllHandler = (_, _) => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<PostSummaryDto>>(
                true,
                System.Net.HttpStatusCode.OK,
                new PagedResult<PostSummaryDto>
                {
                    Items = new[]
                    {
                        new PostSummaryDto
                        {
                            Id = 1,
                            Title = "Latest post",
                            Snippet = "Newest work",
                            AuthorId = "artist-1",
                            AuthorName = "Artist One",
                            CreatedAt = DateTime.UtcNow
                        }
                    },
                    PageNumber = 1,
                    PageSize = 9,
                    TotalCount = 1
                }))
        });

        var cut = RenderComponent<MiniPainterHub.WebApp.Pages.Index>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Latest Posts");
            cut.Markup.Should().Contain("Latest post");
        });
    }

    [Fact]
    public void AllPosts_AppliesDescendingCreatedAtOrdering()
    {
        this.AddTestAuthorization();
        this.AddModerationStub();
        this.AddPostStub(new StubPostService
        {
            GetAllHandler = (_, _) => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<PostSummaryDto>>(
                true,
                System.Net.HttpStatusCode.OK,
                new PagedResult<PostSummaryDto>
                {
                    Items = new[]
                    {
                        new PostSummaryDto
                        {
                            Id = 1,
                            Title = "Older post",
                            Snippet = "Older",
                            AuthorId = "artist-1",
                            AuthorName = "Artist One",
                            CreatedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
                        },
                        new PostSummaryDto
                        {
                            Id = 2,
                            Title = "Newer post",
                            Snippet = "Newer",
                            AuthorId = "artist-2",
                            AuthorName = "Artist Two",
                            CreatedAt = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc)
                        }
                    },
                    PageNumber = 1,
                    PageSize = 9,
                    TotalCount = 2
                }))
        });

        var cut = RenderComponent<AllPosts>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Explore posts");
            cut.FindAll("h5.card-title").Select(node => node.TextContent).Should().ContainInOrder("Newer post", "Older post");
        });
    }
}
