using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateAuthorMarkDto
    {
        [Range(typeof(decimal), "0", "1")]
        public decimal NormalizedX { get; set; }

        [Range(typeof(decimal), "0", "1")]
        public decimal NormalizedY { get; set; }

        [StringLength(64)]
        public string? Tag { get; set; }

        [StringLength(1000)]
        public string? Message { get; set; }
    }
}
