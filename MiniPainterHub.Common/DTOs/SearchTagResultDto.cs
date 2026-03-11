using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public sealed class SearchTagResultDto
    {
        [Required]
        [StringLength(TagRules.MaxTagLength)]
        public string Name { get; set; } = default!;

        [Required]
        [StringLength(64)]
        public string Slug { get; set; } = default!;

        public int PostCount { get; set; }
    }
}
