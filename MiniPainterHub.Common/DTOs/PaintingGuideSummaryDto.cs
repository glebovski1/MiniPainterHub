using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class PaintingGuideSummaryDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(160)]
    public string Snippet { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string AuthorName { get; set; } = default!;

    public string AuthorId { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public int StepCount { get; set; }

    [StringLength(2048)]
    public string? CoverImageUrl { get; set; }
}
