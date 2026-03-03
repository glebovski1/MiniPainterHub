using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.ErrorHandling;
using MiniPainterHub.Server.Exceptions;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.ErrorHandling;

public class ValidationExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WhenDomainValidationException_WritesProblemDetails()
    {
        var problemDetailsService = new Mock<IProblemDetailsService>();
        ProblemDetailsContext? captured = null;
        problemDetailsService.Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Returns((ProblemDetailsContext context) =>
            {
                captured = context;
                return ValueTask.FromResult(true);
            });

        var handler = new ValidationExceptionHandler(problemDetailsService.Object, Mock.Of<ILogger<ValidationExceptionHandler>>());
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "req-123";
        httpContext.Request.Path = "/api/posts";

        var exception = new DomainValidationException("Invalid post.", new Dictionary<string, string[]>
        {
            ["Title"] = new[] { "Title is required." }
        });

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        captured.Should().NotBeNull();
        captured!.ProblemDetails.Title.Should().Be("Validation error");
        captured.ProblemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        captured.ProblemDetails.Instance.Should().Be("/api/posts");
        captured.ProblemDetails.Extensions.Should().ContainKey("errors");
    }

    [Fact]
    public async Task TryHandleAsync_WhenFluentValidationException_GroupsErrorsByProperty()
    {
        var problemDetailsService = new Mock<IProblemDetailsService>();
        ProblemDetailsContext? captured = null;
        problemDetailsService.Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Returns((ProblemDetailsContext context) =>
            {
                captured = context;
                return ValueTask.FromResult(true);
            });

        var handler = new ValidationExceptionHandler(problemDetailsService.Object, Mock.Of<ILogger<ValidationExceptionHandler>>());
        var httpContext = new DefaultHttpContext();
        var validationException = new ValidationException(new[]
        {
            new ValidationFailure("Title", "Required"),
            new ValidationFailure("Title", "Too short"),
            new ValidationFailure("Content", "Required")
        });

        var handled = await handler.TryHandleAsync(httpContext, validationException, CancellationToken.None);

        handled.Should().BeTrue();
        captured.Should().NotBeNull();

        var errors = captured!.ProblemDetails.Extensions["errors"].Should().BeOfType<Dictionary<string, string[]>>().Subject;
        errors["Title"].Should().Contain(new[] { "Required", "Too short" });
        errors["Content"].Should().Contain("Required");
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsNotValidation_ReturnsFalse()
    {
        var problemDetailsService = new Mock<IProblemDetailsService>();
        var handler = new ValidationExceptionHandler(problemDetailsService.Object, Mock.Of<ILogger<ValidationExceptionHandler>>());
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new System.Exception("boom"), CancellationToken.None);

        handled.Should().BeFalse();
        problemDetailsService.Verify(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }
}
