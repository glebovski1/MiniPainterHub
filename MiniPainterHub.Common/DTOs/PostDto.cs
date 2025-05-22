using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class PostDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
        public string CreatedById { get; set; } = default!;
        public DateTime CreatedAt { get; set; }

        public string? ImageUrl { get; set; }
    }
}
