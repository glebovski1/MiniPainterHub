using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services
{
    public class ModerationService : IModerationService
    {
        private readonly ApiClient _api;

        public ModerationService(ApiClient api)
        {
            _api = api;
        }

        public Task<bool> HidePostAsync(int postId, ModerationActionRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/posts/{postId}/hide") { Content = JsonContent.Create(request) });

        public Task<bool> RestorePostAsync(int postId, ModerationActionRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/posts/{postId}/restore") { Content = JsonContent.Create(request) });

        public Task<bool> HideCommentAsync(int commentId, ModerationActionRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/comments/{commentId}/hide") { Content = JsonContent.Create(request) });

        public Task<bool> RestoreCommentAsync(int commentId, ModerationActionRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/comments/{commentId}/restore") { Content = JsonContent.Create(request) });

        public Task<bool> SuspendUserAsync(string userId, SuspendUserRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/users/{Uri.EscapeDataString(userId)}/suspend") { Content = JsonContent.Create(request) });

        public Task<bool> UnsuspendUserAsync(string userId, ModerationActionRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/moderation/users/{Uri.EscapeDataString(userId)}/unsuspend") { Content = JsonContent.Create(request) });

        public Task<ApiResult<PagedResult<ModerationAuditDto>?>> GetAuditAsync(ModerationAuditQueryDto query) =>
            _api.SendForResultAsync<PagedResult<ModerationAuditDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/moderation/audit?page={query.Page}&pageSize={query.PageSize}"));
    }
}
