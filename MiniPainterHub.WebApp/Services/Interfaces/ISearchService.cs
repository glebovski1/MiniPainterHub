using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using System.Threading.Tasks;
using System.Threading;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface ISearchService
    {
        Task<ApiResult<SearchOverviewDto?>> GetOverviewAsync(string? query, CancellationToken cancellationToken = default);
        Task<ApiResult<PagedResult<PostSummaryDto>?>> SearchPostsAsync(string? query, string? tag, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> SearchProjectsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<ApiResult<PagedResult<UserListItemDto>?>> SearchUsersAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<ApiResult<PagedResult<SearchTagResultDto>?>> SearchTagsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
