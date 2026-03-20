using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Notifications;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Posts;

public class PostDetailsViewerTests : TestContext
{
    [Fact]
    public void ViewerIsClosedByDefaultAndPostDetailsLayoutRemainsVisible()
    {
        var cut = RenderWithAuth(CreateScenario());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='post-title']").TextContent.Should().Be("Moonlit skin experiment");
            cut.Find("[data-testid='post-details-gallery']");
            cut.Find("[data-testid='post-details-tag-section']").TextContent.Should().Contain("Tags");
            cut.Find("[data-testid='comment-list-container']");
            cut.FindAll("[data-testid='rich-image-viewer-modal']").Should().BeEmpty();
        });
    }

    [Fact]
    public void ClickingHeroPreviewOpensRichViewerModal()
    {
        var cut = RenderWithAuth(CreateScenario());

        cut.Find("[data-testid='post-details-open-viewer-hero']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rich-image-viewer-modal']");
            cut.Find("[data-testid='rich-image-viewer']");
            cut.Find("[data-testid='viewer-side-panel']");
            cut.Find("[data-testid='viewer-stage-image']");
        });
    }

    [Fact]
    public void ClickingPageThumbnailOpensViewerAtThatImage()
    {
        var scenario = CreateScenario();
        var cut = RenderWithAuth(scenario);

        cut.FindAll("[data-testid='post-details-thumbnail']")[1].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rich-image-viewer-modal']");
            cut.Find("[data-testid='viewer-stage-image']")
                .GetAttribute("src")
                .Should()
                .Contain("moonlit-skin-2-full");
        });
    }

    [Fact]
    public void ClickingPageCommentMarkOpensViewerAndHighlightsCorrectComment()
    {
        var scenario = CreateScenario();
        var markStub = new StubCommentMarkService
        {
            GetByCommentIdHandler = (commentId, _) => Task.FromResult(scenario.CommentMarks[commentId])
        };

        var cut = RenderWithAuth(scenario, markStub);

        FindPageCommentCard(cut, "Moonlight glow anchor")
            .QuerySelector("[data-testid='comment-show-mark']")!
            .Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='rich-image-viewer-modal']");
            cut.Find("[data-testid='viewer-comment-state']").TextContent.Should().Contain("#12");
            cut.Find("[data-testid='viewer-stage-image']")
                .GetAttribute("src")
                .Should()
                .Contain("moonlit-skin-2-full");

            cut.FindAll(".comment-item--active")
                .Should()
                .HaveCount(2)
                .And.OnlyContain(item => item.TextContent.Contains("Moonlight glow anchor", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void SelectingAnotherMarkedCommentReplacesTheActiveMarkerAndImageSwitchClearsIt()
    {
        var scenario = CreateScenario();
        var markRequests = new List<int>();
        var markStub = new StubCommentMarkService
        {
            GetByCommentIdHandler = (commentId, _) =>
            {
                markRequests.Add(commentId);
                return Task.FromResult(scenario.CommentMarks[commentId]);
            }
        };

        var cut = RenderWithAuth(scenario, markStub);

        FindPageCommentCard(cut, "Base cloak transition")
            .QuerySelector("[data-testid='comment-show-mark']")!
            .Click();

        cut.WaitForAssertion(() =>
        {
            markRequests.Should().Equal(11);
            cut.Find("[data-testid='viewer-comment-state']").TextContent.Should().Contain("#11");
            cut.Find("[data-testid='viewer-stage-image']").GetAttribute("src").Should().Contain("moonlit-skin-1-full");
        });

        FindPageCommentCard(cut, "Moonlight glow anchor")
            .QuerySelector("[data-testid='comment-show-mark']")!
            .Click();

        cut.WaitForAssertion(() =>
        {
            markRequests.Should().Equal(11, 12);
            cut.Find("[data-testid='viewer-comment-state']").TextContent.Should().Contain("#12");
            cut.Find("[data-testid='viewer-stage-image']").GetAttribute("src").Should().Contain("moonlit-skin-2-full");
            cut.FindAll(".comment-item--active")
                .Should()
                .OnlyContain(item => item.TextContent.Contains("Moonlight glow anchor", StringComparison.Ordinal));
        });

        cut.FindAll("[data-testid='viewer-thumbnail']")[0].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='viewer-stage-image']").GetAttribute("src").Should().Contain("moonlit-skin-1-full");
            cut.FindAll("[data-testid='viewer-comment-state']").Should().BeEmpty();
            cut.FindAll(".comment-item--active").Should().BeEmpty();
        });
    }

    private IRenderedFragment RenderWithAuth(ViewerScenario scenario, StubCommentMarkService? commentMarkStub = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetClaims(
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "viewer-user"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "viewer"));

        this.AddCommentStub(new StubCommentService
        {
            GetByPostWithVisibilityHandler = (_, page, pageSize, _, _) => Task.FromResult(
                new ApiResult<PagedResult<CommentDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<CommentDto>
                    {
                        Items = scenario.Comments.ToList(),
                        PageNumber = page,
                        PageSize = pageSize,
                        TotalCount = scenario.Comments.Count
                    }))
        });
        this.AddCommentMarkStub(commentMarkStub ?? new StubCommentMarkService
        {
            GetByCommentIdHandler = (commentId, _) => Task.FromResult(scenario.CommentMarks[commentId])
        });
        this.AddLikeStub();
        this.AddModerationStub(new StubModerationService());

        Services.AddSingleton(new ApiClient(CreateHttpClient(scenario), new NoOpNotificationService()));

        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<PostDetails>(0);
                childBuilder.AddAttribute(1, nameof(PostDetails.PostId), scenario.Post.Id);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static IElement FindPageCommentCard(IRenderedFragment cut, string contentSnippet)
    {
        var pageCommentList = cut.FindAll("[data-testid='comment-list-container']").First();
        return pageCommentList
            .QuerySelectorAll("[data-testid='comment-item']")
            .Single(item => item.TextContent.Contains(contentSnippet, StringComparison.Ordinal));
    }

    private static HttpClient CreateHttpClient(ViewerScenario scenario)
    {
        return new HttpClient(new ViewerHttpHandler(scenario))
        {
            BaseAddress = new Uri("https://example.test/")
        };
    }

    private static ViewerScenario CreateScenario()
    {
        var createdAt = new DateTime(2026, 3, 13, 19, 30, 0, DateTimeKind.Utc);

        return new ViewerScenario
        {
            Post = new PostDto
            {
                Id = 19,
                Title = "Moonlit skin experiment",
                Content = "Mixed a touch of blue into the midtone instead of only the shadows. It shifted the whole model into night lighting.",
                CreatedById = "author-1",
                AuthorName = "Mira Sol",
                CreatedAt = createdAt,
                Tags = new List<TagDto>
                {
                    new() { Name = "fantasy", Slug = "fantasy" },
                    new() { Name = "moonlight", Slug = "moonlight" },
                    new() { Name = "skin", Slug = "skin" }
                }
            },
            Viewer = new PostViewerDto
            {
                PostId = 19,
                Title = "Moonlit skin experiment",
                CreatedById = "author-1",
                AuthorName = "Mira Sol",
                CreatedAt = createdAt,
                CanAttachCommentMark = true,
                Images = new List<PostViewerImageDto>
                {
                    new()
                    {
                        Id = 101,
                        ImageUrl = "/images/moonlit-skin-1-full.png",
                        PreviewUrl = "/images/moonlit-skin-1-preview.png",
                        ThumbnailUrl = "/images/moonlit-skin-1-thumb.png",
                        Width = 1600,
                        Height = 900
                    },
                    new()
                    {
                        Id = 102,
                        ImageUrl = "/images/moonlit-skin-2-full.png",
                        PreviewUrl = "/images/moonlit-skin-2-preview.png",
                        ThumbnailUrl = "/images/moonlit-skin-2-thumb.png",
                        Width = 1600,
                        Height = 900
                    }
                },
                AuthorMarks = new List<AuthorMarkDto>
                {
                    new()
                    {
                        Id = 501,
                        PostImageId = 101,
                        NormalizedX = 0.33m,
                        NormalizedY = 0.48m,
                        Tag = "midtone mix",
                        Message = "This blend is meant to stay visible even when the zoom level changes."
                    }
                }
            },
            Comments = new List<CommentDto>
            {
                new()
                {
                    Id = 11,
                    PostId = 19,
                    AuthorId = "viewer-a",
                    AuthorName = "Viewer A",
                    Content = "Base cloak transition feels smooth now.",
                    CreatedAt = createdAt.AddMinutes(25),
                    HasViewerMark = true,
                    MarkedPostImageId = 101
                },
                new()
                {
                    Id = 12,
                    PostId = 19,
                    AuthorId = "viewer-b",
                    AuthorName = "Viewer B",
                    Content = "Moonlight glow anchor should sit closer to the cheekbone.",
                    CreatedAt = createdAt.AddMinutes(31),
                    HasViewerMark = true,
                    MarkedPostImageId = 102
                },
                new()
                {
                    Id = 13,
                    PostId = 19,
                    AuthorId = "viewer-c",
                    AuthorName = "Viewer C",
                    Content = "The base is reading clearly even before the highlight pass.",
                    CreatedAt = createdAt.AddMinutes(39)
                }
            },
            CommentMarks = new Dictionary<int, CommentMarkDto>
            {
                [11] = new CommentMarkDto
                {
                    CommentId = 11,
                    PostImageId = 101,
                    NormalizedX = 0.36m,
                    NormalizedY = 0.52m
                },
                [12] = new CommentMarkDto
                {
                    CommentId = 12,
                    PostImageId = 102,
                    NormalizedX = 0.69m,
                    NormalizedY = 0.41m
                }
            }
        };
    }

    private sealed class ViewerScenario
    {
        public PostDto Post { get; set; } = new();
        public PostViewerDto Viewer { get; set; } = new();
        public IReadOnlyList<CommentDto> Comments { get; set; } = Array.Empty<CommentDto>();
        public IReadOnlyDictionary<int, CommentMarkDto> CommentMarks { get; set; } = new Dictionary<int, CommentMarkDto>();
    }

    private sealed class ViewerHttpHandler(ViewerScenario scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri is not null &&
                request.RequestUri.AbsolutePath.Equals($"/api/posts/{scenario.Post.Id}/viewer", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateJsonResponse(scenario.Viewer));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri is not null &&
                request.RequestUri.AbsolutePath.Equals($"/api/posts/{scenario.Post.Id}", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateJsonResponse(scenario.Post));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage CreateJsonResponse<T>(T payload)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public ValueTask ShowSuccessAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowInfoAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowWarningAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowErrorAsync(string message, string? header = null) => ValueTask.CompletedTask;
        public ValueTask ShowValidationErrorsAsync(IDictionary<string, string[]> errors) => ValueTask.CompletedTask;
    }
}
