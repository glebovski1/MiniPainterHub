using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CommentMarkDto
    {
        public int CommentId { get; set; }

        public int PostImageId { get; set; }

        [Range(typeof(decimal), "0", "1")]
        public decimal NormalizedX { get; set; }

        [Range(typeof(decimal), "0", "1")]
        public decimal NormalizedY { get; set; }
    }
}
