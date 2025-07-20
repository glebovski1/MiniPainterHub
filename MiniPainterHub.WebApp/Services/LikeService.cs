using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http.Json;

namespace MiniPainterHub.WebApp.Services
{
    public class LikeService : ILikeService
    {
        private readonly HttpClient _http;

        public LikeService(HttpClient http)
        {
            _http = http;
        }

        public async Task<LikeDto> GetLikesAsync(int postId)
        {
            // GET api/posts/{postId}/likes
            var dto = await _http.GetFromJsonAsync<LikeDto>($"api/posts/{postId}/likes");
            return dto!;
        }

        public async Task<LikeDto> ToggleLikeAsync(int postId)
        {
            // POST api/posts/{postId}/likes
            var response = await _http.PostAsync($"api/posts/{postId}/likes", null);
            response.EnsureSuccessStatusCode();

            // return fresh state
            return await GetLikesAsync(postId);
        }
    }
}
