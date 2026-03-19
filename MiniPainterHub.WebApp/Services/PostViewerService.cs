using System;
using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class PostViewerService : IPostViewerService
    {
        private readonly ApiClient _api;

        public PostViewerService(ApiClient api)
        {
            _api = api;
        }

        public async Task<PostViewerDto> GetAsync(int postId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{postId}/viewer");
            var dto = await _api.SendAsync<PostViewerDto>(request);
            if (dto is null)
            {
                throw new InvalidOperationException($"No viewer payload returned from API for post {postId}.");
            }

            return dto;
        }
    }
}
