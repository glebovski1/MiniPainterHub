using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http.Json;

namespace MiniPainterHub.WebApp.Services
{
    public class PostService : IPostService
    {
        private readonly HttpClient _http;
        public PostService(HttpClient http) => _http = http;

        public async Task<PagedResult<PostDto>> GetAllAsync(int page, int pageSize)
        {
            var result = await _http
              .GetFromJsonAsync<PagedResult<PostDto>>($"api/posts?page={page}&pageSize={pageSize}");
            return result ?? new PagedResult<PostDto>();
        }

        public async Task<PostDto> GetByIdAsync(int id)
        {
            var dto = await _http.GetFromJsonAsync<PostDto>($"api/posts/{id}");
            if (dto is null)
                throw new InvalidOperationException($"No post returned from API for ID {id}.");
            return dto;
        }

        public async Task<PostDto> CreateAsync(CreatePostDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/posts", dto);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<PostDto>();
            if (result is null)
                throw new InvalidOperationException("API returned no data when creating post.");
            return result;
        }

        public async Task<PostDto> CreateWithImageAsync(MultipartFormDataContent content)
        {
            var resp = await _http.PostAsync("api/posts/with-image", content);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<PostDto>();
            if (result is null)
                throw new InvalidOperationException("API returned no data when creating post with image.");
            return result;
        }
    }
}
