using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class CardListVisibilityTests : TestContext
{
    [Fact]
    public void WhenAdminChangesVisibilityFilter_CallsPostServiceWithHiddenFlags()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");
        this.AddModerationStub();

        var calls = new List<(bool IncludeDeleted, bool DeletedOnly)>();
        this.AddPostStub(new StubPostService
        {
            GetAllWithVisibilityHandler = (_, _, includeDeleted, deletedOnly) =>
            {
                calls.Add((includeDeleted, deletedOnly));
                return Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>
                {
                    Items = new[]
                    {
                        new PostSummaryDto
                        {
                            Id = 1,
                            Title = "Post",
                            Snippet = "Snippet",
                            AuthorName = "author",
                            AuthorId = "author-1",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = deletedOnly
                        }
                    },
                    PageNumber = 1,
                    PageSize = 9,
                    TotalCount = 1
                }));
            }
        });

        var cut = RenderWithAuth();
        cut.WaitForAssertion(() => calls.Should().NotBeEmpty());
        calls[0].Should().Be((false, false));

        cut.Find("[data-testid='post-visibility-select']").Change("hidden");

        cut.WaitForAssertion(() =>
        {
            calls.Should().HaveCountGreaterThan(1);
            calls[^1].Should().Be((true, true));
        });
    }

    [Fact]
    public void WhenRegularUser_DoesNotSeeVisibilityFilter()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("user");
        auth.SetRoles("User");
        this.AddModerationStub();

        this.AddPostStub(new StubPostService
        {
            GetAllWithVisibilityHandler = (_, _, _, _) => Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>
            {
                Items = Array.Empty<PostSummaryDto>(),
                PageNumber = 1,
                PageSize = 9,
                TotalCount = 0
            }))
        });

        var cut = RenderWithAuth();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='post-visibility-select']").Should().BeEmpty();
        });
    }

    [Fact]
    public void PostCards_AreKeyedByPostId()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "MiniPainterHub.WebApp",
            "Shared",
            "CardList.razor"));

        source.Should().Contain("<PostCard @key=\"post.Id\"");
    }

    [Fact]
    public void FirstRenderedPostCardRow_GetsImagePriorityOnlyForLcpCandidates()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("user");
        auth.SetRoles("User");
        this.AddModerationStub();

        this.AddPostStub(new StubPostService
        {
            GetAllWithVisibilityHandler = (_, _, _, _) => Task.FromResult(new ApiResult<PagedResult<PostSummaryDto>>(true, HttpStatusCode.OK, new PagedResult<PostSummaryDto>
            {
                Items = new[]
                {
                    new PostSummaryDto
                    {
                        Id = 11,
                        Title = "First",
                        Snippet = "First snippet",
                        AuthorName = "author",
                        AuthorId = "author-1",
                        CreatedAt = DateTime.UtcNow,
                        ImageUrl = "/uploads/images/first_max.webp",
                        ThumbnailUrl = "/uploads/images/first_thumb.webp"
                    },
                    new PostSummaryDto
                    {
                        Id = 12,
                        Title = "Second",
                        Snippet = "Second snippet",
                        AuthorName = "author",
                        AuthorId = "author-1",
                        CreatedAt = DateTime.UtcNow,
                        ImageUrl = "/uploads/images/second_max.webp",
                        ThumbnailUrl = "/uploads/images/second_thumb.webp"
                    },
                    new PostSummaryDto
                    {
                        Id = 13,
                        Title = "Third",
                        Snippet = "Third snippet",
                        AuthorName = "author",
                        AuthorId = "author-1",
                        CreatedAt = DateTime.UtcNow,
                        ImageUrl = "/uploads/images/third_max.webp",
                        ThumbnailUrl = "/uploads/images/third_thumb.webp"
                    }
                },
                PageNumber = 1,
                PageSize = 9,
                TotalCount = 3
            }))
        });

        var cut = RenderWithAuth();

        cut.WaitForAssertion(() =>
        {
            var images = cut.FindAll("[data-testid='post-card-image']");
            images.Should().HaveCount(3);
            images[0].GetAttribute("loading").Should().Be("eager");
            images[0].GetAttribute("fetchpriority").Should().Be("high");
            images[1].GetAttribute("loading").Should().Be("eager");
            images[1].GetAttribute("fetchpriority").Should().Be("high");
            images[2].GetAttribute("loading").Should().Be("lazy");
            images[2].GetAttribute("fetchpriority").Should().Be("auto");
        });
    }

    private IRenderedFragment RenderWithAuth()
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CardList>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
