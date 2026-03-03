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
        public ContentStatus Status { get; set; } = ContentStatus.Active;
        public string? ModerationNote { get; set; }
        public DateTime? ModeratedAt { get; set; }
        public string? ModeratedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsPinned { get; set; }
        public int PinPriority { get; set; }
        // Navigation:
        public ApplicationUser CreatedBy { get; set; }

        public List<PostImage> Images { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<Like> Likes { get; set; } = new();
    }
}
