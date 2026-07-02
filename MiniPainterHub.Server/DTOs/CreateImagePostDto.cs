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

        [StringLength(120)]
        public string? MiniatureName { get; set; }

        [StringLength(2000)]
        public string? PaintsUsed { get; set; }

        [StringLength(1000)]
        public string? Techniques { get; set; }

        [StringLength(40)]
        public string? Difficulty { get; set; }

        [StringLength(80)]
        public string? TimeSpent { get; set; }

        [FromForm(Name = "tags")]
        public List<string> Tags { get; set; } = new();

        // Bound from multipart/form-data under the "images" field
        [FromForm(Name = "images")]
        public List<IFormFile>? Images { get; set; }

        // Optional thumbnails matching each image
        [FromForm(Name = "thumbnails")]
        public List<IFormFile>? Thumbnails { get; set; }
    }
}
