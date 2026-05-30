using Microsoft.AspNetCore.Http;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Imaging;
using MiniPainterHub.Server.Options;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Features.Media;

internal static class PostImageUploadValidator
{
    public const int MaxImagesPerPost = 8;
    public const long MaxUploadBytes = 20L * 1024 * 1024;

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
            if (image.Length > MaxUploadBytes)
            {
                throw new ImageTooLargeException(image.FileName, image.Length, MaxUploadBytes);
            }

            if (imageOptions.Enabled)
            {
                var contentType = ResolveContentType(image);
                if (!ImageContentTypes.IsAllowed(contentType))
                {
                    throw new UnsupportedImageContentTypeException(image.FileName, contentType);
                }
            }

            if (imageOptions.Enabled || thumbnails is null || i >= thumbnails.Count || thumbnails[i] is not { Length: > 0 } thumb)
            {
                continue;
            }

            if (thumb.Length > MaxUploadBytes)
            {
                throw new ImageTooLargeException(thumb.FileName, thumb.Length, MaxUploadBytes);
            }
        }
    }

    private static string ResolveContentType(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType)
            ? "image/jpeg"
            : file.ContentType;
}
