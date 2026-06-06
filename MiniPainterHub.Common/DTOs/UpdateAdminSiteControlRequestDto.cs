using System;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class UpdateAdminSiteControlRequestDto
    {
        public bool Enabled { get; set; } = true;
        public DateTime? DisabledUntilUtc { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
