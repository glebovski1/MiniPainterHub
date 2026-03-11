using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services
{
    public sealed class SearchService : ISearchService
    {
        private readonly ApiClient _api;

        public SearchService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<SearchOverviewDto?>> GetOverviewAsync(string? query) =>
            _api.SendForResultAsync<SearchOverviewDto?>(new HttpRequestMessage(HttpMethod.Get, $"api/search/overview{BuildQuery(new Dictionary<string, string?> { ["q"] = query })}"));

        public Task<ApiResult<PagedResult<PostSummaryDto>?>> SearchPostsAsync(string? query, string? tag, int page, int pageSize) =>
            _api.SendForResultAsync<PagedResult<PostSummaryDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/search/posts{BuildQuery(new Dictionary<string, string?>
            {
                ["q"] = query,
                ["tag"] = tag,
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString()
            })}"));

        public Task<ApiResult<PagedResult<UserListItemDto>?>> SearchUsersAsync(string? query, int page, int pageSize) =>
            _api.SendForResultAsync<PagedResult<UserListItemDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/search/users{BuildQuery(new Dictionary<string, string?>
            {
                ["q"] = query,
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString()
            })}"));

        public Task<ApiResult<PagedResult<SearchTagResultDto>?>> SearchTagsAsync(string? query, int page, int pageSize) =>
            _api.SendForResultAsync<PagedResult<SearchTagResultDto>?>(new HttpRequestMessage(HttpMethod.Get, $"api/search/tags{BuildQuery(new Dictionary<string, string?>
            {
                ["q"] = query,
                ["page"] = page.ToString(),
                ["pageSize"] = pageSize.ToString()
            })}"));

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
