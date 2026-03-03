using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class CommentFormTests : TestContext
{
    [Fact]
    public async Task Submit_WhenCreateSucceeds_ClearsInputAndInvokesCallback()
    {
        var callbackInvoked = false;
        this.AddCommentStub(new StubCommentService
        {
            CreateHandler = (postId, dto) => Task.FromResult(new ApiResult<CommentDto?>(
                true,
                HttpStatusCode.Created,
                new CommentDto
                {
                    Id = 11,
                    PostId = postId,
                    Content = dto.Text,
                    AuthorId = "user-1",
                    AuthorName = "user-1"
                }))
        });

        var cut = RenderComponent<CommentForm>(parameters => parameters
            .Add(p => p.PostId, 88)
            .Add(p => p.OnCommentAdded, () =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }));

        cut.Find("[data-testid='comment-input']").Change("Great paint job.");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='comment-input']").GetAttribute("value").Should().BeEmpty();
            callbackInvoked.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Submit_WhenUnauthorized_ShowsSignInPrompt()
    {
        this.AddCommentStub(new StubCommentService
        {
            CreateHandler = (_, _) => Task.FromResult(new ApiResult<CommentDto?>(
                false,
                HttpStatusCode.Unauthorized,
                null))
        });

        var cut = RenderComponent<CommentForm>(parameters => parameters.Add(p => p.PostId, 88));

        cut.Find("[data-testid='comment-input']").Change("A comment");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='comment-error']").TextContent.Should().Contain("Please sign in to add a comment."));
    }

    [Fact]
    public async Task Submit_WhenServerFails_ShowsGenericError()
    {
        this.AddCommentStub(new StubCommentService
        {
            CreateHandler = (_, _) => Task.FromResult(new ApiResult<CommentDto?>(
                false,
                HttpStatusCode.InternalServerError,
                null))
        });

        var cut = RenderComponent<CommentForm>(parameters => parameters.Add(p => p.PostId, 88));

        cut.Find("[data-testid='comment-input']").Change("A comment");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='comment-error']").TextContent.Should().Contain("We couldn't post your comment. Please try again."));
    }
}
