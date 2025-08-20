using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class LikeDto
    {
        [Required]
        public int PostId { get; set; }

        [Required]
        public int Count { get; set; }

        [Required]
        public bool UserHasLiked { get; set; }
    }
}
