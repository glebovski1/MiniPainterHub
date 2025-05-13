using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class LikeDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string UserId { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
