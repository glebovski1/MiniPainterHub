using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class CreatePaintingGuideDto
{
    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(1000)]
    public string Summary { get; set; } = default!;

    [StringLength(2000)]
    public string? Materials { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(12)]
    public List<CreatePaintingGuideStepDto> Steps { get; set; } = new();
}
