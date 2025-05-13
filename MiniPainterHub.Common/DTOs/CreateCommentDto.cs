using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateCommentDto
    {
        public int PostId { get; set; }
        public string Text { get; set; } = default!;
    }
}
