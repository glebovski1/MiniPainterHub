using MiniPainterHub.Server.Exceptions;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Features.Media;

internal static class PostImageStorageKeys
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionsByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = "jpg",
            ["image/jpg"] = "jpg",
            ["image/pjpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/webp"] = "webp",
            ["image/gif"] = "gif",
            ["image/bmp"] = "bmp",
            ["image/x-ms-bmp"] = "bmp",
            ["image/tiff"] = "tiff",
            ["image/x-tiff"] = "tiff"
        };

    public static string CreateImageKey(int postId, int imageIndex, string contentType) =>
        CreateKey(postId, imageIndex, "image", contentType);

    public static string CreateThumbnailKey(int postId, int imageIndex, string contentType) =>
        CreateKey(postId, imageIndex, "thumb", contentType);

    private static string CreateKey(int postId, int imageIndex, string role, string contentType)
    {
        if (!ExtensionsByContentType.TryGetValue(contentType, out var extension))
        {
            throw new UnsupportedImageContentTypeException(role, contentType);
        }

        return $"post-{postId}-{imageIndex}-{role}-{Guid.NewGuid():N}.{extension}";
    }
}
