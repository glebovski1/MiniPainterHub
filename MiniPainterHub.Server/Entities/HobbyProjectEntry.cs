using System;

namespace MiniPainterHub.Server.Entities;

public sealed class HobbyProjectEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int PostId { get; set; }
    public DateTime LinkedUtc { get; set; }
    public string? MilestoneLabel { get; set; }
    public int? ShowcaseOrder { get; set; }

    public HobbyProject Project { get; set; } = null!;
    public Post Post { get; set; } = null!;
}
