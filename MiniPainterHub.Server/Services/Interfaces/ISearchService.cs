using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ISearchService
    {
        Task<SearchOverviewDto> GetOverviewAsync(string? query);
        Task<PagedResult<PostSummaryDto>> SearchPostsAsync(string? query, string? tagSlug, int page, int pageSize);
        Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? query, int page, int pageSize);
        Task<PagedResult<SearchTagResultDto>> SearchTagsAsync(string? query, int page, int pageSize);
    }
}
