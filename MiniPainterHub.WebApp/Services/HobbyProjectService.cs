using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services;

public sealed class HobbyProjectService : IHobbyProjectService
{
    private const string BaseRoute = "api/projects";
    private readonly ApiClient _api;

    public HobbyProjectService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetAllAsync(HobbyProjectQueryDto query) =>
        _api.SendForResultAsync<PagedResult<HobbyProjectSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, BaseRoute + BuildQuery(query)));

    public Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetMineAsync(HobbyProjectQueryDto query) =>
        _api.SendForResultAsync<PagedResult<HobbyProjectSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, $"{BaseRoute}/mine{BuildQuery(query)}"));

    public Task<ApiResult<PagedResult<HobbyProjectSummaryDto>?>> GetByOwnerAsync(string ownerUserId, HobbyProjectQueryDto query) =>
        _api.SendForResultAsync<PagedResult<HobbyProjectSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, BaseRoute + BuildQuery(query, ownerUserId)));

    public Task<ApiResult<HobbyProjectDto?>> GetAsync(int projectId) =>
        _api.SendForResultAsync<HobbyProjectDto?>(
            new HttpRequestMessage(HttpMethod.Get, $"{BaseRoute}/{projectId}"));

    public Task<ApiResult<PagedResult<HobbyProjectEntryDto>?>> GetDiaryAsync(int projectId, int page, int pageSize) =>
        GetEntriesAsync(projectId, "diary", page, pageSize);

    public Task<ApiResult<PagedResult<HobbyProjectEntryDto>?>> GetShowcaseAsync(int projectId, int page, int pageSize) =>
        GetEntriesAsync(projectId, "showcase", page, pageSize);

    public Task<ApiResult<PagedResult<PostSummaryDto>?>> GetAvailablePostsAsync(int projectId, string? search, int page, int pageSize) =>
        _api.SendForResultAsync<PagedResult<PostSummaryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, BuildAvailablePostsRoute(projectId, search, page, pageSize)));

    public Task<ApiResult<HobbyProjectDto?>> CreateAsync(CreateHobbyProjectDto request) =>
        SendMutationAsync(HttpMethod.Post, BaseRoute, request);

    public Task<ApiResult<HobbyProjectDto?>> UpdateAsync(int projectId, UpdateHobbyProjectDto request) =>
        SendMutationAsync(HttpMethod.Put, $"{BaseRoute}/{projectId}", request);

    public Task<ApiResult<HobbyProjectDto?>> UpdateStatusAsync(int projectId, UpdateHobbyProjectStatusDto request) =>
        SendMutationAsync(HttpMethod.Put, $"{BaseRoute}/{projectId}/status", request);

    public Task<ApiResult<HobbyProjectDto?>> ArchiveAsync(int projectId) =>
        SendMutationAsync(HttpMethod.Post, $"{BaseRoute}/{projectId}/archive");

    public Task<ApiResult<HobbyProjectDto?>> UnarchiveAsync(int projectId) =>
        SendMutationAsync(HttpMethod.Post, $"{BaseRoute}/{projectId}/restore");

    public Task<ApiResult<HobbyProjectDto?>> LinkPostAsync(int projectId, LinkHobbyProjectPostDto request) =>
        SendMutationAsync(HttpMethod.Post, $"{BaseRoute}/{projectId}/posts", request);

    public Task<ApiResult<HobbyProjectDto?>> UpdateEntryAsync(int projectId, int postId, UpdateHobbyProjectEntryDto request) =>
        SendMutationAsync(HttpMethod.Put, $"{BaseRoute}/{projectId}/posts/{postId}", request);

    public Task<ApiResult<HobbyProjectDto?>> UnlinkPostAsync(int projectId, int postId) =>
        SendMutationAsync(HttpMethod.Delete, $"{BaseRoute}/{projectId}/posts/{postId}");

    public Task<ApiResult<HobbyProjectDto?>> UpdateShowcaseAsync(int projectId, UpdateHobbyProjectShowcaseDto request) =>
        SendMutationAsync(HttpMethod.Put, $"{BaseRoute}/{projectId}/showcase", request);

    public Task<ApiResult<HobbyProjectDto?>> UpdateCoverAsync(int projectId, UpdateHobbyProjectCoverDto request) =>
        SendMutationAsync(HttpMethod.Put, $"{BaseRoute}/{projectId}/cover", request);

    private Task<ApiResult<PagedResult<HobbyProjectEntryDto>?>> GetEntriesAsync(int projectId, string view, int page, int pageSize) =>
        _api.SendForResultAsync<PagedResult<HobbyProjectEntryDto>?>(
            new HttpRequestMessage(HttpMethod.Get, $"{BaseRoute}/{projectId}/{view}?page={page}&pageSize={pageSize}"));

    private Task<ApiResult<HobbyProjectDto?>> SendMutationAsync(HttpMethod method, string route) =>
        _api.SendForResultAsync<HobbyProjectDto?>(new HttpRequestMessage(method, route));

    private Task<ApiResult<HobbyProjectDto?>> SendMutationAsync<TRequest>(HttpMethod method, string route, TRequest request) =>
        _api.SendForResultAsync<HobbyProjectDto?>(new HttpRequestMessage(method, route)
        {
            Content = ApiClient.CreateJsonContent(request)
        });

    private static string BuildAvailablePostsRoute(int projectId, string? search, int page, int pageSize)
    {
        var route = $"{BaseRoute}/{projectId}/available-posts?page={page}&pageSize={pageSize}";
        return string.IsNullOrWhiteSpace(search)
            ? route
            : $"{route}&search={Uri.EscapeDataString(search.Trim())}";
    }

    private static string BuildQuery(HobbyProjectQueryDto query, string? ownerUserIdOverride = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        var values = new Dictionary<string, string?>
        {
            ["search"] = query.Search,
            ["ownerUserId"] = ownerUserIdOverride ?? query.OwnerUserId,
            ["kind"] = query.Kind,
            ["status"] = query.Status,
            ["sort"] = query.Sort,
            ["includeArchived"] = query.IncludeArchived ? "true" : null,
            ["pageNumber"] = query.PageNumber.ToString(),
            ["pageSize"] = query.PageSize.ToString()
        };

        var parts = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}");

        return "?" + string.Join("&", parts);
    }
}
