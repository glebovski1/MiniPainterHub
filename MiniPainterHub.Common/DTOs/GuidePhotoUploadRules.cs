using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs;

public static class GuidePhotoUploadRules
{
    public const int MaxStepPhotosPerGuide = 12;
    public const long MaxUploadBytes = PostImageUploadRules.MaxUploadBytes;
    public const long MaxMultipartBodyBytes = MaxStepPhotosPerGuide * MaxUploadBytes + PostImageUploadRules.MultipartFormOverheadBytes;
    public const string AllowedContentTypesLabel = PostImageUploadRules.AllowedContentTypesLabel;

    public static IReadOnlyCollection<string> AllowedContentTypes => PostImageUploadRules.AllowedContentTypes;

    public static bool IsAllowedContentType(string? contentType) =>
        PostImageUploadRules.IsAllowedContentType(contentType);
}
