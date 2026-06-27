using MiniPainterHub.Server.Exceptions;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Features.Pagination;

internal static class PaginationGuard
{
    public const int MaxPageSize = 100;

    public static void Validate(int page, int pageSize, string message = "Pagination parameters are invalid.")
    {
        var errors = new Dictionary<string, string[]>();
        AddErrors(errors, page, pageSize);
        ThrowIfInvalid(errors, message);
    }

    public static void ValidatePageSize(int pageSize, string message = "Pagination parameters are invalid.")
    {
        var errors = new Dictionary<string, string[]>();
        AddPageSizeError(errors, pageSize);
        ThrowIfInvalid(errors, message);
    }

    public static void AddErrors(IDictionary<string, string[]> errors, int page, int pageSize)
    {
        if (page < 1)
        {
            errors["page"] = new[] { "Page number must be at least 1." };
        }

        AddPageSizeError(errors, pageSize);
    }

    public static void AddPageSizeError(IDictionary<string, string[]> errors, int pageSize)
    {
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = new[] { $"Page size must be between 1 and {MaxPageSize}." };
        }
    }

    public static void ThrowIfInvalid(IDictionary<string, string[]> errors, string message)
    {
        if (errors.Count > 0)
        {
            throw new DomainValidationException(message, errors);
        }
    }
}
