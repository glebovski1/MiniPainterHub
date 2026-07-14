using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreatePostDto
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = default!;

        [StringLength(120)]
        public string? MiniatureName { get; set; }

        [StringLength(2000)]
        public string? PaintsUsed { get; set; }

        [StringLength(1000)]
        public string? Techniques { get; set; }

        [StringLength(40)]
        public string? Difficulty { get; set; }

        [StringLength(80)]
        public string? TimeSpent { get; set; }

        [MaxLength(TagRules.MaxTagsPerPost)]
        public List<string> Tags { get; set; } = new();

        [MaxLength(PostImageUploadRules.MaxImagesPerPost)]
        public List<PostImageDto>? Images { get; set; }

        public int? ProjectId { get; set; }

        [StringLength(HobbyProjectRules.MaxMilestoneLabelLength)]
        public string? MilestoneLabel { get; set; }
    }
}
