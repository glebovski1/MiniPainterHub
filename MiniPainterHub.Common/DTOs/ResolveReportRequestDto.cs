using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class ResolveReportRequestDto
    {
        [Required]
        [StringLength(32)]
        public string Status { get; set; } = default!;

        [StringLength(500)]
        public string? ResolutionNote { get; set; }
    }
}
