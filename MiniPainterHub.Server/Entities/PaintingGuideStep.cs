namespace MiniPainterHub.Server.Entities;

public class PaintingGuideStep
{
    public int Id { get; set; }
    public int PaintingGuideId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? PaintsUsed { get; set; }
    public string? Techniques { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageStorageKey { get; set; }

    public PaintingGuide PaintingGuide { get; set; } = null!;
}
