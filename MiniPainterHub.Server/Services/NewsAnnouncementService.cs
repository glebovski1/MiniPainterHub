using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public class NewsAnnouncementService : INewsAnnouncementService
{
    private readonly AppDbContext _appDbContext;

    public NewsAnnouncementService(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
    }

    public async Task<PagedResult<NewsAnnouncementSummaryDto>> GetAllAsync(int page, int pageSize)
    {
        PaginationGuard.Validate(page, pageSize);

        var query = _appDbContext.NewsAnnouncements
            .AsNoTracking()
            .Where(announcement => !announcement.IsDeleted)
            .OrderByDescending(announcement => announcement.PublishedUtc);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(announcement => announcement.CreatedBy)
            .ThenInclude(user => user.Profile)
            .Select(announcement => ToSummaryDto(announcement))
            .ToListAsync();

        return new PagedResult<NewsAnnouncementSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<NewsAnnouncementDto> GetByIdAsync(int announcementId)
    {
        var announcement = await _appDbContext.NewsAnnouncements
            .AsNoTracking()
            .Include(item => item.CreatedBy)
            .ThenInclude(user => user.Profile)
            .FirstOrDefaultAsync(item => item.Id == announcementId && !item.IsDeleted);

        if (announcement is null)
        {
            throw new NotFoundException("Announcement not found.");
        }

        return ToDto(announcement);
    }

    public async Task<NewsAnnouncementDto> CreateAsync(string userId, CreateNewsAnnouncementDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Title, nameof(dto.Title));
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Summary, nameof(dto.Summary));
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Body, nameof(dto.Body));

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User must be authenticated to create announcements.");
        }

        var user = await _appDbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new UnauthorizedAccessException("User must be authenticated to create announcements.");
        }

        var now = DateTime.UtcNow;
        var announcement = new NewsAnnouncement
        {
            Title = dto.Title.Trim(),
            Summary = dto.Summary.Trim(),
            Body = dto.Body.Trim(),
            CreatedById = userId,
            CreatedBy = user,
            PublishedUtc = now,
            UpdatedUtc = now
        };

        _appDbContext.NewsAnnouncements.Add(announcement);
        await _appDbContext.SaveChangesAsync();

        return ToDto(announcement);
    }

    private static NewsAnnouncementDto ToDto(NewsAnnouncement announcement) =>
        new()
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Summary = announcement.Summary,
            Body = announcement.Body,
            CreatedById = announcement.CreatedById,
            AuthorName = PostDtoMapper.ResolveDisplayName(announcement.CreatedBy?.UserName, announcement.CreatedBy?.Profile?.DisplayName),
            PublishedAt = announcement.PublishedUtc
        };

    private static NewsAnnouncementSummaryDto ToSummaryDto(NewsAnnouncement announcement) =>
        new()
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Summary = announcement.Summary,
            AuthorName = PostDtoMapper.ResolveDisplayName(announcement.CreatedBy?.UserName, announcement.CreatedBy?.Profile?.DisplayName),
            AuthorId = announcement.CreatedById,
            PublishedAt = announcement.PublishedUtc
        };
}
