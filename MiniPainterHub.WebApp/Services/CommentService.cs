using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http.Json;

namespace MiniPainterHub.WebApp.Services
{
    public class CommentService : ICommentService
    {
        private readonly HttpClient _http;

        public CommentService(HttpClient http)
        {
            _http = http;
        }

        public async Task<PagedResult<CommentDto>> GetByPostAsync(int postId, int page, int pageSize)
        {
            // GET api/posts/{postId}/comments?page=1&pageSize=10
            var url = $"api/posts/{postId}/comments?page={page}&pageSize={pageSize}";
            var result = await _http.GetFromJsonAsync<PagedResult<CommentDto>>(url);
            return result!;
        }

        public async Task<CommentDto> CreateAsync(int postId, CreateCommentDto dto)
        {
            // POST api/posts/{postId}/comments
            var response = await _http.PostAsJsonAsync($"api/posts/{postId}/comments", dto);
            response.EnsureSuccessStatusCode();
            // The API returns 201 Created with the CommentDto in the body
            return await response.Content.ReadFromJsonAsync<CommentDto>()!;
        }

        // Optional methods:
        // public async Task<bool> UpdateAsync(int commentId, UpdateCommentDto dto) { … }
        // public async Task DeleteAsync(int commentId) { … }
    }
}

