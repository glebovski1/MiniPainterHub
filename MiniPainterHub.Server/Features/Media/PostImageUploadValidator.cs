using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace MiniPainterHub.Server.Features.Media;

internal static class PostImageUploadValidator
{
    public const int MaxImagesPerPost = PostImageUploadRules.MaxImagesPerPost;
    public const long MaxUploadBytes = PostImageUploadRules.MaxUploadBytes;

    public static void Validate(
        IReadOnlyList<IFormFile>? images,
        IReadOnlyList<IFormFile>? thumbnails,
        ImagesOptions imageOptions)
    {
        if (images is null)
        {
            return;
        }

        for (var i = 0; i < images.Count && i < MaxImagesPerPost; i++)
        {
            var image = images[i];
            ValidateSafeFileName(image, "Images");

            if (image.Length > MaxUploadBytes)
            {
                throw new ImageTooLargeException(image.FileName, image.Length, MaxUploadBytes);
            }

            var contentType = ResolveContentType(image);
            if (!PostImageUploadRules.IsAllowedContentType(contentType))
            {
                throw new UnsupportedImageContentTypeException(image.FileName, contentType);
            }

            if (thumbnails is null || i >= thumbnails.Count || thumbnails[i] is not { Length: > 0 } thumb)
            {
                continue;
            }

            ValidateSafeFileName(thumb, "Thumbnails");

            if (thumb.Length > MaxUploadBytes)
            {
                throw new ImageTooLargeException(thumb.FileName, thumb.Length, MaxUploadBytes);
            }

            var thumbnailContentType = ResolveContentType(thumb);
            if (!PostImageUploadRules.IsAllowedContentType(thumbnailContentType))
            {
                throw new UnsupportedImageContentTypeException(thumb.FileName, thumbnailContentType);
            }
        }
    }

    public static string ResolveContentType(IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            return file.ContentType;
        }

        if (file.Headers?.TryGetValue("Content-Type", out StringValues headerValue) == true
            && !StringValues.IsNullOrEmpty(headerValue))
        {
            return headerValue.ToString();
        }

        return string.Empty;
    }

    private static void ValidateSafeFileName(IFormFile file, string fieldName)
    {
        var fileName = file.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var normalized = fileName.Replace('\\', '/');
        if (Path.IsPathRooted(fileName)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new DomainValidationException("Invalid post images.", new Dictionary<string, string[]>
            {
                [fieldName] = new[] { "Image filenames must not include paths or parent directory segments." }
            });
        }
    }
}
