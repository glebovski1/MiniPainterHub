using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class Comment
    {
        public int Id { get; set; }
        public int PostId { get; set; }     // FK to Post
        public string AuthorId { get; set; }     // FK to ApplicationUser
        public string Text { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool IsDeleted { get; set; }
        // Navigation:
        public Post Post { get; set; }
        public ApplicationUser Author { get; set; }
    }
}
