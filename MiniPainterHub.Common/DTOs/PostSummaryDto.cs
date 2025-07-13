using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class PostSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string Snippet { get; set; } = default!;   // e.g. first 100 chars
        public string? ImageUrl { get; set; }
        public string AuthorName { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public int CommentCount { get; set; }
        public int LikeCount { get; set; }
    }
}
