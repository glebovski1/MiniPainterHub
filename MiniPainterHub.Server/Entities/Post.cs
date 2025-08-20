using MiniPainterHub.Server.Identity;
using System.Collections.Generic;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class Post
    {
        public int Id { get; set; }
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
        public string CreatedById { get; set; }    // FK to ApplicationUser
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool IsDeleted { get; set; }
        // Navigation:
        public ApplicationUser CreatedBy { get; set; }

        public string? ImageUrl { get; set; }
        public List<Comment> Comments { get; set; }
        public List<Like> Likes { get; set; }


    }
}
