using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Media;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Features.Tags;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _appDbContext;
        private readonly IPostImageAttachmentService _postImageAttachmentService;
        private readonly IAccountRestrictionService? _accountRestrictionService;
        private readonly IHobbyProjectPostLinker? _hobbyProjectPostLinker;

        public PostService(
            AppDbContext appDbContext,
            IPostImageAttachmentService postImageAttachmentService,
            IAccountRestrictionService? accountRestrictionService = null,
            IHobbyProjectPostLinker? hobbyProjectPostLinker = null)
        {
            _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
            _postImageAttachmentService = postImageAttachmentService ?? throw new ArgumentNullException(nameof(postImageAttachmentService));
            _accountRestrictionService = accountRestrictionService;
            _hobbyProjectPostLinker = hobbyProjectPostLinker;
        }

        public async Task<PostDto> CreateAsync(string userId, CreatePostDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.Title, nameof(dto.Title));
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.Content, nameof(dto.Content));

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            }

            if (_accountRestrictionService != null)
            {
                await _accountRestrictionService.EnsureCanCreatePostAsync(userId);
            }

            var user = await _appDbContext.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            }

            var newPost = new Post
            {
                CreatedById = userId,
                CreatedBy = user,
                Title = dto.Title,
                Content = dto.Content,
                MiniatureName = NormalizeRecipeField(dto.MiniatureName),
                PaintsUsed = NormalizeRecipeField(dto.PaintsUsed),
                Techniques = NormalizeRecipeField(dto.Techniques),
                Difficulty = NormalizeRecipeField(dto.Difficulty),
                TimeSpent = NormalizeRecipeField(dto.TimeSpent),
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Images = new List<PostImage>()
            };

            if (dto.Images != null)
            {
                foreach (var img in dto.Images.Take(PostImageUploadValidator.MaxImagesPerPost))
                {
                    newPost.Images.Add(new PostImage
                    {
                        ImageUrl = img.ImageUrl,
                        PreviewUrl = string.IsNullOrWhiteSpace(img.PreviewUrl) ? img.ImageUrl : img.PreviewUrl,
                        ThumbnailUrl = img.ThumbnailUrl,
                        Width = img.Width,
                        Height = img.Height
                    });
                }
            }

            _appDbContext.Posts.Add(newPost);
            if (dto.ProjectId.HasValue)
            {
                if (_hobbyProjectPostLinker is null)
                {
                    throw new InvalidOperationException("Hobby project post linking is unavailable.");
                }

                await _hobbyProjectPostLinker.LinkNewPostAsync(userId, newPost, dto.ProjectId.Value, dto.MilestoneLabel);
            }
            else if (!string.IsNullOrWhiteSpace(dto.MilestoneLabel))
            {
                throw new DomainValidationException("Invalid project link.", new Dictionary<string, string[]>
                {
                    ["milestoneLabel"] = new[] { "A milestone label requires a hobby project." }
                });
            }

            await SyncTagsAsync(newPost, dto.Tags);
            try
            {
                await _appDbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (dto.ProjectId.HasValue && IsLikelyUniqueConstraint(ex))
            {
                throw new ConflictException("That post is already linked to a hobby project.", ex);
            }

            return PostDtoMapper.ToPostDto(newPost);
        }

        public async Task<bool> DeleteAsync(int postId, string userId)
        {
            var post = await _appDbContext.Posts
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
            {
                throw new NotFoundException("Post not found.");
            }

            post.IsDeleted = true;
            post.SoftDeletedUtc = DateTime.UtcNow;
            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();

            await _postImageAttachmentService.CleanupDeletedPostImagesAsync(post.Id, post.Images);
            return true;
        }

        public Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize, bool includeDeleted = false, bool deletedOnly = false)
        {
            IQueryable<Post> query = _appDbContext.Posts.AsNoTracking();

            if (deletedOnly)
            {
                query = query.Where(p => p.IsDeleted);
            }
            else if (!includeDeleted)
            {
                query = query.Where(p => !p.IsDeleted);
            }

            return GetPagedPostsAsync(query, page, pageSize);
        }

        public Task<PagedResult<PostSummaryDto>> GetByAuthorAsync(string authorUserId, int page, int pageSize) =>
            GetPagedPostsAsync(ActivePosts().Where(p => p.CreatedById == authorUserId), page, pageSize);

        public Task<PagedResult<PostSummaryDto>> GetFollowingFeedAsync(string userId, int page, int pageSize) =>
            GetPagedPostsAsync(
                ActivePosts().Where(p =>
                    _appDbContext.Follows.Any(f => f.FollowerUserId == userId && f.FollowedUserId == p.CreatedById)),
                page,
                pageSize);

        public async Task<IReadOnlyList<PostSummaryDto>> GetTopPostsAsync(int count, TimeSpan lookback)
        {
            ValidateTopPostsQuery(count, lookback);

            var cutoff = DateTime.UtcNow.Subtract(lookback);
            var topItems = await ActivePosts()
                .Where(p => p.CreatedUtc >= cutoff)
                .OrderByDescending(p => p.Likes.Count)
                .ThenByDescending(p => p.CreatedUtc)
                .Take(count)
                .Select(p => new PostSummaryPageItem(
                    p.Id,
                    p.Comments.Count,
                    p.Likes.Count))
                .ToListAsync();

            if (topItems.Count == 0)
            {
                return Array.Empty<PostSummaryDto>();
            }

            var topIds = topItems.Select(item => item.Id).ToList();
            var posts = await BuildPostGraphQuery()
                .Where(p => topIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            return topItems
                .Where(item => posts.ContainsKey(item.Id))
                .Select(item => PostDtoMapper.ToPostSummaryDto(posts[item.Id], item.CommentCount, item.LikeCount))
                .ToList();
        }

        public async Task<PostDto> GetByIdAsync(int postId)
        {
            var post = await BuildPostGraphQuery()
                .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);

            if (post is null)
            {
                throw new NotFoundException("Post not found.");
            }

            return PostDtoMapper.ToPostDto(post);
        }

        public async Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var post = await _appDbContext.Posts
                .Include(p => p.PostTags)
                .ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
            {
                throw new NotFoundException("Post not found.");
            }

            post.Title = dto.Title;
            post.Content = dto.Content;
            post.MiniatureName = NormalizeRecipeField(dto.MiniatureName);
            post.PaintsUsed = NormalizeRecipeField(dto.PaintsUsed);
            post.Techniques = NormalizeRecipeField(dto.Techniques);
            post.Difficulty = NormalizeRecipeField(dto.Difficulty);
            post.TimeSpent = NormalizeRecipeField(dto.TimeSpent);
            post.UpdatedUtc = DateTime.UtcNow;

            await SyncTagsAsync(post, dto.Tags);
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<PostDto> CreateWithImagesAsync(string userId, CreateImagePostDto dto, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(dto);
            _postImageAttachmentService.ValidateCreateWithImages(dto, ct);

            var created = await CreateAsync(userId, new CreatePostDto
            {
                Title = dto.Title,
                Content = dto.Content,
                MiniatureName = dto.MiniatureName,
                PaintsUsed = dto.PaintsUsed,
                Techniques = dto.Techniques,
                Difficulty = dto.Difficulty,
                TimeSpent = dto.TimeSpent,
                Tags = dto.Tags,
                ProjectId = dto.ProjectId,
                MilestoneLabel = dto.MilestoneLabel
            });

            if (dto.Images is null || dto.Images.Count == 0)
            {
                return created;
            }

            created.Images = await _postImageAttachmentService.AttachUploadedImagesAsync(created.Id, dto, ct);
            created.ImageUrl = created.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault();
            return created;
        }

        public Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PostImageDto> images) =>
            _postImageAttachmentService.AddImagesAsync(postId, images);

        public Task<bool> ExistsAsync(int postId) =>
            _appDbContext.Posts.AnyAsync(post => post.Id == postId && !post.IsDeleted);

        private IQueryable<Post> ActivePosts() =>
            _appDbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

        private async Task<PagedResult<PostSummaryDto>> GetPagedPostsAsync(IQueryable<Post> query, int page, int pageSize)
        {
            PaginationGuard.Validate(page, pageSize);

            var ordered = query.OrderByDescending(p => p.CreatedUtc);
            var totalCount = await ordered.CountAsync();
            var pageItems = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostSummaryPageItem(
                    p.Id,
                    p.Comments.Count,
                    p.Likes.Count))
                .ToListAsync();

            if (pageItems.Count == 0)
            {
                return new PagedResult<PostSummaryDto>
                {
                    Items = Array.Empty<PostSummaryDto>(),
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize
                };
            }

            var pageIds = pageItems.Select(item => item.Id).ToList();
            var posts = await BuildPostGraphQuery()
                .Where(p => pageIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var items = pageItems
                .Where(item => posts.ContainsKey(item.Id))
                .Select(item => PostDtoMapper.ToPostSummaryDto(posts[item.Id], item.CommentCount, item.LikeCount))
                .ToList();

            return new PagedResult<PostSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        private async Task SyncTagsAsync(Post post, IEnumerable<string>? requestedTags)
        {
            var normalizedTags = NormalizeTags(requestedTags);

            if (post.PostTags.Count > 0)
            {
                var existingNormalized = normalizedTags
                    .Select(t => t.NormalizedName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var toRemove = post.PostTags
                    .Where(pt => !existingNormalized.Contains(pt.Tag.NormalizedName))
                    .ToList();

                if (toRemove.Count > 0)
                {
                    _appDbContext.PostTags.RemoveRange(toRemove);
                    foreach (var postTag in toRemove)
                    {
                        post.PostTags.Remove(postTag);
                    }
                }
            }

            if (normalizedTags.Count == 0)
            {
                return;
            }

            var tags = await ResolveTagsAsync(normalizedTags);
            var desiredNormalized = tags
                .Select(t => t.NormalizedName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingPostTags = post.PostTags
                .Select(pt => pt.Tag.NormalizedName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in tags.Where(t => !existingPostTags.Contains(t.NormalizedName)))
            {
                post.PostTags.Add(new PostTag
                {
                    Post = post,
                    Tag = tag
                });
            }
        }

        private async Task<List<Tag>> ResolveTagsAsync(IReadOnlyList<NormalizedTagRequest> normalizedTags)
        {
            if (normalizedTags.Count == 0)
            {
                return new List<Tag>();
            }

            var normalizedNames = normalizedTags.Select(t => t.NormalizedName).ToList();
            var existingTags = await _appDbContext.Tags
                .Where(t => normalizedNames.Contains(t.NormalizedName))
                .ToListAsync();
            var tagByNormalizedName = existingTags.ToDictionary(t => t.NormalizedName, StringComparer.OrdinalIgnoreCase);
            var usedSlugs = (await _appDbContext.Tags
                .AsNoTracking()
                .Select(t => t.Slug)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var normalizedTag in normalizedTags)
            {
                if (tagByNormalizedName.ContainsKey(normalizedTag.NormalizedName))
                {
                    continue;
                }

                var slug = TagTextUtilities.ResolveUniqueSlug(normalizedTag.Slug, usedSlugs);
                var tag = new Tag
                {
                    DisplayName = normalizedTag.DisplayName,
                    NormalizedName = normalizedTag.NormalizedName,
                    Slug = slug,
                    CreatedUtc = DateTime.UtcNow
                };

                _appDbContext.Tags.Add(tag);
                tagByNormalizedName[normalizedTag.NormalizedName] = tag;
                usedSlugs.Add(slug);
            }

            return normalizedTags
                .Select(t => tagByNormalizedName[t.NormalizedName])
                .ToList();
        }

        private static IReadOnlyList<NormalizedTagRequest> NormalizeTags(IEnumerable<string>? requestedTags)
        {
            if (requestedTags is null)
            {
                return Array.Empty<NormalizedTagRequest>();
            }

            var normalized = new List<NormalizedTagRequest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawTag in requestedTags)
            {
                if (string.IsNullOrWhiteSpace(rawTag))
                {
                    continue;
                }

                var displayName = TagTextUtilities.CollapseWhitespace(rawTag);
                if (displayName.Length > TagRules.MaxTagLength)
                {
                    throw new DomainValidationException("Invalid post tags.", new Dictionary<string, string[]>
                    {
                        ["Tags"] = new[] { $"Tags must be {TagRules.MaxTagLength} characters or fewer." }
                    });
                }

                var normalizedName = TagTextUtilities.NormalizeText(displayName);
                var slug = TagTextUtilities.CreateSlug(displayName);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    throw new DomainValidationException("Invalid post tags.", new Dictionary<string, string[]>
                    {
                        ["Tags"] = new[] { "Tags must include at least one letter or number." }
                    });
                }

                if (seen.Add(normalizedName))
                {
                    normalized.Add(new NormalizedTagRequest(displayName, normalizedName, slug));
                }
            }

            if (normalized.Count > TagRules.MaxTagsPerPost)
            {
                throw new DomainValidationException("Invalid post tags.", new Dictionary<string, string[]>
                {
                    ["Tags"] = new[] { $"A maximum of {TagRules.MaxTagsPerPost} tags is allowed." }
                });
            }

            return normalized;
        }

        private static void ValidateTopPostsQuery(int count, TimeSpan lookback)
        {
            var errors = new Dictionary<string, string[]>();
            var lookbackDays = lookback.TotalDays;

            if (count is < 1 or > 20)
            {
                errors["count"] = new[] { "Count must be between 1 and 20." };
            }

            if (lookbackDays < 1 || lookbackDays > 365)
            {
                errors["lookbackDays"] = new[] { "Lookback must be between 1 and 365 days." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Top posts query parameters are invalid.", errors);
            }
        }

        private static string? NormalizeRecipeField(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private IQueryable<Post> BuildPostGraphQuery() =>
            _appDbContext.Posts
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.CreatedBy)
                .ThenInclude(u => u.Profile)
                .Include(p => p.Images)
                .Include(p => p.PostTags)
                .ThenInclude(pt => pt.Tag)
                .Include(p => p.HobbyProjectEntry!)
                .ThenInclude(entry => entry.Project);

        private static bool IsLikelyUniqueConstraint(DbUpdateException exception)
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record PostSummaryPageItem(int Id, int CommentCount, int LikeCount);

    }
}
