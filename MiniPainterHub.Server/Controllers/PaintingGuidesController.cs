using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Infrastructure.Caching;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/guides")]
[OutputCache(PolicyName = OutputCachePolicies.PublicDatabaseShort)]
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
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<PaintingGuideDto>> Create([FromBody] CreatePaintingGuideDto dto)
    {
        var userId = User.GetUserIdOrThrow();
        var created = await _paintingGuideService.CreateAsync(userId, dto, cancellationToken: HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("with-step-photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(GuidePhotoUploadRules.MaxMultipartBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = GuidePhotoUploadRules.MaxMultipartBodyBytes)]
    [EnableRateLimiting(RateLimitingPolicies.Upload)]
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
        catch (UnsupportedImageDimensionsException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Image dimensions too large",
                detail: ex.Message);
        }
        catch (UploadConcurrencyLimitExceededException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Upload capacity reached",
                detail: ex.Message);
        }
    }

    private static IReadOnlyDictionary<int, IFormFile> BuildStepPhotoMap(CreatePaintingGuideWithPhotosDto dto)
    {
        var files = dto.StepPhotos ?? new List<IFormFile>();
        var indices = dto.StepPhotoIndices ?? new List<int>();
        var errors = new Dictionary<string, string[]>();

        if (files.Count > GuidePhotoUploadRules.MaxStepPhotosPerGuide)
        {
            errors["StepPhotos"] = new[] { $"A guide can include at most {GuidePhotoUploadRules.MaxStepPhotosPerGuide} step photos." };
        }

        if (files.Count != indices.Count)
        {
            errors["StepPhotoIndices"] = new[] { "Each uploaded step photo must include a matching step index." };
        }

        if (indices.Count != indices.Distinct().Count())
        {
            errors["StepPhotoIndices"] = new[] { "Each guide step can receive at most one uploaded photo." };
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException("Guide step photos are invalid.", errors);
        }

        var mapped = new Dictionary<int, IFormFile>();

        for (var i = 0; i < files.Count && i < indices.Count; i++)
        {
            mapped[indices[i]] = files[i];
        }

        return mapped;
    }
}
