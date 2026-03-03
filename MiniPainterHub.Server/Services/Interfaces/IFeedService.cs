using System.Threading.Tasks;
using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IFeedService
    {
        Task<PagedResult<FeedItemDto>> GetFeedAsync(int page, int pageSize);
    }
}
