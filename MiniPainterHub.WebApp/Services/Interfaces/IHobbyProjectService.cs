using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces;

public interface IHobbyProjectService
{
    Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetAllAsync(HobbyProjectQueryDto query);
    Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetMineAsync(HobbyProjectQueryDto query);
    Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetByOwnerAsync(string ownerUserId, HobbyProjectQueryDto query);
    Task<ApiResult<HobbyProjectDto?>> GetAsync(int projectId);
    Task<ApiResult<PagedResult<HobbyProjectEntryDto>?>> GetDiaryAsync(int projectId, int page, int pageSize);
    Task<ApiResult<PagedResult<HobbyProjectEntryDto>?>> GetShowcaseAsync(int projectId, int page, int pageSize);
    Task<ApiResult<PagedResult<PostSummaryDto>?>> GetAvailablePostsAsync(int projectId, string? search, int page, int pageSize);
    Task<ApiResult<HobbyProjectDto?>> CreateAsync(CreateHobbyProjectDto request);
    Task<ApiResult<HobbyProjectDto?>> UpdateAsync(int projectId, UpdateHobbyProjectDto request);
    Task<ApiResult<HobbyProjectDto?>> UpdateStatusAsync(int projectId, UpdateHobbyProjectStatusDto request);
    Task<ApiResult<HobbyProjectDto?>> ArchiveAsync(int projectId);
    Task<ApiResult<HobbyProjectDto?>> UnarchiveAsync(int projectId);
    Task<ApiResult<HobbyProjectDto?>> LinkPostAsync(int projectId, LinkHobbyProjectPostDto request);
    Task<ApiResult<HobbyProjectDto?>> UpdateEntryAsync(int projectId, int postId, UpdateHobbyProjectEntryDto request);
    Task<ApiResult<HobbyProjectDto?>> UnlinkPostAsync(int projectId, int postId);
    Task<ApiResult<HobbyProjectDto?>> UpdateShowcaseAsync(int projectId, UpdateHobbyProjectShowcaseDto request);
    Task<ApiResult<HobbyProjectDto?>> UpdateCoverAsync(int projectId, UpdateHobbyProjectCoverDto request);
}
