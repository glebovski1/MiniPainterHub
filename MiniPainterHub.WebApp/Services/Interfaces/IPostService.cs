using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IPostService
    {
        /// <summary>Fetches a paged list of posts.</summary>
        Task<ApiResult<PagedResult<PostSummaryDto>>> GetAllAsync(int page, int pageSize);

        public Task<IEnumerable<PostSummaryDto>> GetTopPosts(int count, TimeSpan timeOffcet);

        /// <summary>Fetches a single post by ID.</summary>
        Task<PostDto> GetByIdAsync(int id);

        /// <summary>Creates a new post without an image.</summary>
        Task<PostDto> CreateAsync(CreatePostDto dto);

        /// <summary>Creates a new post with an image (multipart/form-data).</summary>
        Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content);
    }
}
