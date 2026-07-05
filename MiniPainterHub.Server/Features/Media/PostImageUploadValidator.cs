using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        var totalBytes = images.SumLength();
        if (thumbnails is not null)
        {
            if (imageOptions.Enabled && thumbnails.Any(file => file.Length > 0))
            {
                throw new DomainValidationException("Invalid post images.", new Dictionary<string, string[]>
                {
                    ["Thumbnails"] = new[] { "Server-side image processing is enabled, so client-supplied thumbnails are not accepted." }
                });
            }

            totalBytes += thumbnails.SumLength();
        }

        if (totalBytes > PostImageUploadRules.MaxMultipartBodyBytes)
        {
            throw new ImageTooLargeException("post image request", totalBytes, PostImageUploadRules.MaxMultipartBodyBytes);
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

    private static long SumLength(this IReadOnlyList<IFormFile> files)
    {
        long total = 0;
        for (var i = 0; i < files.Count; i++)
        {
            total += files[i].Length;
        }

        return total;
    }
}
