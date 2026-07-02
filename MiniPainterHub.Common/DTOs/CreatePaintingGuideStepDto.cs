using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class CreatePaintingGuideStepDto
{
    [Required]
    [StringLength(120)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(4000)]
    public string Description { get; set; } = default!;

    [StringLength(1000)]
    public string? PaintsUsed { get; set; }

    [StringLength(1000)]
    public string? Techniques { get; set; }
}
