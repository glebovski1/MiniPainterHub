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

        public async Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts?page={page}&pageSize={pageSize}");
            var result = await _api.SendAsync<PagedResult<PostSummaryDto>>(request);
            return result ?? new PagedResult<PostSummaryDto>();
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts?page={1}&pagesize={1000}");
            var result = await _api.SendAsync<PagedResult<PostSummaryDto>>(request);

            var posts = result?.Items
                .Where(p => p.CreatedAt >= DateTime.UtcNow.Subtract(timeOffcet))
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToList();

            return posts ?? Enumerable.Empty<PostSummaryDto>();
        }
    }
}
