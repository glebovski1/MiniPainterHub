using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class PaintingGuideDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(1000)]
    public string Summary { get; set; } = default!;

    [StringLength(2000)]
    public string? Materials { get; set; }

    [Required]
    public string CreatedById { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string AuthorName { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public List<PaintingGuideStepDto> Steps { get; set; } = new();
}
