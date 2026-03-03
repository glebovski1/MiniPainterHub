using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services
{
    public class AdminService
    {
        private readonly ApiClient _api;
        public AdminService(ApiClient api) { _api = api; }

        public async Task<List<AppSettingDto>> GetFlagsAsync() => await _api.SendAsync<List<AppSettingDto>>(new HttpRequestMessage(HttpMethod.Get, "/api/admin/flags")) ?? new();
        public Task<bool> SetFlagAsync(SetAppSettingDto dto) => _api.SendAsync(new HttpRequestMessage(HttpMethod.Put, "/api/admin/flags") { Content = JsonContent.Create(dto) });
        public async Task<List<NewsItemDto>> GetNewsAsync() => await _api.SendAsync<List<NewsItemDto>>(new HttpRequestMessage(HttpMethod.Get, "/api/admin/news")) ?? new();
        public async Task<List<FeedPolicyDto>> GetFeedPoliciesAsync() => await _api.SendAsync<List<FeedPolicyDto>>(new HttpRequestMessage(HttpMethod.Get, "/api/admin/feed-policies")) ?? new();
        public async Task<PagedResult<ModerationActionDto>> GetAuditAsync() => await _api.SendAsync<PagedResult<ModerationActionDto>>(new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit")) ?? new();
    }
}
