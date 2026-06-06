using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class AdminService : IAdminService
    {
        private readonly ApiClient _api;

        public AdminService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<PagedResult<AdminInboxItemDto>?>> GetInboxAsync(AdminInboxQueryDto query) =>
            _api.SendForResultAsync<PagedResult<AdminInboxItemDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/admin/inbox{BuildQuery(new Dictionary<string, string?>
            {
                ["page"] = query.Page.ToString(),
                ["pageSize"] = query.PageSize.ToString(),
                ["windowHours"] = query.WindowHours.ToString(),
                ["itemType"] = query.ItemType,
                ["state"] = query.State,
                ["search"] = query.Search
            })}"));

        public Task<ApiResult<AdminInboxDetailDto?>> GetInboxDetailAsync(string targetType, string targetId) =>
            _api.SendForResultAsync<AdminInboxDetailDto?>(new HttpRequestMessage(HttpMethod.Get, $"api/admin/inbox/{Uri.EscapeDataString(targetType)}/{Uri.EscapeDataString(targetId)}"));

        public Task<bool> ReviewInboxItemAsync(string targetType, string targetId, AdminInboxReviewRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/admin/inbox/{Uri.EscapeDataString(targetType)}/{Uri.EscapeDataString(targetId)}/review")
            {
                Content = ApiClient.CreateJsonContent(request)
            });

        public Task<ApiResult<IReadOnlyList<AdminSiteControlDto>?>> GetControlsAsync() =>
            _api.SendForResultAsync<IReadOnlyList<AdminSiteControlDto>?>(new HttpRequestMessage(HttpMethod.Get, "api/admin/controls"));

        public Task<ApiResult<AdminSiteControlDto?>> UpdateControlAsync(string key, UpdateAdminSiteControlRequestDto request) =>
            _api.SendForResultAsync<AdminSiteControlDto?>(new HttpRequestMessage(HttpMethod.Put, $"api/admin/controls/{Uri.EscapeDataString(key)}")
            {
                Content = ApiClient.CreateJsonContent(request)
            });

        public Task<ApiResult<AdminDashboardStatsDto?>> GetDashboardAsync(int windowHours) =>
            _api.SendForResultAsync<AdminDashboardStatsDto?>(new HttpRequestMessage(HttpMethod.Get, $"api/admin/dashboard?windowHours={windowHours}"));

        private static string BuildQuery(IReadOnlyDictionary<string, string?> values)
        {
            var query = new List<string>();
            foreach (var pair in values)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    query.Add($"{pair.Key}={Uri.EscapeDataString(pair.Value)}");
                }
            }

            return query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        }
    }
}
