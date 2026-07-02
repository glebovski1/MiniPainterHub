using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Guides;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Guides;

public class GuideListTests : TestContext
{
    [Fact]
    public void WhenGuidesLoad_RendersGuideCards()
    {
        this.AddTestAuthorization();
        this.AddGuideStub(new StubPaintingGuideService
        {
            GetAllHandler = (_, _) => Task.FromResult(new ApiResult<PagedResult<PaintingGuideSummaryDto>>(
                true,
                HttpStatusCode.OK,
                new PagedResult<PaintingGuideSummaryDto>
                {
                    Items = new List<PaintingGuideSummaryDto>
                    {
                        new()
                        {
                            Id = 5,
                            Title = "Red cloak guide",
                            Snippet = "Paint a red cloak.",
                            AuthorName = "Painter",
                            AuthorId = "user-1",
                            CreatedAt = DateTime.UtcNow,
                            StepCount = 3
                        }
                    },
                    PageNumber = 1,
                    PageSize = 12,
                    TotalCount = 1
                }))
        });

        var cut = RenderComponent<GuideList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='guide-card']").Should().HaveCount(1);
            cut.Markup.Should().Contain("Red cloak guide");
            cut.Markup.Should().Contain("3 step(s)");
        });
    }
}
