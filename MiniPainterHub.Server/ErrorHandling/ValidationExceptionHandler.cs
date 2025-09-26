using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Exceptions;

namespace MiniPainterHub.Server.ErrorHandling;

public sealed class ValidationExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ValidationExceptionHandler> _logger;

    public ValidationExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ValidationExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, System.Exception exception, CancellationToken cancellationToken)
    {
        var errors = exception switch
        {
            ValidationException fluent => fluent.Errors
                .GroupBy(e => e.PropertyName ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
            DomainValidationException domain => new Dictionary<string, string[]>(domain.Errors, System.StringComparer.OrdinalIgnoreCase),
            _ => null
        };

        if (errors is null)
        {
            return false;
        }

        _logger.LogWarning(exception, "Validation failure for request {RequestId}", httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Title = "Validation error",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        }, cancellationToken);
    }
}
