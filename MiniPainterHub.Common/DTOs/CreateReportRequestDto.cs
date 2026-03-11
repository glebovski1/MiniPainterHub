using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class CreateReportRequestDto
    {
        [Required]
        [StringLength(32)]
        public string ReasonCode { get; set; } = default!;

        [StringLength(1000)]
        public string? Details { get; set; }
    }
}
