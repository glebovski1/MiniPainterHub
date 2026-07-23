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
using System.Threading;
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

        public async Task<SearchOverviewDto> GetOverviewAsync(string? query, CancellationToken cancellationToken = default)
        {
            if (!HasSearchTerm(query))
            {
                return new SearchOverviewDto();
            }

            var normalizedQuery = TagTextUtilities.NormalizeText(query!);
            var posts = await GetOverviewPostsAsync(normalizedQuery, cancellationToken);
            var projects = await GetOverviewProjectsAsync(normalizedQuery, cancellationToken);
            var users = await GetOverviewUsersAsync(normalizedQuery, cancellationToken);
            var tags = await GetOverviewTagsAsync(normalizedQuery, cancellationToken);

            var result = new SearchOverviewDto
            {
                Posts = posts,
                Projects = projects,
                Users = users,
                Tags = tags
            };
            return result;
        }

        public async Task<PagedResult<HobbyProjectSummaryDto>> SearchProjectsAsync(
            string? query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            ValidatePaging(page, pageSize);
            if (!HasSearchTerm(query))
            {
                return EmptyPage<HobbyProjectSummaryDto>(page, pageSize);
            }

            var normalizedQuery = TagTextUtilities.NormalizeText(query!);
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);
            var candidates = _appDbContext.HobbyProjects
                .AsNoTracking()
                .Where(project =>
                    !project.IsHidden
                    && project.ArchivedUtc == null
                    && project.Entries.Any(entry => !entry.Post.IsDeleted))
                .Select(project => new SearchProjectCandidate
                {
                    Id = project.Id,
                    TitleStartsMatch = EF.Functions.Like(project.Title.ToLower(), startsPattern, LikeEscape),
                    TitleContainsMatch = EF.Functions.Like(project.Title.ToLower(), containsPattern, LikeEscape),
                    DescriptionContainsMatch = EF.Functions.Like(project.Description.ToLower(), containsPattern, LikeEscape),
                    MetadataContainsMatch = EF.Functions.Like((project.GameSystem ?? string.Empty).ToLower(), containsPattern, LikeEscape)
                        || EF.Functions.Like((project.FactionTheme ?? string.Empty).ToLower(), containsPattern, LikeEscape)
                        || EF.Functions.Like((project.Goal ?? string.Empty).ToLower(), containsPattern, LikeEscape),
                    OwnerContainsMatch = EF.Functions.Like(
                        ((project.OwnerUser.Profile != null ? project.OwnerUser.Profile.DisplayName : null)
                            ?? project.OwnerUser.DisplayName
                            ?? project.OwnerUser.UserName
                            ?? string.Empty).ToLower(),
                        containsPattern,
                        LikeEscape),
                    UpdatedUtc = project.UpdatedUtc
                })
                .Where(project =>
                    project.TitleContainsMatch
                    || project.DescriptionContainsMatch
                    || project.MetadataContainsMatch
                    || project.OwnerContainsMatch);

            var totalCount = await candidates.CountAsync(cancellationToken);
            var pageIds = await candidates
                .OrderByDescending(project => project.TitleStartsMatch)
                .ThenByDescending(project => project.TitleContainsMatch)
                .ThenByDescending(project => project.DescriptionContainsMatch)
                .ThenByDescending(project => project.MetadataContainsMatch)
                .ThenByDescending(project => project.OwnerContainsMatch)
                .ThenByDescending(project => project.UpdatedUtc)
                .ThenByDescending(project => project.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(project => project.Id)
                .ToListAsync(cancellationToken);

            if (pageIds.Count == 0)
            {
                return CreatePage(Array.Empty<HobbyProjectSummaryDto>(), totalCount, page, pageSize);
            }

            var projects = await LoadProjectSummariesAsync(pageIds, cancellationToken);

            var items = pageIds
                .Where(projects.ContainsKey)
                .Select(projectId => projects[projectId])
                .ToList();
            var result = CreatePage(items, totalCount, page, pageSize);
            return result;
        }

        public async Task<PagedResult<PostSummaryDto>> SearchPostsAsync(string? query, string? tagSlug, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            ValidatePaging(page, pageSize);

            var hasSearchTerm = HasSearchTerm(query);
            var normalizedQuery = hasSearchTerm ? TagTextUtilities.NormalizeText(query!) : string.Empty;
            var normalizedTagSlug = string.IsNullOrWhiteSpace(tagSlug) ? null : tagSlug.Trim().ToLowerInvariant();

            if (!hasSearchTerm && string.IsNullOrWhiteSpace(normalizedTagSlug))
            {
                return EmptyPage<PostSummaryDto>(page, pageSize);
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
                    Project = p.HobbyProjectEntry != null
                        && !p.HobbyProjectEntry.Project.IsHidden
                        && p.HobbyProjectEntry.Project.ArchivedUtc == null
                        && p.HobbyProjectEntry.Project.Entries.Any(entry => !entry.Post.IsDeleted)
                            ? new HobbyProjectReferenceDto
                            {
                                Id = p.HobbyProjectEntry.Project.Id,
                                Title = p.HobbyProjectEntry.Project.Title,
                                Status = p.HobbyProjectEntry.Project.Status,
                                IsPublic = true
                            }
                            : null,
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

            var totalCount = await rankedPosts.CountAsync(cancellationToken);
            var items = await rankedPosts
                .OrderByDescending(p => p.ExactTagMatch)
                .ThenByDescending(p => p.TitleStartsMatch)
                .ThenByDescending(p => p.TitleContainsMatch)
                .ThenByDescending(p => p.ContentContainsMatch)
                .ThenByDescending(p => p.TagContainsMatch)
                .ThenByDescending(p => p.Summary.CreatedAt)
                .ThenByDescending(p => p.Summary.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => p.Summary)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.ThumbnailUrl = ResolveSummaryThumbnailUrl(item.ImageUrl, item.ThumbnailUrl);
            }

            var result = CreatePage(items, totalCount, page, pageSize);
            return result;
        }

        public async Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default)
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

            var totalCount = await usersQuery.CountAsync(cancellationToken);
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
                .ToListAsync(cancellationToken);

            return CreatePage(items, totalCount, page, pageSize);
        }

        public async Task<PagedResult<SearchTagResultDto>> SearchTagsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default)
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

            var totalCount = await tagsQuery.CountAsync(cancellationToken);
            var items = await tagsQuery
                .OrderByDescending(t => t.NormalizedName == normalizedQuery)
                .ThenByDescending(t => EF.Functions.Like(t.NormalizedName, startsPattern, LikeEscape))
                .ThenByDescending(t => EF.Functions.Like(t.NormalizedName, containsPattern, LikeEscape))
                .ThenByDescending(t => t.Tag.PostCount)
                .ThenBy(t => t.Tag.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => t.Tag)
                .ToListAsync(cancellationToken);

            var result = CreatePage(items, totalCount, page, pageSize);
            SetCache(cacheKey, result);
            return result;
        }

        private async Task<List<PostSummaryDto>> GetOverviewPostsAsync(string normalizedQuery, CancellationToken cancellationToken)
        {
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);
            var items = await _appDbContext.Posts
                .AsNoTracking()
                .Where(post => !post.IsDeleted
                    && (EF.Functions.Like(post.Title.ToLower(), containsPattern, LikeEscape)
                        || EF.Functions.Like(post.Content.ToLower(), containsPattern, LikeEscape)
                        || post.PostTags.Any(postTag => EF.Functions.Like(postTag.Tag.NormalizedName, containsPattern, LikeEscape))))
                .Select(post => new SearchPostCandidate
                {
                    ExactTagMatch = post.PostTags.Any(postTag => postTag.Tag.NormalizedName == normalizedQuery),
                    TitleStartsMatch = EF.Functions.Like(post.Title.ToLower(), startsPattern, LikeEscape),
                    TitleContainsMatch = EF.Functions.Like(post.Title.ToLower(), containsPattern, LikeEscape),
                    ContentContainsMatch = EF.Functions.Like(post.Content.ToLower(), containsPattern, LikeEscape),
                    TagContainsMatch = post.PostTags.Any(postTag => EF.Functions.Like(postTag.Tag.NormalizedName, containsPattern, LikeEscape)),
                    Summary = new PostSummaryDto
                    {
                        Id = post.Id,
                        Title = post.Title,
                        Snippet = post.Content.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content,
                        MiniatureName = post.MiniatureName,
                        Techniques = post.Techniques,
                        Difficulty = post.Difficulty,
                        ImageUrl = post.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).FirstOrDefault(),
                        ThumbnailUrl = post.Images
                            .OrderBy(image => image.Id)
                            .Select(image => image.ThumbnailUrl != null && image.ThumbnailUrl != string.Empty
                                ? image.ThumbnailUrl
                                : image.PreviewUrl ?? image.ImageUrl)
                            .FirstOrDefault(),
                        AuthorName = post.CreatedBy.Profile != null && post.CreatedBy.Profile.DisplayName != null
                            ? post.CreatedBy.Profile.DisplayName
                            : post.CreatedBy.UserName ?? string.Empty,
                        AuthorId = post.CreatedById,
                        CreatedAt = post.CreatedUtc,
                        CommentCount = post.Comments.Count,
                        LikeCount = post.Likes.Count,
                        IsDeleted = post.IsDeleted,
                        Project = post.HobbyProjectEntry != null
                            && !post.HobbyProjectEntry.Project.IsHidden
                            && post.HobbyProjectEntry.Project.ArchivedUtc == null
                            && post.HobbyProjectEntry.Project.Entries.Any(entry => !entry.Post.IsDeleted)
                                ? new HobbyProjectReferenceDto
                                {
                                    Id = post.HobbyProjectEntry.Project.Id,
                                    Title = post.HobbyProjectEntry.Project.Title,
                                    Status = post.HobbyProjectEntry.Project.Status,
                                    IsPublic = true
                                }
                                : null,
                        Tags = post.PostTags
                            .OrderBy(postTag => postTag.Tag.DisplayName)
                            .Select(postTag => new TagDto
                            {
                                Name = postTag.Tag.DisplayName,
                                Slug = postTag.Tag.Slug
                            })
                            .ToList()
                    }
                })
                .OrderByDescending(post => post.ExactTagMatch)
                .ThenByDescending(post => post.TitleStartsMatch)
                .ThenByDescending(post => post.TitleContainsMatch)
                .ThenByDescending(post => post.ContentContainsMatch)
                .ThenByDescending(post => post.TagContainsMatch)
                .ThenByDescending(post => post.Summary.CreatedAt)
                .ThenByDescending(post => post.Summary.Id)
                .Take(OverviewTake)
                .Select(post => post.Summary)
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.ThumbnailUrl = ResolveSummaryThumbnailUrl(item.ImageUrl, item.ThumbnailUrl);
            }

            return items;
        }

        private async Task<List<HobbyProjectSummaryDto>> GetOverviewProjectsAsync(string normalizedQuery, CancellationToken cancellationToken)
        {
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);
            var ids = await _appDbContext.HobbyProjects
                .AsNoTracking()
                .Where(project => !project.IsHidden
                    && project.ArchivedUtc == null
                    && project.Entries.Any(entry => !entry.Post.IsDeleted))
                .Select(project => new SearchProjectCandidate
                {
                    Id = project.Id,
                    TitleStartsMatch = EF.Functions.Like(project.Title.ToLower(), startsPattern, LikeEscape),
                    TitleContainsMatch = EF.Functions.Like(project.Title.ToLower(), containsPattern, LikeEscape),
                    DescriptionContainsMatch = EF.Functions.Like(project.Description.ToLower(), containsPattern, LikeEscape),
                    MetadataContainsMatch = EF.Functions.Like((project.GameSystem ?? string.Empty).ToLower(), containsPattern, LikeEscape)
                        || EF.Functions.Like((project.FactionTheme ?? string.Empty).ToLower(), containsPattern, LikeEscape)
                        || EF.Functions.Like((project.Goal ?? string.Empty).ToLower(), containsPattern, LikeEscape),
                    OwnerContainsMatch = EF.Functions.Like(
                        ((project.OwnerUser.Profile != null ? project.OwnerUser.Profile.DisplayName : null)
                            ?? project.OwnerUser.DisplayName
                            ?? project.OwnerUser.UserName
                            ?? string.Empty).ToLower(),
                        containsPattern,
                        LikeEscape),
                    UpdatedUtc = project.UpdatedUtc
                })
                .Where(project => project.TitleContainsMatch
                    || project.DescriptionContainsMatch
                    || project.MetadataContainsMatch
                    || project.OwnerContainsMatch)
                .OrderByDescending(project => project.TitleStartsMatch)
                .ThenByDescending(project => project.TitleContainsMatch)
                .ThenByDescending(project => project.DescriptionContainsMatch)
                .ThenByDescending(project => project.MetadataContainsMatch)
                .ThenByDescending(project => project.OwnerContainsMatch)
                .ThenByDescending(project => project.UpdatedUtc)
                .ThenByDescending(project => project.Id)
                .Take(OverviewTake)
                .Select(project => project.Id)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                return new List<HobbyProjectSummaryDto>();
            }

            var projects = await LoadProjectSummariesAsync(ids, cancellationToken);
            return ids.Where(projects.ContainsKey).Select(id => projects[id]).ToList();
        }

        private async Task<List<UserListItemDto>> GetOverviewUsersAsync(string normalizedQuery, CancellationToken cancellationToken)
        {
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);
            return await _appDbContext.Users
                .AsNoTracking()
                .Select(user => new SearchUserCandidate
                {
                    SearchDisplayName = ((user.Profile != null ? user.Profile.DisplayName : null) ?? user.DisplayName ?? user.UserName ?? string.Empty).ToLower(),
                    SearchUserName = (user.UserName ?? string.Empty).ToLower(),
                    SearchBio = ((user.Profile != null ? user.Profile.Bio : null) ?? string.Empty).ToLower(),
                    User = new UserListItemDto
                    {
                        UserId = user.Id,
                        UserName = user.UserName ?? string.Empty,
                        DisplayName = user.Profile != null && user.Profile.DisplayName != null
                            ? user.Profile.DisplayName
                            : user.DisplayName ?? user.UserName ?? string.Empty,
                        AvatarUrl = user.Profile != null && user.Profile.AvatarUrl != null
                            ? user.Profile.AvatarUrl
                            : user.AvatarUrl
                    }
                })
                .Where(user => EF.Functions.Like(user.SearchDisplayName, containsPattern, LikeEscape)
                    || EF.Functions.Like(user.SearchUserName, containsPattern, LikeEscape)
                    || EF.Functions.Like(user.SearchBio, containsPattern, LikeEscape))
                .OrderByDescending(user => EF.Functions.Like(user.SearchDisplayName, startsPattern, LikeEscape))
                .ThenByDescending(user => EF.Functions.Like(user.SearchDisplayName, containsPattern, LikeEscape))
                .ThenByDescending(user => EF.Functions.Like(user.SearchUserName, startsPattern, LikeEscape))
                .ThenByDescending(user => EF.Functions.Like(user.SearchUserName, containsPattern, LikeEscape))
                .ThenByDescending(user => EF.Functions.Like(user.SearchBio, containsPattern, LikeEscape))
                .ThenBy(user => user.User.DisplayName)
                .ThenBy(user => user.User.UserName)
                .Take(OverviewTake)
                .Select(user => user.User)
                .ToListAsync(cancellationToken);
        }

        private Task<List<SearchTagResultDto>> GetOverviewTagsAsync(string normalizedQuery, CancellationToken cancellationToken)
        {
            var containsPattern = CreateContainsPattern(normalizedQuery);
            var startsPattern = CreateStartsPattern(normalizedQuery);
            return _appDbContext.Tags
                .AsNoTracking()
                .Select(tag => new SearchTagCandidate
                {
                    NormalizedName = tag.NormalizedName,
                    Slug = tag.Slug,
                    Tag = new SearchTagResultDto
                    {
                        Name = tag.DisplayName,
                        Slug = tag.Slug,
                        PostCount = tag.PostTags.Count(postTag => !postTag.Post.IsDeleted)
                    }
                })
                .Where(tag => EF.Functions.Like(tag.NormalizedName, containsPattern, LikeEscape)
                    || EF.Functions.Like(tag.Slug, containsPattern, LikeEscape))
                .OrderByDescending(tag => tag.NormalizedName == normalizedQuery)
                .ThenByDescending(tag => EF.Functions.Like(tag.NormalizedName, startsPattern, LikeEscape))
                .ThenByDescending(tag => EF.Functions.Like(tag.NormalizedName, containsPattern, LikeEscape))
                .ThenByDescending(tag => tag.Tag.PostCount)
                .ThenBy(tag => tag.Tag.Name)
                .Take(OverviewTake)
                .Select(tag => tag.Tag)
                .ToListAsync(cancellationToken);
        }

        private async Task<Dictionary<int, HobbyProjectSummaryDto>> LoadProjectSummariesAsync(
            IReadOnlyCollection<int> projectIds,
            CancellationToken cancellationToken)
        {
            var rows = await HobbyProjectService.ProjectSummaryRows(
                    _appDbContext.HobbyProjects.AsNoTracking().Where(project => projectIds.Contains(project.Id)))
                .ToListAsync(cancellationToken);
            var covers = await HobbyProjectService.LoadCoverCandidatesAsync(_appDbContext, projectIds, cancellationToken);
            return rows.ToDictionary(
                project => project.Id,
                project => HobbyProjectService.MapSummary(project, covers, ownerView: false));
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

        private sealed class SearchProjectCandidate
        {
            public int Id { get; init; }
            public bool TitleStartsMatch { get; init; }
            public bool TitleContainsMatch { get; init; }
            public bool DescriptionContainsMatch { get; init; }
            public bool MetadataContainsMatch { get; init; }
            public bool OwnerContainsMatch { get; init; }
            public DateTime UpdatedUtc { get; init; }
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
