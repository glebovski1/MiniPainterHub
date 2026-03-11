using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IReportService
    {
        Task<bool> ReportPostAsync(int postId, CreateReportRequestDto request);
        Task<bool> ReportCommentAsync(int commentId, CreateReportRequestDto request);
        Task<bool> ReportUserAsync(string userId, CreateReportRequestDto request);
        Task<ApiResult<PagedResult<ReportQueueItemDto>?>> GetQueueAsync(ReportQueueQueryDto query);
        Task<bool> ResolveAsync(long reportId, ResolveReportRequestDto request);
    }
}
