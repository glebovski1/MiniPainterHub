using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class NewsAnnouncementSummaryDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(500)]
    public string Summary { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string AuthorName { get; set; } = default!;

    public string AuthorId { get; set; } = default!;

    public DateTime PublishedAt { get; set; }
}
