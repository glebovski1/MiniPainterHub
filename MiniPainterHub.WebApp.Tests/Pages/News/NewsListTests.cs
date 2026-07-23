using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.News;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.News;

public class NewsListTests : BunitContext
{
    [Fact]
    public void WhenAnnouncementsLoad_RendersNewsCards()
    {
        this.AddAuthorization();
        this.AddNewsStub(new StubNewsAnnouncementService
        {
            GetAllHandler = (_, _) => Task.FromResult(new ApiResult<PagedResult<NewsAnnouncementSummaryDto>>(
                true,
                HttpStatusCode.OK,
                new PagedResult<NewsAnnouncementSummaryDto>
                {
                    Items = new List<NewsAnnouncementSummaryDto>
                    {
                        new()
                        {
                            Id = 3,
                            Title = "Challenge week",
                            Summary = "A new painting prompt is live.",
                            AuthorName = "Admin",
                            AuthorId = "admin",
                            PublishedAt = DateTime.UtcNow
                        }
                    },
                    PageNumber = 1,
                    PageSize = 10,
                    TotalCount = 1
                }))
        });

        var cut = Render<NewsList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='news-card']").Should().HaveCount(1);
            cut.Markup.Should().Contain("Challenge week");
            cut.Markup.Should().Contain("A new painting prompt is live.");
        });
    }
}
