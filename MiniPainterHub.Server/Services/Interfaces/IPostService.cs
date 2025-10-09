using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IPostService
    {
        Task<bool> ExistsAsync(int postId);
        Task<PostDto> CreateAsync(string userId, CreatePostDto dto);
        Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize);
        Task<PostDto> GetByIdAsync(int postId);
        Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto);
        Task<bool> DeleteAsync(int postId, string userId);
        Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PostImageDto> images);
        Task<PostDto> CreateWithImagesAsync(string userId, CreateImagePostDto dto, CancellationToken ct);
    }
}
