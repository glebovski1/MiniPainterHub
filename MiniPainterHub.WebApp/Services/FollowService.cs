using System.Net.Http;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class FollowService : IFollowService
    {
        private readonly ApiClient _api;

        public FollowService(ApiClient api)
        {
            _api = api;
        }

        public async Task FollowAsync(string userId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/follows/{Uri.EscapeDataString(userId)}");
            await _api.SendAsync(request);
        }

        public async Task UnfollowAsync(string userId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/follows/{Uri.EscapeDataString(userId)}");
            await _api.SendAsync(request);
        }

        public async Task<IReadOnlyList<UserListItemDto>> GetFollowersAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/follows/me/followers");
            var result = await _api.SendAsync<List<UserListItemDto>>(request);
            return result ?? new List<UserListItemDto>();
        }

        public async Task<IReadOnlyList<UserListItemDto>> GetFollowingAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/follows/me/following");
            var result = await _api.SendAsync<List<UserListItemDto>>(request);
            return result ?? new List<UserListItemDto>();
        }
    }
}
