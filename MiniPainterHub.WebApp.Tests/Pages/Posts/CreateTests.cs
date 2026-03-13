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
    public async Task Submit_WithTags_PreviewsDistinctTagsAndSendsThemToTheApi()
    {
        CreatePostDto? captured = null;
        this.AddPostStub(new StubPostService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PostDto
                {
                    Id = 124,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                });
            }
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("Tagged title");
        cut.Find("[data-testid='create-post-content']").Change("Tagged content");
        cut.Find("[data-testid='create-post-tags']").Input("glazing, NMM, glazing");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='create-post-tag-chip']").Should().HaveCount(2));

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.Tags.Should().Equal("glazing", "NMM");
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
    public async Task Submit_WhileRequestIsInFlight_DisablesFormAndPreventsDuplicateCreateCalls()
    {
        var submitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubmit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createCalls = 0;

        this.AddPostStub(new StubPostService
        {
            CreateHandler = async dto =>
            {
                createCalls++;
                submitStarted.TrySetResult();
                await releaseSubmit.Task;
                return new PostDto
                {
                    Id = 125,
                    Title = dto.Title,
                    Content = dto.Content,
                    CreatedById = "user-1"
                };
            }
        });

        var cut = RenderComponent<Create>();

        cut.Find("[data-testid='create-post-title']").Change("New title");
        cut.Find("[data-testid='create-post-content']").Change("New content");

        var firstSubmit = cut.Find("[data-testid='create-post-form']").SubmitAsync();
        await submitStarted.Task;

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='create-post-submit']").HasAttribute("disabled").Should().BeTrue();
            cut.Find("[data-testid='create-post-submit']").TextContent.Should().Contain("Creating...");
            cut.Find("[data-testid='create-post-cancel']").HasAttribute("disabled").Should().BeTrue();
        });

        await cut.Find("[data-testid='create-post-form']").SubmitAsync();
        createCalls.Should().Be(1);

        releaseSubmit.TrySetResult();
        await firstSubmit;
    }

    [Fact]
    public void CancelButton_NavigatesToHome()
    {
        this.AddPostStub();
        var cut = RenderComponent<Create>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='create-post-cancel']").Click();

        nav.Uri.Should().Be("http://localhost/");
    }
}
