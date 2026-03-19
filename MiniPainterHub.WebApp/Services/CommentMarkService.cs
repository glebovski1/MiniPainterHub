using System;
using System.Net.Http;
using System.Net.Http.Json;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class CommentMarkService : ICommentMarkService
    {
        private readonly ApiClient _api;

        public CommentMarkService(ApiClient api)
        {
            _api = api;
        }

        public async Task<CommentMarkDto> GetByCommentIdAsync(int commentId, bool includeDeleted = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/comments/{commentId}/mark?includeDeleted={includeDeleted}");
            var dto = await _api.SendAsync<CommentMarkDto>(request, new ApiRequestOptions { SuppressErrorNotifications = true });
            if (dto is null)
            {
                throw new InvalidOperationException("Comment mark could not be loaded.");
            }

            return dto;
        }

        public async Task<CommentMarkDto> UpsertAsync(int commentId, ViewerMarkDraftDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/comments/{commentId}/mark")
            {
                Content = JsonContent.Create(dto)
            };

            var result = await _api.SendAsync<CommentMarkDto>(request);
            if (result is null)
            {
                throw new InvalidOperationException("API returned no data when updating the comment mark.");
            }

            return result;
        }

        public async Task DeleteAsync(int commentId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/comments/{commentId}/mark");
            var success = await _api.SendAsync(request);
            if (!success)
            {
                throw new InvalidOperationException("API failed to delete the comment mark.");
            }
        }
    }
}
