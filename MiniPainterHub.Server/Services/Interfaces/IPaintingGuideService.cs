using Microsoft.AspNetCore.Http;
using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IPaintingGuideService
{
    Task<PagedResult<PaintingGuideSummaryDto>> GetAllAsync(int page, int pageSize);

    Task<PaintingGuideDto> GetByIdAsync(int guideId);

    Task<PaintingGuideDto> CreateAsync(
        string userId,
        CreatePaintingGuideDto dto,
        IReadOnlyDictionary<int, IFormFile>? stepPhotos = null,
        CancellationToken cancellationToken = default);
}
