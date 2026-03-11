using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IReportService
    {
        Task SubmitPostReportAsync(string reporterUserId, int postId, CreateReportRequestDto request);
        Task SubmitCommentReportAsync(string reporterUserId, int commentId, CreateReportRequestDto request);
        Task SubmitUserReportAsync(string reporterUserId, string targetUserId, CreateReportRequestDto request);
        Task<PagedResult<ReportQueueItemDto>> GetQueueAsync(ReportQueueQueryDto query);
        Task ResolveAsync(string reviewerUserId, long reportId, ResolveReportRequestDto request);
    }
}
