using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class CreateNewsAnnouncementDto
{
    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(500)]
    public string Summary { get; set; } = default!;

    [Required]
    [StringLength(8000)]
    public string Body { get; set; } = default!;
}
