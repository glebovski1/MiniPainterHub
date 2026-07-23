using System;
using System.Collections.Generic;
using System.Linq;
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

public class CommentListInfiniteScrollTests : BunitContext
{
    [Fact]
    public async Task ViewerPanel_LoadMoreAppendsCommentsAndOmitsPagination()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var requestedPages = new List<int>();
        AddCommentScenario((page, pageSize) =>
        {
            requestedPages.Add(page);
            return SuccessfulPage(page, pageSize, totalCount: 12);
        });

        var cut = RenderWithAuth(postId: 42, isViewerPanel: true);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='comment-item']").Should().HaveCount(10);
            cut.FindAll("nav[aria-label='Comment pagination']").Should().BeEmpty();
            cut.FindAll("[data-testid='viewer-comments-load-sentinel']").Should().ContainSingle();
        });

        var commentList = cut.FindComponent<CommentList>().Instance;
        await cut.InvokeAsync(commentList.LoadMoreAsync);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='comment-item']").Should().HaveCount(12);
            cut.FindAll("nav[aria-label='Comment pagination']").Should().BeEmpty();
            cut.FindAll("[data-testid='viewer-comments-load-sentinel']").Should().BeEmpty();
            requestedPages.Should().Equal(1, 2);
        });
    }

    [Fact]
    public async Task ViewerPanel_LoadMoreFailurePreservesLoadedCommentsAndOffersRetry()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        AddCommentScenario((page, pageSize) => page == 1
            ? SuccessfulPage(page, pageSize, totalCount: 12)
            : new ApiResult<PagedResult<CommentDto>>(false, HttpStatusCode.ServiceUnavailable, null));

        var cut = RenderWithAuth(postId: 42, isViewerPanel: true);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='comment-item']").Should().HaveCount(10));

        var commentList = cut.FindComponent<CommentList>().Instance;
        await cut.InvokeAsync(commentList.LoadMoreAsync);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='comment-item']").Should().HaveCount(10);
            cut.Find("[data-testid='viewer-comments-load-more-error']").TextContent.Should().Contain("Try again");
            cut.FindAll("nav[aria-label='Comment pagination']").Should().BeEmpty();
        });
    }

    [Fact]
    public void PageThread_KeepsNumberedPagination()
    {
        AddCommentScenario((page, pageSize) => SuccessfulPage(page, pageSize, totalCount: 12));

        var cut = RenderWithAuth(postId: 42, isViewerPanel: false);

        cut.WaitForAssertion(() =>
        {
            cut.Find("nav[aria-label='Comment pagination']").Should().NotBeNull();
            cut.FindAll("[data-testid='viewer-comments-load-sentinel']").Should().BeEmpty();
        });
    }

    private void AddCommentScenario(Func<int, int, ApiResult<PagedResult<CommentDto>>> loadPage)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetRoles("User");
        this.AddModerationStub();
        this.AddCommentStub(new StubCommentService
        {
            GetByPostWithVisibilityHandler = (_, page, pageSize, _, _) =>
                Task.FromResult(loadPage(page, pageSize))
        });
    }

    private IRenderedComponent<IComponent> RenderWithAuth(int postId, bool isViewerPanel)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<CommentList>(0);
                childBuilder.AddAttribute(1, nameof(CommentList.PostId), postId);
                childBuilder.AddAttribute(2, nameof(CommentList.IsViewerPanel), isViewerPanel);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static ApiResult<PagedResult<CommentDto>> SuccessfulPage(int page, int pageSize, int totalCount)
    {
        var firstId = ((page - 1) * pageSize) + 1;
        var count = Math.Clamp(totalCount - firstId + 1, 0, pageSize);
        var comments = Enumerable.Range(firstId, count)
            .Select(id => new CommentDto
            {
                Id = id,
                PostId = 42,
                AuthorId = $"author-{id}",
                AuthorName = $"Painter {id}",
                Content = $"Comment {id}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-id)
            })
            .ToArray();

        return new ApiResult<PagedResult<CommentDto>>(
            true,
            HttpStatusCode.OK,
            new PagedResult<CommentDto>
            {
                Items = comments,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
    }
}
