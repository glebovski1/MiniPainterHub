using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class LikeDto
    {
        public int PostId { get; set; }
        public int Count { get; set; }
        public bool UserHasLiked { get; set; }
    }
}
