using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.Auth
{
    public class LoginDto
    {
        public string UserName { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}
