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
        public DateTime? ModeratedUtc { get; set; }
        public string? ModeratedByUserId { get; set; }
        public string? ModerationReason { get; set; }
        public DateTime? SoftDeletedUtc { get; set; }
        // Navigation:
        public ApplicationUser CreatedBy { get; set; }
        public ApplicationUser? ModeratedByUser { get; set; }

        public List<PostImage> Images { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<Like> Likes { get; set; } = new();
        public List<PostTag> PostTags { get; set; } = new();
    }
}
