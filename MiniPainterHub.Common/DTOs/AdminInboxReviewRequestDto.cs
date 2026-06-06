using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class AdminInboxReviewRequestDto
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
