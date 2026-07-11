using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Features.Tags;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public sealed class SearchService : ISearchService
    {
        private const int OverviewTake = 5;
        private const string LikeEscape = "\\";
        private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromSeconds(15);
        private readonly AppDbContext _appDbContext;
        private readonly IMemoryCache? _cache;

        public SearchService(AppDbContext appDbContext, IMemoryCache? cache = null)
        {
            _appDbContext = appDbContext;
            _cache = cache;
        }

        public async Task<SearchOverviewDto> GetOverviewAsync(string? query)
        {
            if (!HasSearchTerm(query))
            {
                return new SearchOverviewDto();
            }

            var cacheKey = CreateCacheKey("overview", query);
            if (TryGetCached(cacheKey, out SearchOverviewDto? cachedOverview))
            {
                return cachedOverview!;
            }

            var posts = await SearchPostsAsync(query, null, 1, OverviewTake);
            var users = await SearchUsersAsync(query, 1, OverviewTake);
            var tags = await SearchTagsAsync(query, 1, OverviewTake);

            var result = new SearchOverviewDto
            {
                Posts = posts.Items.ToList(),
                Users = users.Items.ToList(),
                Tags = tags.Items.ToList()
            };
            SetCache(cacheKey, result);
            return result;
        }

        public async Task<PagedResult<PostSummaryDto>> SearchPostsAsync(string? query, string? tagSlug, int page, int pageSize)
        {
            ValidatePaging(page, pageSize);

            var hasSearchTerm = HasSearchTerm(query);
            var normalizedQuery = hasSearchTerm ? TagTextUtilities.NormalizeText(query!) : string.Empty;
            var normalizedTagSlug = string.IsNullOrWhiteSpace(tagSlug) ? null : tagSlug.Trim().ToLowerInvariant();

            if (!hasSearchTerm && string.IsNullOrWhiteSpace(normalizedTagSlug))
            {
                return EmptyPage<PostSummaryDto>(page, pageSize);
            }

            var cacheKey = CreateCacheKey("posts", normalizedQuery, normalizedTagSlug ?? string.Empty, page, pageSize);
            if (TryGetCached(cacheKey, out PagedResult<PostSummaryDto>? cachedPosts))
            {
                return cachedPosts!;
            }

            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);

            var postsQuery = _appDbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(normalizedTagSlug))
            {
                postsQuery = postsQuery.Where(p => p.PostTags.Any(pt => pt.Tag.Slug == normalizedTagSlug));
            }

            if (hasSearchTerm)
            {
                postsQuery = postsQuery.Where(p =>
                    EF.Functions.Like(p.Title.ToLower(), containsPattern, LikeEscape)
                    || EF.Functions.Like(p.Content.ToLower(), containsPattern, LikeEscape)
                    || p.PostTags.Any(pt => EF.Functions.Like(pt.Tag.NormalizedName, containsPattern, LikeEscape)));
            }

            var rankedPosts = postsQuery.Select(p => new SearchPostCandidate
            {
                ExactTagMatch = hasSearchTerm && p.PostTags.Any(pt => pt.Tag.NormalizedName == normalizedQuery),
                TitleStartsMatch = hasSearchTerm && EF.Functions.Like(p.Title.ToLower(), startsPattern, LikeEscape),
                TitleContainsMatch = hasSearchTerm && EF.Functions.Like(p.Title.ToLower(), containsPattern, LikeEscape),
                ContentContainsMatch = hasSearchTerm && EF.Functions.Like(p.Content.ToLower(), containsPattern, LikeEscape),
                TagContainsMatch = hasSearchTerm && p.PostTags.Any(pt => EF.Functions.Like(pt.Tag.NormalizedName, containsPattern, LikeEscape)),
                Summary = new PostSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Snippet = p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content,
                    MiniatureName = p.MiniatureName,
                    Techniques = p.Techniques,
                    Difficulty = p.Difficulty,
                    ImageUrl = p.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    ThumbnailUrl = p.Images
                        .OrderBy(i => i.Id)
                        .Select(i => i.ThumbnailUrl != null && i.ThumbnailUrl != string.Empty ? i.ThumbnailUrl : i.PreviewUrl ?? i.ImageUrl)
                        .FirstOrDefault(),
                    AuthorName = p.CreatedBy.Profile != null && !string.IsNullOrWhiteSpace(p.CreatedBy.Profile.DisplayName)
                        ? p.CreatedBy.Profile.DisplayName
                        : (p.CreatedBy.UserName ?? string.Empty),
                    AuthorId = p.CreatedById,
                    CreatedAt = p.CreatedUtc,
                    CommentCount = p.Comments.Count,
                    LikeCount = p.Likes.Count,
                    IsDeleted = p.IsDeleted,
                    Tags = p.PostTags
                        .OrderBy(pt => pt.Tag.DisplayName)
                        .Select(pt => new TagDto
                        {
                            Name = pt.Tag.DisplayName,
                            Slug = pt.Tag.Slug
                        })
                        .ToList()
                }
            });

            var totalCount = await rankedPosts.CountAsync();
            var items = await rankedPosts
                .OrderByDescending(p => p.ExactTagMatch)
                .ThenByDescending(p => p.TitleStartsMatch)
                .ThenByDescending(p => p.TitleContainsMatch)
                .ThenByDescending(p => p.ContentContainsMatch)
                .ThenByDescending(p => p.TagContainsMatch)
                .ThenByDescending(p => p.Summary.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => p.Summary)
                .ToListAsync();

            foreach (var item in items)
            {
                item.ThumbnailUrl = ResolveSummaryThumbnailUrl(item.ImageUrl, item.ThumbnailUrl);
            }

            var result = CreatePage(items, totalCount, page, pageSize);
            SetCache(cacheKey, result);
            return result;
        }

        public async Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? query, int page, int pageSize)
        {
            ValidatePaging(page, pageSize);
            if (!HasSearchTerm(query))
            {
                return EmptyPage<UserListItemDto>(page, pageSize);
            }

            var normalizedQuery = TagTextUtilities.NormalizeText(query!);
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);

            var usersQuery = _appDbContext.Users
                .AsNoTracking()
                .Select(u => new SearchUserCandidate
                {
                    SearchDisplayName = ((u.Profile != null ? u.Profile.DisplayName : null) ?? u.DisplayName ?? u.UserName ?? string.Empty).ToLower(),
                    SearchUserName = (u.UserName ?? string.Empty).ToLower(),
                    SearchBio = ((u.Profile != null ? u.Profile.Bio : null) ?? string.Empty).ToLower(),
                    User = new UserListItemDto
                    {
                        UserId = u.Id,
                        UserName = u.UserName ?? string.Empty,
                        DisplayName = u.Profile != null && !string.IsNullOrWhiteSpace(u.Profile.DisplayName)
                            ? u.Profile.DisplayName
                            : (u.DisplayName ?? u.UserName ?? string.Empty),
                        AvatarUrl = u.Profile != null && !string.IsNullOrWhiteSpace(u.Profile.AvatarUrl)
                            ? u.Profile.AvatarUrl
                            : u.AvatarUrl
                    }
                })
                .Where(u =>
                    EF.Functions.Like(u.SearchDisplayName, containsPattern, LikeEscape)
                    || EF.Functions.Like(u.SearchUserName, containsPattern, LikeEscape)
                    || EF.Functions.Like(u.SearchBio, containsPattern, LikeEscape));

            var totalCount = await usersQuery.CountAsync();
            var items = await usersQuery
                .OrderByDescending(u => EF.Functions.Like(u.SearchDisplayName, startsPattern, LikeEscape))
                .ThenByDescending(u => EF.Functions.Like(u.SearchDisplayName, containsPattern, LikeEscape))
                .ThenByDescending(u => EF.Functions.Like(u.SearchUserName, startsPattern, LikeEscape))
                .ThenByDescending(u => EF.Functions.Like(u.SearchUserName, containsPattern, LikeEscape))
                .ThenByDescending(u => EF.Functions.Like(u.SearchBio, containsPattern, LikeEscape))
                .ThenBy(u => u.User.DisplayName)
                .ThenBy(u => u.User.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => u.User)
                .ToListAsync();

            return CreatePage(items, totalCount, page, pageSize);
        }

        public async Task<PagedResult<SearchTagResultDto>> SearchTagsAsync(string? query, int page, int pageSize)
        {
            ValidatePaging(page, pageSize);
            if (!HasSearchTerm(query))
            {
                return EmptyPage<SearchTagResultDto>(page, pageSize);
            }

            var normalizedQuery = TagTextUtilities.NormalizeText(query!);
            var cacheKey = CreateCacheKey("tags", normalizedQuery, page, pageSize);
            if (TryGetCached(cacheKey, out PagedResult<SearchTagResultDto>? cachedTags))
            {
                return cachedTags!;
            }

            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);

            var tagsQuery = _appDbContext.Tags
                .AsNoTracking()
                .Select(t => new SearchTagCandidate
                {
                    NormalizedName = t.NormalizedName,
                    Slug = t.Slug,
                    Tag = new SearchTagResultDto
                    {
                        Name = t.DisplayName,
                        Slug = t.Slug,
                        PostCount = t.PostTags.Count(pt => !pt.Post.IsDeleted)
                    }
                })
                .Where(t =>
                    EF.Functions.Like(t.NormalizedName, containsPattern, LikeEscape)
                    || EF.Functions.Like(t.Slug, containsPattern, LikeEscape));

            var totalCount = await tagsQuery.CountAsync();
            var items = await tagsQuery
                .OrderByDescending(t => t.NormalizedName == normalizedQuery)
                .ThenByDescending(t => EF.Functions.Like(t.NormalizedName, startsPattern, LikeEscape))
                .ThenByDescending(t => EF.Functions.Like(t.NormalizedName, containsPattern, LikeEscape))
                .ThenByDescending(t => t.Tag.PostCount)
                .ThenBy(t => t.Tag.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => t.Tag)
                .ToListAsync();

            var result = CreatePage(items, totalCount, page, pageSize);
            SetCache(cacheKey, result);
            return result;
        }

        private static bool HasSearchTerm(string? query) =>
            !string.IsNullOrWhiteSpace(query) && TagTextUtilities.CollapseWhitespace(query).Length >= 2;

        private static string CreateContainsPattern(string normalizedQuery) =>
            "%" + EscapeLikePattern(normalizedQuery) + "%";

        private static string CreateStartsPattern(string normalizedQuery) =>
            EscapeLikePattern(normalizedQuery) + "%";

        private static string EscapeLikePattern(string value) =>
            value
                .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
                .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
                .Replace("_", LikeEscape + "_", StringComparison.Ordinal)
                .Replace("[", "[[]", StringComparison.Ordinal);

        private static string CreateCacheKey(string area, params object?[] values) =>
            "search:" + area + ":" + string.Join(":", values.Select(value => value?.ToString() ?? string.Empty));

        private bool TryGetCached<T>(string key, out T? value)
        {
            if (_cache is not null && _cache.TryGetValue(key, out T? cached))
            {
                value = cached;
                return true;
            }

            value = default;
            return false;
        }

        private void SetCache<T>(string key, T value)
        {
            _cache?.Set(key, value, SearchCacheDuration);
        }

        private static PagedResult<T> CreatePage<T>(IReadOnlyList<T> items, int totalCount, int page, int pageSize) =>
            new()
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

        private static PagedResult<T> EmptyPage<T>(int page, int pageSize) =>
            new()
            {
                Items = Array.Empty<T>(),
                TotalCount = 0,
                PageNumber = page,
                PageSize = pageSize
            };

        private static string? ResolveSummaryThumbnailUrl(string? imageUrl, string? thumbnailUrl)
        {
            if (!string.IsNullOrWhiteSpace(thumbnailUrl)
                && !string.Equals(thumbnailUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
            {
                return thumbnailUrl;
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return thumbnailUrl;
            }

            var path = imageUrl;
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }

            return path.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase)
                ? "/api/images/thumbnail?url=" + Uri.EscapeDataString(path)
                : thumbnailUrl;
        }

        private static void ValidatePaging(int page, int pageSize)
        {
            PaginationGuard.Validate(page, pageSize);
        }

        private sealed class SearchPostCandidate
        {
            public PostSummaryDto Summary { get; init; } = new();
            public bool ExactTagMatch { get; init; }
            public bool TitleStartsMatch { get; init; }
            public bool TitleContainsMatch { get; init; }
            public bool ContentContainsMatch { get; init; }
            public bool TagContainsMatch { get; init; }
        }

        private sealed class SearchUserCandidate
        {
            public UserListItemDto User { get; init; } = new();
            public string SearchDisplayName { get; init; } = string.Empty;
            public string SearchUserName { get; init; } = string.Empty;
            public string SearchBio { get; init; } = string.Empty;
        }

        private sealed class SearchTagCandidate
        {
            public SearchTagResultDto Tag { get; init; } = new();
            public string NormalizedName { get; init; } = string.Empty;
            public string Slug { get; init; } = string.Empty;
        }
    }
}
