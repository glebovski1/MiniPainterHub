using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class PostService : IPostService
    {
        private readonly ApiClient _api;

        public PostService(ApiClient api) => _api = api;

        public async Task<ApiResult<PagedResult<PostSummaryDto>>> GetAllAsync(int page, int pageSize, bool includeDeleted = false, bool deletedOnly = false)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/posts?page={page}&pageSize={pageSize}&includeDeleted={includeDeleted}&deletedOnly={deletedOnly}");
            var result = await _api.SendForResultAsync<PagedResult<PostSummaryDto>>(request);

            return result with
            {
                Value = result.Value ?? new PagedResult<PostSummaryDto>()
            };
        }

        public async Task<ApiResult<PagedResult<PostSummaryDto>>> GetByAuthorAsync(string userId, int page, int pageSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts/by-user/{Uri.EscapeDataString(userId)}?page={page}&pageSize={pageSize}");
            var result = await _api.SendForResultAsync<PagedResult<PostSummaryDto>>(request);
            return result with
            {
                Value = result.Value ?? new PagedResult<PostSummaryDto>()
            };
        }

        public async Task<ApiResult<PagedResult<PostSummaryDto>>> GetFollowingFeedAsync(int page, int pageSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/feed/following?page={page}&pageSize={pageSize}");
            var result = await _api.SendForResultAsync<PagedResult<PostSummaryDto>>(request);
            return result with
            {
                Value = result.Value ?? new PagedResult<PostSummaryDto>()
            };
        }

        public async Task<PostDto> GetByIdAsync(int id)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{id}");
            var dto = await _api.SendAsync<PostDto>(request);
            if (dto is null)
            {
                throw new InvalidOperationException($"No post returned from API for ID {id}.");
            }

            return dto;
        }

        public async Task<PostDto> CreateAsync(CreatePostDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/posts")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<PostDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when creating post.");
            }

            return result;
        }

        public async Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/posts/with-image")
            {
                Content = content
            };

            var result = await _api.SendAsync<PostDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when creating post with image.");
            }

            return result;
        }

        public async Task<IEnumerable<PostSummaryDto>> GetTopPosts(int count, TimeSpan timeOffcet)
        {
            var safeCount = Math.Clamp(count, 1, 20);
            var lookbackDays = Math.Clamp((int)Math.Ceiling(timeOffcet.TotalDays), 1, 365);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts/top?count={safeCount}&lookbackDays={lookbackDays}");
            var posts = await _api.SendAsync<List<PostSummaryDto>>(request);

            return posts ?? Enumerable.Empty<PostSummaryDto>();
        }
    }
}
