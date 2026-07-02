using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities;

public class NewsAnnouncement
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string CreatedById { get; set; } = string.Empty;
    public DateTime PublishedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }

    public ApplicationUser CreatedBy { get; set; } = null!;
}
