using MiniPainterHub.Server.Identity;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities;

public class PaintingGuide
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string? Materials { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }

    public ApplicationUser CreatedBy { get; set; } = null!;
    public List<PaintingGuideStep> Steps { get; set; } = new();
}
