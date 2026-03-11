using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
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
        private readonly AppDbContext _appDbContext;

        public SearchService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<SearchOverviewDto> GetOverviewAsync(string? query)
        {
            if (!HasSearchTerm(query))
            {
                return new SearchOverviewDto();
            }

            var posts = await SearchPostsAsync(query, null, 1, OverviewTake);
            var users = await SearchUsersAsync(query, 1, OverviewTake);
            var tags = await SearchTagsAsync(query, 1, OverviewTake);

            return new SearchOverviewDto
            {
                Posts = posts.Items.ToList(),
                Users = users.Items.ToList(),
                Tags = tags.Items.ToList()
            };
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
                    p.Title.ToLower().Contains(normalizedQuery)
                    || p.Content.ToLower().Contains(normalizedQuery)
                    || p.PostTags.Any(pt => pt.Tag.NormalizedName.Contains(normalizedQuery)));
            }

            var rankedPosts = postsQuery.Select(p => new SearchPostCandidate
            {
                ExactTagMatch = hasSearchTerm && p.PostTags.Any(pt => pt.Tag.NormalizedName == normalizedQuery),
                TitleStartsMatch = hasSearchTerm && p.Title.ToLower().StartsWith(normalizedQuery),
                TitleContainsMatch = hasSearchTerm && p.Title.ToLower().Contains(normalizedQuery),
                ContentContainsMatch = hasSearchTerm && p.Content.ToLower().Contains(normalizedQuery),
                TagContainsMatch = hasSearchTerm && p.PostTags.Any(pt => pt.Tag.NormalizedName.Contains(normalizedQuery)),
                Summary = new PostSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Snippet = p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content,
                    ImageUrl = p.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
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

            return CreatePage(items, totalCount, page, pageSize);
        }

        public async Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? query, int page, int pageSize)
        {
            ValidatePaging(page, pageSize);
            if (!HasSearchTerm(query))
            {
                return EmptyPage<UserListItemDto>(page, pageSize);
            }

            var normalizedQuery = TagTextUtilities.NormalizeText(query!);

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
                    u.SearchDisplayName.Contains(normalizedQuery)
                    || u.SearchUserName.Contains(normalizedQuery)
                    || u.SearchBio.Contains(normalizedQuery));

            var totalCount = await usersQuery.CountAsync();
            var items = await usersQuery
                .OrderByDescending(u => u.SearchDisplayName.StartsWith(normalizedQuery))
                .ThenByDescending(u => u.SearchDisplayName.Contains(normalizedQuery))
                .ThenByDescending(u => u.SearchUserName.StartsWith(normalizedQuery))
                .ThenByDescending(u => u.SearchUserName.Contains(normalizedQuery))
                .ThenByDescending(u => u.SearchBio.Contains(normalizedQuery))
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
                    t.NormalizedName.Contains(normalizedQuery)
                    || t.Slug.Contains(normalizedQuery));

            var totalCount = await tagsQuery.CountAsync();
            var items = await tagsQuery
                .OrderByDescending(t => t.NormalizedName == normalizedQuery)
                .ThenByDescending(t => t.NormalizedName.StartsWith(normalizedQuery))
                .ThenByDescending(t => t.NormalizedName.Contains(normalizedQuery))
                .ThenByDescending(t => t.Tag.PostCount)
                .ThenBy(t => t.Tag.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => t.Tag)
                .ToListAsync();

            return CreatePage(items, totalCount, page, pageSize);
        }

        private static bool HasSearchTerm(string? query) =>
            !string.IsNullOrWhiteSpace(query) && TagTextUtilities.CollapseWhitespace(query).Length >= 2;

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

        private static void ValidatePaging(int page, int pageSize)
        {
            var errors = new Dictionary<string, string[]>();
            if (page < 1)
            {
                errors["page"] = new[] { "Page number must be at least 1." };
            }

            if (pageSize <= 0)
            {
                errors["pageSize"] = new[] { "Page size must be greater than 0." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Pagination parameters are invalid.", errors);
            }
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
