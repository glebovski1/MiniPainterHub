using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs;

public class CreatePaintingGuideWithPhotosDto
{
    [Required]
    [StringLength(140)]
    public string Title { get; set; } = default!;

    [Required]
    [StringLength(1000)]
    public string Summary { get; set; } = default!;

    [StringLength(2000)]
    public string? Materials { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(12)]
    public List<CreatePaintingGuideStepDto> Steps { get; set; } = new();

    [FromForm(Name = "stepPhotos")]
    public List<IFormFile>? StepPhotos { get; set; }

    [FromForm(Name = "stepPhotoIndices")]
    public List<int> StepPhotoIndices { get; set; } = new();

    public CreatePaintingGuideDto ToCreateDto() =>
        new()
        {
            Title = Title,
            Summary = Summary,
            Materials = Materials,
            Steps = Steps
        };
}
