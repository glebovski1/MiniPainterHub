using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Media;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Imaging;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public class PaintingGuideService : IPaintingGuideService
{
    private const int MaxStepsPerGuide = 12;
    private readonly AppDbContext _appDbContext;
    private readonly IImageService _imageService;
    private readonly IAccountRestrictionService? _accountRestrictionService;

    public PaintingGuideService(
        AppDbContext appDbContext,
        IImageService imageService,
        IAccountRestrictionService? accountRestrictionService = null)
    {
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _accountRestrictionService = accountRestrictionService;
    }

    public async Task<PagedResult<PaintingGuideSummaryDto>> GetAllAsync(int page, int pageSize)
    {
        PaginationGuard.Validate(page, pageSize);

        var query = _appDbContext.PaintingGuides
            .AsNoTracking()
            .Where(guide => !guide.IsDeleted)
            .OrderByDescending(guide => guide.CreatedUtc);

        var totalCount = await query.CountAsync();
        var guides = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(guide => guide.CreatedBy)
            .ThenInclude(user => user.Profile)
            .Include(guide => guide.Steps)
            .AsSplitQuery()
            .ToListAsync();

        return new PagedResult<PaintingGuideSummaryDto>
        {
            Items = guides.Select(ToSummaryDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<PaintingGuideDto> GetByIdAsync(int guideId)
    {
        var guide = await BuildGuideGraphQuery()
            .FirstOrDefaultAsync(g => g.Id == guideId && !g.IsDeleted);

        if (guide is null)
        {
            throw new NotFoundException("Guide not found.");
        }

        return ToDto(guide);
    }

    public async Task<PaintingGuideDto> CreateAsync(
        string userId,
        CreatePaintingGuideDto dto,
        IReadOnlyDictionary<int, IFormFile>? stepPhotos = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Title, nameof(dto.Title));
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Summary, nameof(dto.Summary));
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User must be authenticated to create guides.");
        }

        if (_accountRestrictionService != null)
        {
            await _accountRestrictionService.EnsureCanCreatePostAsync(userId);
        }

        var normalizedSteps = NormalizeSteps(dto.Steps);
        ValidateStepPhotos(stepPhotos, normalizedSteps.Count);

        var user = await _appDbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new UnauthorizedAccessException("User must be authenticated to create guides.");
        }

        var now = DateTime.UtcNow;
        var guide = new PaintingGuide
        {
            Title = dto.Title.Trim(),
            Summary = dto.Summary.Trim(),
            Materials = NormalizeOptional(dto.Materials),
            CreatedById = userId,
            CreatedBy = user,
            CreatedUtc = now,
            UpdatedUtc = now,
            Steps = normalizedSteps
                .Select((step, index) => new PaintingGuideStep
                {
                    SortOrder = index + 1,
                    Title = step.Title,
                    Description = step.Description,
                    PaintsUsed = step.PaintsUsed,
                    Techniques = step.Techniques
                })
                .ToList()
        };

        _appDbContext.PaintingGuides.Add(guide);
        await _appDbContext.SaveChangesAsync(cancellationToken);

        var uploadedKeys = new List<string>();
        try
        {
            if (stepPhotos is not null)
            {
                foreach (var (stepIndex, photo) in stepPhotos.OrderBy(pair => pair.Key))
                {
                    var step = guide.Steps[stepIndex];
                    var contentType = PostImageUploadValidator.ResolveContentType(photo);
                    var storageKey = CreateStepPhotoKey(guide.Id, stepIndex + 1, contentType);
                    await using var stream = photo.OpenReadStream();
                    step.ImageUrl = await _imageService.UploadAsync(stream, storageKey);
                    step.ImageStorageKey = storageKey;
                    uploadedKeys.Add(storageKey);
                }

                guide.UpdatedUtc = DateTime.UtcNow;
                await _appDbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            await CleanupFailedGuideCreateAsync(guide, uploadedKeys);
            throw;
        }

        return ToDto(guide);
    }

    private async Task CleanupFailedGuideCreateAsync(PaintingGuide guide, IReadOnlyList<string> uploadedKeys)
    {
        foreach (var key in uploadedKeys)
        {
            try
            {
                await _imageService.DeleteAsync(key);
            }
            catch
            {
                // Best-effort rollback; the original upload failure remains the important signal.
            }
        }

        _appDbContext.PaintingGuides.Remove(guide);
        await _appDbContext.SaveChangesAsync();
    }

    private IQueryable<PaintingGuide> BuildGuideGraphQuery() =>
        _appDbContext.PaintingGuides
            .AsNoTracking()
            .AsSplitQuery()
            .Include(guide => guide.CreatedBy)
            .ThenInclude(user => user.Profile)
            .Include(guide => guide.Steps);

    private static IReadOnlyList<NormalizedGuideStep> NormalizeSteps(IEnumerable<CreatePaintingGuideStepDto>? steps)
    {
        var normalized = (steps ?? Enumerable.Empty<CreatePaintingGuideStepDto>())
            .Take(MaxStepsPerGuide + 1)
            .Select(step => new NormalizedGuideStep(
                NormalizeRequired(step.Title),
                NormalizeRequired(step.Description),
                NormalizeOptional(step.PaintsUsed),
                NormalizeOptional(step.Techniques)))
            .ToList();

        var errors = new Dictionary<string, string[]>();
        if (normalized.Count == 0)
        {
            errors["Steps"] = new[] { "A guide needs at least one step." };
        }
        else if (normalized.Count > MaxStepsPerGuide)
        {
            errors["Steps"] = new[] { $"A guide can include at most {MaxStepsPerGuide} steps." };
        }

        for (var i = 0; i < normalized.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(normalized[i].Title))
            {
                errors[$"Steps[{i}].Title"] = new[] { "Step title is required." };
            }

            if (string.IsNullOrWhiteSpace(normalized[i].Description))
            {
                errors[$"Steps[{i}].Description"] = new[] { "Step description is required." };
            }
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException("Guide steps are invalid.", errors);
        }

        return normalized;
    }

    private static void ValidateStepPhotos(IReadOnlyDictionary<int, IFormFile>? stepPhotos, int stepCount)
    {
        if (stepPhotos is null || stepPhotos.Count == 0)
        {
            return;
        }

        var errors = new Dictionary<string, string[]>();
        foreach (var (stepIndex, photo) in stepPhotos)
        {
            if (stepIndex < 0 || stepIndex >= stepCount)
            {
                errors["StepPhotos"] = new[] { "Each uploaded step photo must map to an existing guide step." };
                continue;
            }

            ValidateSafeFileName(photo, errors);
            if (photo.Length > PostImageUploadValidator.MaxUploadBytes)
            {
                throw new ImageTooLargeException(photo.FileName, photo.Length, PostImageUploadValidator.MaxUploadBytes);
            }

            var contentType = PostImageUploadValidator.ResolveContentType(photo);
            if (!ImageContentTypes.IsAllowed(contentType))
            {
                throw new UnsupportedImageContentTypeException(photo.FileName, contentType);
            }
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException("Guide step photos are invalid.", errors);
        }
    }

    private static void ValidateSafeFileName(IFormFile file, Dictionary<string, string[]> errors)
    {
        var fileName = file.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var normalized = fileName.Replace('\\', '/');
        if (Path.IsPathRooted(fileName)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal))
        {
            errors["StepPhotos"] = new[] { "Step photo filenames must not include paths or parent directory segments." };
        }
    }

    private static string CreateStepPhotoKey(int guideId, int stepNumber, string contentType) =>
        $"guide-{guideId}-step-{stepNumber}-photo-{Guid.NewGuid():N}.{ResolveExtension(contentType)}";

    private static string ResolveExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "image/gif" => "gif",
            "image/bmp" or "image/x-ms-bmp" => "bmp",
            "image/tiff" or "image/x-tiff" => "tiff",
            _ => throw new UnsupportedImageContentTypeException("step photo", contentType)
        };

    private static PaintingGuideDto ToDto(PaintingGuide guide) =>
        new()
        {
            Id = guide.Id,
            Title = guide.Title,
            Summary = guide.Summary,
            Materials = guide.Materials,
            CreatedById = guide.CreatedById,
            AuthorName = PostDtoMapper.ResolveDisplayName(guide.CreatedBy?.UserName, guide.CreatedBy?.Profile?.DisplayName),
            CreatedAt = guide.CreatedUtc,
            Steps = guide.Steps
                .OrderBy(step => step.SortOrder)
                .Select(step => new PaintingGuideStepDto
                {
                    Id = step.Id,
                    SortOrder = step.SortOrder,
                    Title = step.Title,
                    Description = step.Description,
                    PaintsUsed = step.PaintsUsed,
                    Techniques = step.Techniques,
                    ImageUrl = step.ImageUrl
                })
                .ToList()
        };

    private static PaintingGuideSummaryDto ToSummaryDto(PaintingGuide guide)
    {
        var orderedSteps = guide.Steps.OrderBy(step => step.SortOrder).ToList();
        return new PaintingGuideSummaryDto
        {
            Id = guide.Id,
            Title = guide.Title,
            Snippet = guide.Summary.Length > 160 ? guide.Summary.Substring(0, 157) + "..." : guide.Summary,
            AuthorName = PostDtoMapper.ResolveDisplayName(guide.CreatedBy?.UserName, guide.CreatedBy?.Profile?.DisplayName),
            AuthorId = guide.CreatedById,
            CreatedAt = guide.CreatedUtc,
            StepCount = orderedSteps.Count,
            CoverImageUrl = orderedSteps.Select(step => step.ImageUrl).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
        };
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedGuideStep(
        string Title,
        string Description,
        string? PaintsUsed,
        string? Techniques);
}
