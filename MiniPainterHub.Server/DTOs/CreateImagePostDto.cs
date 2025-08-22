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

        // Bound from multipart/form-data under the "images" field
        [FromForm(Name = "images")]
        public List<IFormFile>? Images { get; set; }

        // Optional thumbnails matching each image
        [FromForm(Name = "thumbnails")]
        public List<IFormFile>? Thumbnails { get; set; }
    }
}
