using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

public class PostDetailsModerationTests : TestContext
{
    [Fact]
    public void WhenPostHasTags_RendersVisibleTagSection()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("reader");

        this.AddCommentStub(new StubCommentService
        {
            GetByPostHandler = (_, _, _) => Task.FromResult(
                new ApiResult<PagedResult<CommentDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<CommentDto>
                    {
                        Items = Array.Empty<CommentDto>(),
                        PageNumber = 1,
                        PageSize = 10,
                        TotalCount = 0
                    }))
        });
        this.AddLikeStub();
        this.AddModerationStub(new StubModerationService());

        Services.AddSingleton(new ApiClient(CreateHttpClient(), new NoOpNotificationService()));

        var cut = RenderWithAuth(postId: 10);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='post-details-tag-section']").TextContent.Should().Contain("Tags");
            cut.Find("[data-testid='post-details-tags']").TextContent.Should().Contain("#glazing");
            cut.Find("[data-testid='post-details-tags']").TextContent.Should().Contain("#nmm");
        });
    }

    [Fact]
    public async Task WhenModeratorClicksHide_CallsModerationServiceAndShowsSuccess()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");

        this.AddCommentStub(new StubCommentService
        {
            GetByPostHandler = (_, _, _) => Task.FromResult(
                new ApiResult<PagedResult<CommentDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<CommentDto>
                    {
                        Items = Array.Empty<CommentDto>(),
                        PageNumber = 1,
                        PageSize = 10,
                        TotalCount = 0
                    }))
        });
        this.AddLikeStub();

        var hideCalled = false;
        var hiddenPostId = 0;
        this.AddModerationStub(new StubModerationService
        {
            HidePostHandler = (postId, _) =>
            {
                hideCalled = true;
                hiddenPostId = postId;
                return Task.FromResult(true);
            }
        });

        Services.AddSingleton(new ApiClient(CreateHttpClient(), new NoOpNotificationService()));

        var cut = RenderWithAuth(postId: 10);
        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='post-title']").TextContent.Should().Be("Post 10");
        });

        await cut.Find("[data-testid='post-inline-hide']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            hideCalled.Should().BeTrue();
            hiddenPostId.Should().Be(10);
            cut.Find("[data-testid='post-inline-moderation-result']").TextContent.Should().Contain("hidden");
        });
    }

    [Fact]
    public void WhenPostIdParameterChanges_ReloadsTheRequestedPost()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("reader");

        this.AddCommentStub(new StubCommentService
        {
            GetByPostHandler = (_, _, _) => Task.FromResult(
                new ApiResult<PagedResult<CommentDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<CommentDto>
                    {
                        Items = Array.Empty<CommentDto>(),
                        PageNumber = 1,
                        PageSize = 10,
                        TotalCount = 0
                    }))
        });
        this.AddLikeStub();
        this.AddModerationStub(new StubModerationService());
        Services.AddSingleton(new ApiClient(CreateHttpClient(), new NoOpNotificationService()));

        var cut = RenderWithAuth(postId: 10);

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='post-title']").TextContent.Should().Be("Post 10"));

        cut.FindComponent<PostDetails>()
            .SetParametersAndRender(parameters => parameters.Add(p => p.PostId, 11));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='post-title']").TextContent.Should().Be("Post 11"));
    }

    [Fact]
    public void WhenPostProvidesTypedImages_RendersGalleryAndFallbackThumbnail()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("reader");

        this.AddCommentStub(new StubCommentService
        {
            GetByPostHandler = (_, _, _) => Task.FromResult(
                new ApiResult<PagedResult<CommentDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<CommentDto>
                    {
                        Items = Array.Empty<CommentDto>(),
                        PageNumber = 1,
                        PageSize = 10,
                        TotalCount = 0
                    }))
        });
        this.AddLikeStub();
        this.AddModerationStub(new StubModerationService());
        Services.AddSingleton(new ApiClient(CreateHttpClient(), new NoOpNotificationService()));

        var cut = RenderWithAuth(postId: 12);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='post-details-image']")
                .GetAttribute("src")
                .Should().Be("https://example.test/uploads/post12_preview.webp");

            var thumbnails = cut.FindAll("[data-testid='post-details-thumbnail']");
            thumbnails.Should().HaveCount(2);
            thumbnails[0].GetAttribute("src").Should().Be("https://example.test/uploads/post12_thumb.webp");
            thumbnails[1].GetAttribute("src").Should().Be("https://example.test/uploads/post12b_thumb.webp");
        });
    }

    private IRenderedFragment RenderWithAuth(int postId)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<PostDetails>(0);
                childBuilder.AddAttribute(1, nameof(PostDetails.PostId), postId);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new PostDetailsHttpHandler())
        {
            BaseAddress = new Uri("https://example.test/")
        };
    }

    private sealed class PostDetailsHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri is not null &&
                request.RequestUri.AbsolutePath.StartsWith("/api/posts/", StringComparison.OrdinalIgnoreCase))
            {
                var idToken = request.RequestUri.AbsolutePath.Substring("/api/posts/".Length);
                if (!int.TryParse(idToken, out var postId))
                {
                    postId = 10;
                }

                var payload = JsonSerializer.Serialize(new PostDto
                {
                    Id = postId,
                    Title = $"Post {postId}",
                    Content = "Body",
                    CreatedById = "author-1",
                    AuthorName = "Author",
                    CreatedAt = DateTime.UtcNow,
                    Images = postId == 12
                        ? new List<PostImageDto>
                        {
                            new()
                            {
                                Id = 1,
                                ImageUrl = "/uploads/post12_max.webp",
                                PreviewUrl = "/uploads/post12_preview.webp",
                                ThumbnailUrl = "/uploads/post12_thumb.webp"
                            },
                            new()
                            {
                                Id = 2,
                                ImageUrl = "/uploads/post12b_max.webp"
                            }
                        }
                        : new List<PostImageDto>(),
                    Tags = new List<TagDto>
                    {
                        new() { Name = "glazing", Slug = "glazing" },
                        new() { Name = "nmm", Slug = "nmm" }
                    }
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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
