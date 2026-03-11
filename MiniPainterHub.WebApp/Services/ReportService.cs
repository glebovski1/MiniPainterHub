using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class ReportService : IReportService
    {
        private readonly ApiClient _api;

        public ReportService(ApiClient api)
        {
            _api = api;
        }

        public Task<bool> ReportPostAsync(int postId, CreateReportRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/reports/posts/{postId}") { Content = JsonContent.Create(request) });

        public Task<bool> ReportCommentAsync(int commentId, CreateReportRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/reports/comments/{commentId}") { Content = JsonContent.Create(request) });

        public Task<bool> ReportUserAsync(string userId, CreateReportRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/reports/users/{Uri.EscapeDataString(userId)}") { Content = JsonContent.Create(request) });

        public Task<ApiResult<PagedResult<ReportQueueItemDto>?>> GetQueueAsync(ReportQueueQueryDto query) =>
            _api.SendForResultAsync<PagedResult<ReportQueueItemDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/reports{BuildQuery(new Dictionary<string, string?>
            {
                ["page"] = query.Page.ToString(),
                ["pageSize"] = query.PageSize.ToString(),
                ["status"] = query.Status,
                ["targetType"] = query.TargetType,
                ["reasonCode"] = query.ReasonCode
            })}"));

        public Task<bool> ResolveAsync(long reportId, ResolveReportRequestDto request) =>
            _api.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"api/reports/{reportId}/resolve") { Content = JsonContent.Create(request) });

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
