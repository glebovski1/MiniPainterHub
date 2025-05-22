using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateImagePostDto
    {
        public string Title { get; set; }
        public string Content { get; set; }

        // Bound from multipart/form-data under the “image” field
        [FromForm(Name = "image")]
        public IFormFile? Image { get; set; }
    }
}
