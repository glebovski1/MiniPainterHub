using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Services.Interfaces;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Posts;

public class TopPostsTests : TestContext
{
    [Fact]
    public void WhenTopPostHasTags_ImageCaptionIncludesTags()
    {
        var postService = new StubPostService
        {
            GetTopPostsHandler = (_, _) => Task.FromResult<IEnumerable<PostSummaryDto>>(
                new[]
                {
                    new PostSummaryDto
                    {
                        Id = 77,
                        Title = "Neon lens test",
                        ImageUrl = "/uploads/images/post77_max.webp",
                        Tags = new List<TagDto>
                        {
                            new() { Name = "glazing", Slug = "glazing" },
                            new() { Name = "nmm", Slug = "nmm" }
                        }
                    }
                })
        };

        Services.AddSingleton<IPostService>(postService);

        var cut = RenderComponent<TopPosts>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Neon lens test");
            cut.Markup.Should().Contain("#glazing");
            cut.Markup.Should().Contain("#nmm");
        });
    }
}
