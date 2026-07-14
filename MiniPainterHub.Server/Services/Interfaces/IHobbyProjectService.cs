using MiniPainterHub.Common.DTOs;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IHobbyProjectService
{
    Task<PagedResult<HobbyProjectSummaryDto>> GetAllAsync(HobbyProjectQueryDto query);
    Task<PagedResult<HobbyProjectSummaryDto>> GetMineAsync(string userId, HobbyProjectQueryDto query);
    Task<HobbyProjectDto> GetByIdAsync(int projectId, string? currentUserId = null);
    Task<PagedResult<HobbyProjectEntryDto>> GetDiaryAsync(int projectId, string? currentUserId, int page, int pageSize);
    Task<PagedResult<HobbyProjectEntryDto>> GetShowcaseAsync(int projectId, string? currentUserId, int page, int pageSize);
    Task<PagedResult<PostSummaryDto>> GetAvailablePostsAsync(string userId, int projectId, string? search, int page, int pageSize);
    Task<HobbyProjectDto> CreateAsync(string userId, CreateHobbyProjectDto request);
    Task<HobbyProjectDto> UpdateAsync(string userId, int projectId, UpdateHobbyProjectDto request);
    Task<HobbyProjectDto> UpdateStatusAsync(string userId, int projectId, UpdateHobbyProjectStatusDto request);
    Task<HobbyProjectDto> ArchiveAsync(string userId, int projectId);
    Task<HobbyProjectDto> RestoreAsync(string userId, int projectId);
    Task<HobbyProjectDto> LinkPostAsync(string userId, int projectId, LinkHobbyProjectPostDto request);
    Task<HobbyProjectDto> UpdateEntryAsync(string userId, int projectId, int postId, UpdateHobbyProjectEntryDto request);
    Task<HobbyProjectDto> UnlinkPostAsync(string userId, int projectId, int postId);
    Task<HobbyProjectDto> UpdateShowcaseAsync(string userId, int projectId, UpdateHobbyProjectShowcaseDto request);
    Task<HobbyProjectDto> UpdateCoverAsync(string userId, int projectId, UpdateHobbyProjectCoverDto request);
}
