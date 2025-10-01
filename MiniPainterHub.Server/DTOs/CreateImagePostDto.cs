using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class CreateImagePostDto
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = default!;

        [Required]
        [StringLength(4000)]
        public string Content { get; set; } = default!;

        // Bound from multipart/form-data under the "images" field
        [FromForm(Name = "images")]
        public List<IFormFile>? Images { get; set; }

        // Optional thumbnails matching each image
        [FromForm(Name = "thumbnails")]
        public List<IFormFile>? Thumbnails { get; set; }
    }
}
