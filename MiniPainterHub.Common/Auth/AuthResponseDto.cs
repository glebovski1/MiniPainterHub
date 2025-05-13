using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.Auth
{
    public class AuthResponseDto
    {
        public bool IsSuccess { get; set; }
        public string? Token { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }
}
