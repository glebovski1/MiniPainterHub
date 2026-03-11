using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Entities
{
    public class Tag
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = default!;
        public string NormalizedName { get; set; } = default!;
        public string Slug { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }

        public List<PostTag> PostTags { get; set; } = new();
    }
}
