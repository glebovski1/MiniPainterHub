using System;
using System.Net.Http;
using System.Net.Http.Json;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class AuthorMarkService : IAuthorMarkService
    {
        private readonly ApiClient _api;

        public AuthorMarkService(ApiClient api)
        {
            _api = api;
        }

        public async Task<AuthorMarkDto> CreateAsync(int postId, int imageId, CreateAuthorMarkDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{postId}/images/{imageId}/author-marks")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<AuthorMarkDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when creating an author mark.");
            }

            return result;
        }

        public async Task<AuthorMarkDto> UpdateAsync(int markId, UpdateAuthorMarkDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/author-marks/{markId}")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<AuthorMarkDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when updating an author mark.");
            }

            return result;
        }

        public async Task DeleteAsync(int markId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/author-marks/{markId}");
            var success = await _api.SendAsync(request);
            if (!success)
            {
                throw new InvalidOperationException("API failed to delete the author mark.");
            }
        }
    }
}
