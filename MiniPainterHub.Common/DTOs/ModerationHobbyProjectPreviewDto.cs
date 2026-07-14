using System;

namespace MiniPainterHub.Common.DTOs;

public sealed class ModerationHobbyProjectPreviewDto
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DescriptionSnippet { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? ModeratedByUserId { get; set; }
    public DateTime? ModeratedUtc { get; set; }
    public string? ModerationReason { get; set; }
}
