using MiniPainterHub.Server.Identity;
using System;

namespace MiniPainterHub.Server.Entities
{
    public class Like
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        // Navigation:
        public Post Post { get; set; }
        public ApplicationUser User { get; set; }
    }
}
