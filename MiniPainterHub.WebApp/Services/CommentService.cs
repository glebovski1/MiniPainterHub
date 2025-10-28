using System.Net.Http;
using System.Net.Http.Json;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class CommentService : ICommentService
    {
        private readonly ApiClient _api;

        public CommentService(ApiClient api)
        {
            _api = api;
        }

        public async Task<PagedResult<CommentDto>> GetByPostAsync(int postId, int page, int pageSize)
        {
            var url = $"api/posts/{postId}/comments?page={page}&pageSize={pageSize}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var result = await _api.SendAsync<PagedResult<CommentDto>>(request);
            return result ?? new PagedResult<CommentDto>();
        }

        public async Task<CommentDto> CreateAsync(int postId, CreateCommentDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/posts/{postId}/comments")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<CommentDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when creating comment.");
            }

            return result;
        }
    }
}
