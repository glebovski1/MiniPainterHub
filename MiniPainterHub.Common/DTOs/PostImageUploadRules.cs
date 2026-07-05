using System;
using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs
{
    public static class PostImageUploadRules
    {
        private static readonly HashSet<string> AllowedContentTypesSet = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/jpg",
            "image/pjpeg",
            "image/png",
            "image/webp"
        };

        public const int MaxImagesPerPost = 8;
        public const long MaxUploadBytes = 20L * 1024 * 1024;
        public const long MaxMultipartBodyBytes = MaxImagesPerPost * MaxUploadBytes + MultipartFormOverheadBytes;
        public const long MultipartFormOverheadBytes = 2L * 1024 * 1024;
        public const string AllowedContentTypesLabel = "JPEG, PNG, or WEBP";

        public static IReadOnlyCollection<string> AllowedContentTypes => AllowedContentTypesSet;

        public static bool IsAllowedContentType(string? contentType) =>
            !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypesSet.Contains(contentType);
    }
}
