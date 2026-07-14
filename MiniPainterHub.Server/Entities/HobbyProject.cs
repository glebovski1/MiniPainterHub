using MiniPainterHub.Server.Identity;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities;

public sealed class HobbyProject
{
    public int Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? GameSystem { get; set; }
    public string? FactionTheme { get; set; }
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? CoverPostId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    public bool IsHidden { get; set; }
    public DateTime? ModeratedUtc { get; set; }
    public string? ModeratedByUserId { get; set; }
    public string? ModerationReason { get; set; }

    public ApplicationUser OwnerUser { get; set; } = null!;
    public ApplicationUser? ModeratedByUser { get; set; }
    public Post? CoverPost { get; set; }
    public ICollection<HobbyProjectEntry> Entries { get; set; } = new List<HobbyProjectEntry>();
}
