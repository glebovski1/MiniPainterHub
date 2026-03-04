using System;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class CommentItemTests : TestContext
{
    [Fact]
    public void WhenModeratorRole_RendersCommentIdBadge()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");
        this.AddModerationStub();

        var comment = new CommentDto
        {
            Id = 123,
            PostId = 7,
            AuthorId = "author-1",
            AuthorName = "author",
            Content = "Text",
            CreatedAt = DateTime.UtcNow
        };

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentItem>(0);
                childBuilder.AddAttribute(1, nameof(CommentItem.Comment), comment);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("[data-testid='comment-id-badge']").TextContent.Should().Be("#123");
    }

    [Fact]
    public async Task WhenModeratorClicksHide_CallsModerationServiceAndInvokesCallback()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");

        var hideCalled = false;
        var moderatedCallbackCalled = false;
        this.AddModerationStub(new StubModerationService
        {
            HideCommentHandler = (_, _) =>
            {
                hideCalled = true;
                return Task.FromResult(true);
            }
        });

        var comment = new CommentDto
        {
            Id = 456,
            PostId = 7,
            AuthorId = "author-1",
            AuthorName = "author",
            Content = "Text",
            CreatedAt = DateTime.UtcNow
        };

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentItem>(0);
                childBuilder.AddAttribute(1, nameof(CommentItem.Comment), comment);
                childBuilder.AddAttribute(2, nameof(CommentItem.OnModerated), EventCallback.Factory.Create(this, () => moderatedCallbackCalled = true));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        await cut.Find("[data-testid='comment-inline-hide']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            hideCalled.Should().BeTrue();
            moderatedCallbackCalled.Should().BeTrue();
            cut.Find("[data-testid='comment-inline-moderation-result']").TextContent.Should().Contain("hidden");
        });
    }

    [Fact]
    public void WhenRegularUser_DoesNotRenderInlineModerationButtons()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("user");
        auth.SetRoles("User");
        this.AddModerationStub();

        var comment = new CommentDto
        {
            Id = 555,
            PostId = 7,
            AuthorId = "author-1",
            AuthorName = "author",
            Content = "Text",
            CreatedAt = DateTime.UtcNow
        };

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentItem>(0);
                childBuilder.AddAttribute(1, nameof(CommentItem.Comment), comment);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.FindAll("[data-testid='comment-inline-hide']").Should().BeEmpty();
        cut.FindAll("[data-testid='comment-inline-restore']").Should().BeEmpty();
    }

    [Fact]
    public void WhenHiddenComment_RendersHiddenBadgeAndRestoreButton()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");
        this.AddModerationStub();

        var comment = new CommentDto
        {
            Id = 556,
            PostId = 7,
            AuthorId = "author-1",
            AuthorName = "author",
            Content = "Text",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = true
        };

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentItem>(0);
                childBuilder.AddAttribute(1, nameof(CommentItem.Comment), comment);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("[data-testid='comment-hidden-badge']").TextContent.Should().Contain("Hidden");
        cut.FindAll("[data-testid='comment-inline-hide']").Should().BeEmpty();
        cut.FindAll("[data-testid='comment-inline-restore']").Should().HaveCount(1);
    }
}
