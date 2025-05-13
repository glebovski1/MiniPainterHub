using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class CreatePostDto
    {
        public string Title { get; set; } = default!;
        public string Content { get; set; } = default!;
    }
}
