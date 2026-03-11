using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class SearchTests : TestContext
{
    [Fact]
    public void AllTab_RendersOverviewResults()
    {
        this.AddTestAuthorization();
        this.AddModerationStub();
        this.AddSearchStub(new StubSearchService
        {
            GetOverviewHandler = _ => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<SearchOverviewDto?>(true, System.Net.HttpStatusCode.OK, new SearchOverviewDto
            {
                Posts =
                {
                    new PostSummaryDto
                    {
                        Id = 10,
                        Title = "Non-metallic metal",
                        Snippet = "NMM guide",
                        AuthorId = "user-1",
                        AuthorName = "Painter",
                        CreatedAt = DateTime.UtcNow
                    }
                },
                Users =
                {
                    new UserListItemDto
                    {
                        UserId = "user-1",
                        UserName = "painter",
                        DisplayName = "Painter Prime"
                    }
                },
                Tags =
                {
                    new SearchTagResultDto
                    {
                        Name = "nmm",
                        Slug = "nmm",
                        PostCount = 3
                    }
                }
            }))
        });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/search?q=nmm");

        var cut = RenderComponent<Search>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='search-overview-results']").TextContent.Should().Contain("Non-metallic metal");
            cut.Find("[data-testid='search-overview-users']").TextContent.Should().Contain("Painter Prime");
            cut.Find("[data-testid='search-overview-tags']").TextContent.Should().Contain("#nmm");
        });
    }

    [Fact]
    public void TagResult_ClickNavigatesToPostsTabWithTagFilter()
    {
        this.AddTestAuthorization();
        this.AddModerationStub();
        this.AddSearchStub(new StubSearchService
        {
            SearchTagsHandler = (_, page, pageSize) => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<SearchTagResultDto>?>(true, System.Net.HttpStatusCode.OK, new PagedResult<SearchTagResultDto>
            {
                Items = new[]
                {
                    new SearchTagResultDto
                    {
                        Name = "glazing",
                        Slug = "glazing",
                        PostCount = 2
                    }
                },
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = 1
            }))
        });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/search?q=gl&tab=tags");

        var cut = RenderComponent<Search>();

        cut.WaitForElement("[data-testid='search-tag-result']");
        cut.Find("[data-testid='search-tag-result']").Click();

        nav.Uri.Should().Contain("/search?");
        nav.Uri.Should().Contain("tab=posts");
        nav.Uri.Should().Contain("tag=glazing");
        nav.Uri.Should().Contain("q=glazing");
    }
}
