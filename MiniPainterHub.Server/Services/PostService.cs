using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Imaging;
using MiniPainterHub.Server.Options;
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
        private const int MaxImagesPerPost = 5;
        private const long MaxUploadBytes = 20L * 1024 * 1024;
        private readonly AppDbContext _appDbContext;
        private readonly IImageService _imageService;
        private readonly IImageProcessor _imageProcessor;
        private readonly IImageStore _imageStore;
        private readonly ImagesOptions _imageOptions;
        private readonly ILogger<PostService> _logger;
        private readonly IAccountRestrictionService? _accountRestrictionService;

        public PostService(
            AppDbContext appDbContext,
            IImageService imageService,
            IImageProcessor imageProcessor,
            IImageStore imageStore,
            IOptions<ImagesOptions> imageOptions,
            ILogger<PostService> logger,
            IAccountRestrictionService? accountRestrictionService = null)
        {
            _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _imageOptions = imageOptions?.Value ?? throw new ArgumentNullException(nameof(imageOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accountRestrictionService = accountRestrictionService;
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
                Title = dto.Title,
                Content = dto.Content,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Images = new List<PostImage>()
            };

            if (dto.Images != null)
            {
                foreach (var img in dto.Images.Take(MaxImagesPerPost))
                {
                    newPost.Images.Add(new PostImage
                    {
                        ImageUrl = img.ImageUrl,
                        PreviewUrl = string.IsNullOrWhiteSpace(img.PreviewUrl) ? img.ImageUrl : img.PreviewUrl,
                        ThumbnailUrl = img.ThumbnailUrl
                    });
                }
            }

            _appDbContext.Posts.Add(newPost);
            await SyncTagsAsync(newPost, dto.Tags);
            await _appDbContext.SaveChangesAsync();

            return new PostDto
            {
                Id = newPost.Id,
                CreatedById = userId,
                Title = newPost.Title,
                Content = newPost.Content,
                CreatedAt = newPost.CreatedUtc,
                AuthorName = ResolveDisplayName(user.UserName, user.Profile?.DisplayName),
                ImageUrl = newPost.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                Images = newPost.Images.Select(i => new PostImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    PreviewUrl = i.PreviewUrl,
                    ThumbnailUrl = i.ThumbnailUrl
                }).ToList(),
                Tags = newPost.PostTags
                    .OrderBy(pt => pt.Tag.DisplayName)
                    .Select(pt => new TagDto
                    {
                        Name = pt.Tag.DisplayName,
                        Slug = pt.Tag.Slug
                    })
                    .ToList()
            };
        }

        public async Task<bool> DeleteAsync(int postId, string userId)
        {
            var post = await _appDbContext.Posts
                .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
            {
                throw new NotFoundException("Post not found.");
            }

            post.IsDeleted = true;
            post.SoftDeletedUtc = DateTime.UtcNow;
            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
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

        public async Task<PostDto> GetByIdAsync(int postId)
        {
            var post = await BuildPostGraphQuery()
                .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);

            if (post is null)
            {
                throw new NotFoundException("Post not found.");
            }

            return MapPostDto(post);
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
            post.UpdatedUtc = DateTime.UtcNow;

            await SyncTagsAsync(post, dto.Tags);
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<PostDto> CreateWithImagesAsync(string userId, CreateImagePostDto dto, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ct.ThrowIfCancellationRequested();

            if (dto.Images != null && dto.Images.Count > MaxImagesPerPost)
            {
                throw new DomainValidationException("Invalid post images.", new Dictionary<string, string[]>
                {
                    ["Images"] = new[] { $"A maximum of {MaxImagesPerPost} images is allowed." }
                });
            }

            ValidateIncomingImages(dto.Images, dto.Thumbnails);

            var created = await CreateAsync(userId, new CreatePostDto
            {
                Title = dto.Title,
                Content = dto.Content,
                Tags = dto.Tags
            });

            if (dto.Images is null || dto.Images.Count == 0)
            {
                return created;
            }

            _logger.LogInformation(
                "Processing {Count} uploaded images for post {PostId} (pipeline enabled: {Enabled})",
                Math.Min(dto.Images.Count, MaxImagesPerPost),
                created.Id,
                _imageOptions.Enabled);

            var processedImages = new List<ProcessedImageResult>();
            try
            {
                if (_imageOptions.Enabled)
                {
                    await ProcessWithPipelineAsync(created.Id, dto.Images, processedImages, ct);
                }
                else
                {
                    await ProcessWithLegacyAsync(created.Id, dto.Images, dto.Thumbnails, processedImages, ct);
                }

                created.Images = await AddImagesAsync(
                    created.Id,
                    processedImages
                        .Where(result => result.Image is not null)
                        .Select(result => result.Image!));
                created.ImageUrl = created.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault();

                return created;
            }
            catch
            {
                await CleanupFailedCreateWithImagesAsync(created.Id, processedImages);
                throw;
            }
        }

        public async Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PostImageDto> images)
        {
            var post = await _appDbContext.Posts
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
                ?? throw new NotFoundException("Post not found.");

            var incoming = images ?? Enumerable.Empty<PostImageDto>();
            var remainingSlots = Math.Max(0, MaxImagesPerPost - post.Images.Count);
            var toAdd = incoming
                .Take(remainingSlots)
                .Select(img => new PostImage
                {
                    PostId = postId,
                    ImageUrl = img.ImageUrl,
                    PreviewUrl = string.IsNullOrWhiteSpace(img.PreviewUrl) ? img.ImageUrl : img.PreviewUrl,
                    ThumbnailUrl = img.ThumbnailUrl
                })
                .ToList();

            foreach (var entity in toAdd)
            {
                post.Images.Add(entity);
            }

            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();

            return await _appDbContext.PostImages
                .Where(i => i.PostId == postId)
                .OrderBy(i => i.Id)
                .Select(i => new PostImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    PreviewUrl = i.PreviewUrl,
                    ThumbnailUrl = i.ThumbnailUrl
                })
                .ToListAsync();
        }

        public Task<bool> ExistsAsync(int postId) =>
            _appDbContext.Posts.AnyAsync(post => post.Id == postId && !post.IsDeleted);

        private IQueryable<Post> ActivePosts() =>
            _appDbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

        private async Task<PagedResult<PostSummaryDto>> GetPagedPostsAsync(IQueryable<Post> query, int page, int pageSize)
        {
            ValidatePaging(page, pageSize);

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
                .Select(item => MapPostSummaryDto(posts[item.Id], item.CommentCount, item.LikeCount))
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

                var slug = ResolveUniqueSlug(normalizedTag.Slug, usedSlugs);
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

        private static string ResolveUniqueSlug(string baseSlug, ISet<string> usedSlugs)
        {
            var candidate = baseSlug;
            var suffix = 2;
            while (usedSlugs.Contains(candidate))
            {
                candidate = $"{baseSlug}-{suffix}";
                suffix++;
            }

            return candidate;
        }

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

        private void ValidateIncomingImages(
            IReadOnlyList<IFormFile>? images,
            IReadOnlyList<IFormFile>? thumbnails)
        {
            if (images is null)
            {
                return;
            }

            for (var i = 0; i < images.Count && i < MaxImagesPerPost; i++)
            {
                var image = images[i];
                if (image.Length > MaxUploadBytes)
                {
                    throw new ImageTooLargeException(image.FileName, image.Length, MaxUploadBytes);
                }

                if (_imageOptions.Enabled)
                {
                    var contentType = ResolveContentType(image);
                    if (!ImageContentTypes.IsAllowed(contentType))
                    {
                        throw new UnsupportedImageContentTypeException(image.FileName, contentType);
                    }
                }

                if (_imageOptions.Enabled || thumbnails is null || i >= thumbnails.Count || thumbnails[i] is not { Length: > 0 } thumb)
                {
                    continue;
                }

                if (thumb.Length > MaxUploadBytes)
                {
                    throw new ImageTooLargeException(thumb.FileName, thumb.Length, MaxUploadBytes);
                }
            }
        }

        private async Task ProcessWithLegacyAsync(
            int postId,
            IReadOnlyList<IFormFile> images,
            IReadOnlyList<IFormFile>? thumbnails,
            List<ProcessedImageResult> results,
            CancellationToken ct)
        {
            for (var i = 0; i < images.Count && i < MaxImagesPerPost; i++)
            {
                ct.ThrowIfCancellationRequested();

                var image = images[i];
                var uploadedFiles = new List<string>();
                try
                {
                    await using var stream = image.OpenReadStream();
                    var fileName = $"{postId}_{i}_{image.FileName}";
                    var url = await _imageService.UploadAsync(stream, fileName);
                    uploadedFiles.Add(fileName);

                    string? thumbUrl = null;
                    if (thumbnails != null && i < thumbnails.Count && thumbnails[i] is { Length: > 0 } thumb)
                    {
                        await using var thumbStream = thumb.OpenReadStream();
                        var thumbFileName = $"{postId}_{i}_thumb_{thumb.FileName}";
                        thumbUrl = await _imageService.UploadAsync(thumbStream, thumbFileName);
                        uploadedFiles.Add(thumbFileName);
                    }

                    results.Add(new ProcessedImageResult(
                        new PostImageDto
                        {
                            ImageUrl = url,
                            PreviewUrl = url,
                            ThumbnailUrl = thumbUrl
                        },
                        null,
                        uploadedFiles));
                }
                catch
                {
                    results.Add(new ProcessedImageResult(null, null, uploadedFiles));
                    throw;
                }
            }
        }

        private async Task ProcessWithPipelineAsync(
            int postId,
            IReadOnlyList<IFormFile> images,
            List<ProcessedImageResult> results,
            CancellationToken ct)
        {
            for (var i = 0; i < images.Count && i < MaxImagesPerPost; i++)
            {
                ct.ThrowIfCancellationRequested();

                var image = images[i];
                var contentType = ResolveContentType(image);
                var imageId = Guid.NewGuid();
                try
                {
                    await using var stream = image.OpenReadStream();
                    var variants = await _imageProcessor.ProcessAsync(stream, contentType, ct);
                    var stored = await _imageStore.SaveAsync(ConvertToStorageGuid(postId), imageId, variants, ct);

                    results.Add(new ProcessedImageResult(
                        new PostImageDto
                        {
                            ImageUrl = stored.MaxUrl,
                            PreviewUrl = stored.PreviewUrl,
                            ThumbnailUrl = stored.ThumbUrl
                        },
                        imageId,
                        Array.Empty<string>()));
                }
                catch
                {
                    results.Add(new ProcessedImageResult(null, imageId, Array.Empty<string>()));
                    throw;
                }
            }
        }

        private async Task CleanupFailedCreateWithImagesAsync(int postId, IReadOnlyList<ProcessedImageResult> processedImages)
        {
            var storagePostId = ConvertToStorageGuid(postId);

            foreach (var processedImage in processedImages)
            {
                foreach (var fileName in processedImage.UploadedFileNames)
                {
                    try
                    {
                        await _imageService.DeleteAsync(fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete uploaded image artifact {FileName} during rollback for post {PostId}", fileName, postId);
                    }
                }

                if (!processedImage.StoredImageId.HasValue)
                {
                    continue;
                }

                try
                {
                    await _imageStore.DeleteAsync(storagePostId, processedImage.StoredImageId.Value, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete stored image variants for post {PostId} image {ImageId} during rollback", postId, processedImage.StoredImageId.Value);
                }
            }

            try
            {
                var post = await _appDbContext.Posts
                    .Include(p => p.Images)
                    .Include(p => p.PostTags)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post is null)
                {
                    return;
                }

                if (post.Images.Count > 0)
                {
                    _appDbContext.PostImages.RemoveRange(post.Images);
                }

                if (post.PostTags.Count > 0)
                {
                    _appDbContext.PostTags.RemoveRange(post.PostTags);
                }

                _appDbContext.Posts.Remove(post);
                await _appDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove post {PostId} during image upload rollback", postId);
            }
        }

        private static Guid ConvertToStorageGuid(int postId) =>
            new(postId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        private static string ResolveContentType(IFormFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                return file.ContentType;
            }

            if (file.Headers?.TryGetValue("Content-Type", out StringValues headerValue) == true
                && !StringValues.IsNullOrEmpty(headerValue))
            {
                return headerValue.ToString();
            }

            return string.Empty;
        }

        private IQueryable<Post> BuildPostGraphQuery() =>
            _appDbContext.Posts
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.CreatedBy)
                .ThenInclude(u => u.Profile)
                .Include(p => p.Images)
                .Include(p => p.PostTags)
                .ThenInclude(pt => pt.Tag);

        private static PostDto MapPostDto(Post post) =>
            new()
            {
                Id = post.Id,
                CreatedById = post.CreatedById,
                Title = post.Title,
                Content = post.Content,
                CreatedAt = post.CreatedUtc,
                AuthorName = ResolveDisplayName(post.CreatedBy?.UserName, post.CreatedBy?.Profile?.DisplayName),
                ImageUrl = post.Images
                    .OrderBy(i => i.Id)
                    .Where(i => !string.IsNullOrEmpty(i.ImageUrl))
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                Images = post.Images
                    .OrderBy(i => i.Id)
                    .Select(i => new PostImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        PreviewUrl = i.PreviewUrl,
                        ThumbnailUrl = i.ThumbnailUrl
                    })
                    .ToList(),
                Tags = MapTags(post.PostTags)
            };

        private static PostSummaryDto MapPostSummaryDto(Post post, int commentCount, int likeCount) =>
            new()
            {
                Id = post.Id,
                Title = post.Title,
                Snippet = post.Content.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content,
                ImageUrl = post.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                AuthorName = ResolveDisplayName(post.CreatedBy?.UserName, post.CreatedBy?.Profile?.DisplayName),
                AuthorId = post.CreatedById,
                CreatedAt = post.CreatedUtc,
                CommentCount = commentCount,
                LikeCount = likeCount,
                IsDeleted = post.IsDeleted,
                Tags = MapTags(post.PostTags)
            };

        private static List<TagDto> MapTags(IEnumerable<PostTag> postTags) =>
            postTags
                .OrderBy(pt => pt.Tag.DisplayName)
                .Select(pt => new TagDto
                {
                    Name = pt.Tag.DisplayName,
                    Slug = pt.Tag.Slug
                })
                .ToList();

        private static string ResolveDisplayName(string? userName, string? profileDisplayName) =>
            string.IsNullOrWhiteSpace(profileDisplayName) ? (userName ?? string.Empty) : profileDisplayName;

        private sealed record NormalizedTagRequest(string DisplayName, string NormalizedName, string Slug);

        private sealed record PostSummaryPageItem(int Id, int CommentCount, int LikeCount);

        private sealed record ProcessedImageResult(PostImageDto? Image, Guid? StoredImageId, IReadOnlyList<string> UploadedFileNames);
    }
}
