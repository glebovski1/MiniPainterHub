using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PostViewerDto
    {
        public int PostId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        public string CreatedById { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string AuthorName { get; set; } = default!;

        public DateTime CreatedAt { get; set; }

        public bool CanManageAuthorMarks { get; set; }

        public bool CanAttachCommentMark { get; set; }

        public List<PostViewerImageDto> Images { get; set; } = new();

        public List<AuthorMarkDto> AuthorMarks { get; set; } = new();
    }
}
