using System;
using System.Collections.Generic;
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

public class CommentListVisibilityTests : TestContext
{
    [Fact]
    public void WhenModeratorChangesVisibilityFilter_CallsCommentServiceWithHiddenFlags()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");
        this.AddModerationStub();

        var calls = new List<(bool IncludeDeleted, bool DeletedOnly)>();
        this.AddCommentStub(new StubCommentService
        {
            GetByPostWithVisibilityHandler = (_, _, _, includeDeleted, deletedOnly) =>
            {
                calls.Add((includeDeleted, deletedOnly));
                return Task.FromResult(new ApiResult<PagedResult<CommentDto>>(true, HttpStatusCode.OK, new PagedResult<CommentDto>
                {
                    Items = new[]
                    {
                        new CommentDto
                        {
                            Id = 1,
                            PostId = 99,
                            AuthorId = "author-1",
                            AuthorName = "author",
                            Content = "Comment",
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = deletedOnly
                        }
                    },
                    PageNumber = 1,
                    PageSize = 10,
                    TotalCount = 1
                }));
            }
        });

        var cut = RenderWithAuth(postId: 99);
        cut.WaitForAssertion(() => calls.Should().NotBeEmpty());
        calls[0].Should().Be((false, false));

        cut.Find("[data-testid='comment-visibility-select']").Change("hidden");

        cut.WaitForAssertion(() =>
        {
            calls.Should().HaveCountGreaterThan(1);
            calls[^1].Should().Be((true, true));
        });
    }

    [Fact]
    public void WhenRegularUser_DoesNotSeeCommentVisibilityFilter()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("user");
        auth.SetRoles("User");
        this.AddModerationStub();

        this.AddCommentStub(new StubCommentService
        {
            GetByPostWithVisibilityHandler = (_, _, _, _, _) => Task.FromResult(new ApiResult<PagedResult<CommentDto>>(true, HttpStatusCode.OK, new PagedResult<CommentDto>
            {
                Items = Array.Empty<CommentDto>(),
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 0
            }))
        });

        var cut = RenderWithAuth(postId: 99);
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='comment-visibility-select']").Should().BeEmpty();
        });
    }

    private IRenderedFragment RenderWithAuth(int postId)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentList>(0);
                childBuilder.AddAttribute(1, nameof(CommentList.PostId), postId);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
