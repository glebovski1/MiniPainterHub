using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class LikeService : ILikeService
    {
        private readonly ApiClient _api;

        public LikeService(ApiClient api)
        {
            _api = api;
        }

        public async Task<LikeDto> GetLikesAsync(int postId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/posts/{postId}/likes");
            var dto = await _api.SendAsync<LikeDto>(request);
            return dto ?? new LikeDto();
        }

        public async Task<LikeDto> ToggleLikeAsync(int postId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/posts/{postId}/likes");
            _ = await _api.SendAsync(request);
            return await GetLikesAsync(postId);
        }
    }
}
