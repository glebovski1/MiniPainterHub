using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Posts;

public class CreateTests : TestContext
{
    [Fact]
    public void RendersCreatePostFormWithStableSelectors()
    {
        this.AddPostStub();

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-form']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-title']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-content']").Should().NotBeNull();
        cut.Find("[data-testid='create-post-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WithoutImages_CreatesPostAndNavigatesToDetails()
    {
        CreatePostDto? captured = null;
        this.AddPostStub(new StubPostService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PostDto
                {
                    Id = 123,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                });
            }
        });

        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");
        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/posts/123");
            captured.Should().NotBeNull();
            captured!.Title.Should().Be("New title");
            captured.Content.Should().Be("New content");
        });
    }

    [Fact]
    public async Task Submit_WhenServiceThrows_ShowsErrorMessage()
    {
        this.AddPostStub(new StubPostService
        {
            CreateHandler = _ => Task.FromException<PostDto>(new InvalidOperationException("Create failed"))
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");
        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='create-post-error']").TextContent.Should().Contain("Create failed"));
    }

    [Fact]
    public void CancelButton_NavigatesToHome()
    {
        this.AddPostStub();
        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("button.btn.btn-secondary").Click();

        nav.Uri.Should().Be("http://localhost/");
    }
}
