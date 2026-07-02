using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/guides")]
public sealed class PaintingGuidesController : ControllerBase
{
    private readonly IPaintingGuideService _paintingGuideService;

    public PaintingGuidesController(IPaintingGuideService paintingGuideService)
    {
        _paintingGuideService = paintingGuideService ?? throw new ArgumentNullException(nameof(paintingGuideService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<PaintingGuideSummaryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _paintingGuideService.GetAllAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<PaintingGuideDto>> GetById(int id)
    {
        var result = await _paintingGuideService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PaintingGuideDto>> Create([FromBody] CreatePaintingGuideDto dto)
    {
        var userId = User.GetUserIdOrThrow();
        var created = await _paintingGuideService.CreateAsync(userId, dto, cancellationToken: HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("with-step-photos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PaintingGuideDto>> CreateWithStepPhotos([FromForm] CreatePaintingGuideWithPhotosDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.GetUserIdOrThrow();
        var stepPhotos = BuildStepPhotoMap(dto);

        try
        {
            var created = await _paintingGuideService.CreateAsync(
                userId,
                dto.ToCreateDto(),
                stepPhotos,
                HttpContext.RequestAborted);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DomainValidationException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(ex.Errors)
            {
                Title = ex.Message
            });
        }
        catch (ImageTooLargeException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Image too large",
                detail: ex.Message);
        }
        catch (UnsupportedImageContentTypeException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported media type",
                detail: ex.Message);
        }
    }

    private static IReadOnlyDictionary<int, IFormFile> BuildStepPhotoMap(CreatePaintingGuideWithPhotosDto dto)
    {
        var files = dto.StepPhotos ?? new List<IFormFile>();
        var indices = dto.StepPhotoIndices ?? new List<int>();
        var mapped = new Dictionary<int, IFormFile>();

        for (var i = 0; i < files.Count && i < indices.Count; i++)
        {
            mapped[indices[i]] = files[i];
        }

        return mapped;
    }
}
