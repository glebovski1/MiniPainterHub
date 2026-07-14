using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Media;
using MiniPainterHub.Server.Imaging;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Features.Posts;

public sealed class PostImageAttachmentService : IPostImageAttachmentService
{
    private readonly AppDbContext _appDbContext;
    private readonly IImageService _imageService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IImageStore _imageStore;
    private readonly IUploadConcurrencyLimiter _uploadConcurrencyLimiter;
    private readonly ImagesOptions _imageOptions;
    private readonly ILogger<PostImageAttachmentService> _logger;
    private readonly IHobbyProjectPostLinker? _hobbyProjectPostLinker;

    public PostImageAttachmentService(
        AppDbContext appDbContext,
        IImageService imageService,
        IImageProcessor imageProcessor,
        IImageStore imageStore,
        IOptions<ImagesOptions> imageOptions,
        ILogger<PostImageAttachmentService> logger,
        IHobbyProjectPostLinker? hobbyProjectPostLinker = null)
        : this(
            appDbContext,
            imageService,
            imageProcessor,
            imageStore,
            NoopUploadConcurrencyLimiter.Instance,
            imageOptions,
            logger,
            hobbyProjectPostLinker)
    {
    }

    public PostImageAttachmentService(
        AppDbContext appDbContext,
        IImageService imageService,
        IImageProcessor imageProcessor,
        IImageStore imageStore,
        IUploadConcurrencyLimiter uploadConcurrencyLimiter,
        IOptions<ImagesOptions> imageOptions,
        ILogger<PostImageAttachmentService> logger,
        IHobbyProjectPostLinker? hobbyProjectPostLinker = null)
    {
        _appDbContext = appDbContext ?? throw new ArgumentNullException(nameof(appDbContext));
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        _uploadConcurrencyLimiter = uploadConcurrencyLimiter ?? throw new ArgumentNullException(nameof(uploadConcurrencyLimiter));
        _imageOptions = imageOptions?.Value ?? throw new ArgumentNullException(nameof(imageOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hobbyProjectPostLinker = hobbyProjectPostLinker;
    }

    public void ValidateCreateWithImages(CreateImagePostDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        cancellationToken.ThrowIfCancellationRequested();

        if (dto.Images != null && dto.Images.Count > PostImageUploadValidator.MaxImagesPerPost)
        {
            throw new DomainValidationException("Invalid post images.", new Dictionary<string, string[]>
            {
                ["Images"] = new[] { $"A maximum of {PostImageUploadValidator.MaxImagesPerPost} images is allowed." }
            });
        }

        PostImageUploadValidator.Validate(dto.Images, dto.Thumbnails, _imageOptions);
    }

    public async Task<List<PostImageDto>> AttachUploadedImagesAsync(int postId, CreateImagePostDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        cancellationToken.ThrowIfCancellationRequested();

        if (dto.Images is null || dto.Images.Count == 0)
        {
            return new List<PostImageDto>();
        }

        var imageCount = Math.Min(dto.Images.Count, PostImageUploadValidator.MaxImagesPerPost);
        var totalBytes = dto.Images.Take(PostImageUploadValidator.MaxImagesPerPost).Sum(image => image.Length);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing {Count} uploaded images for post {PostId}. TotalBytes={TotalBytes}; PipelineEnabled={Enabled}",
            imageCount,
            postId,
            totalBytes,
            _imageOptions.Enabled);

        var processedImages = new List<ProcessedImageResult>();
        try
        {
            await using var uploadPermit = await _uploadConcurrencyLimiter.TryAcquireAsync(cancellationToken)
                ?? throw new UploadConcurrencyLimitExceededException();

            if (_imageOptions.Enabled)
            {
                await ProcessWithPipelineAsync(postId, dto.Images, processedImages, cancellationToken);
            }
            else
            {
                await ProcessWithLegacyAsync(postId, dto.Images, dto.Thumbnails, processedImages, cancellationToken);
            }

            return await AddProcessedImagesAsync(
                postId,
                processedImages
                    .Where(result => result.Image is not null)
                    .Select(result => result));
        }
        catch
        {
            _logger.LogWarning(
                "Image upload processing failed for post {PostId}. Count={Count}; TotalBytes={TotalBytes}; ElapsedMs={ElapsedMs}",
                postId,
                imageCount,
                totalBytes,
                stopwatch.ElapsedMilliseconds);
            await CleanupFailedCreateWithImagesAsync(postId, processedImages);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Image upload processing finished for post {PostId}. Count={Count}; TotalBytes={TotalBytes}; ElapsedMs={ElapsedMs}",
                postId,
                imageCount,
                totalBytes,
                stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PostImageDto> images)
    {
        var pendingImages = (images ?? Enumerable.Empty<PostImageDto>())
            .Select(image => new PendingPostImage(image, null, null, null));

        return await AddImagesAsync(postId, pendingImages);
    }

    private async Task<List<PostImageDto>> AddProcessedImagesAsync(int postId, IEnumerable<ProcessedImageResult> images)
    {
        var pendingImages = images
            .Where(result => result.Image is not null)
            .Select(result => new PendingPostImage(
                result.Image!,
                result.StoredImageId,
                result.ImageStorageKey,
                result.ThumbnailStorageKey));

        return await AddImagesAsync(postId, pendingImages);
    }

    private async Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PendingPostImage> images)
    {
        var post = await _appDbContext.Posts
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
            ?? throw new NotFoundException("Post not found.");

        var incoming = images ?? Enumerable.Empty<PendingPostImage>();
        var remainingSlots = Math.Max(0, PostImageUploadValidator.MaxImagesPerPost - post.Images.Count);
        var toAdd = incoming
            .Take(remainingSlots)
            .Select(pending => new PostImage
            {
                PostId = postId,
                ImageUrl = pending.Image.ImageUrl,
                PreviewUrl = string.IsNullOrWhiteSpace(pending.Image.PreviewUrl) ? pending.Image.ImageUrl : pending.Image.PreviewUrl,
                ThumbnailUrl = pending.Image.ThumbnailUrl,
                Width = pending.Image.Width,
                Height = pending.Image.Height,
                StoredImageId = pending.StoredImageId,
                ImageStorageKey = pending.ImageStorageKey,
                ThumbnailStorageKey = pending.ThumbnailStorageKey
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
                ThumbnailUrl = i.ThumbnailUrl,
                Width = i.Width,
                Height = i.Height
            })
            .ToListAsync();
    }

    public async Task CleanupDeletedPostImagesAsync(int postId, IEnumerable<PostImage> images)
    {
        var cleanupTargets = images
            .Select(image => new PostImageCleanupTarget(
                image.StoredImageId,
                image.ImageStorageKey,
                image.ThumbnailStorageKey))
            .ToList();

        if (cleanupTargets.Count == 0)
        {
            return;
        }

        var storagePostId = ConvertToStorageGuid(postId);

        foreach (var image in cleanupTargets)
        {
            foreach (var key in image.LegacyStorageKeys)
            {
                try
                {
                    await _imageService.DeleteAsync(key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete uploaded image artifact {FileName} for deleted post {PostId}", key, postId);
                }
            }

            if (!image.StoredImageId.HasValue)
            {
                continue;
            }

            try
            {
                await _imageStore.DeleteAsync(storagePostId, image.StoredImageId.Value, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete stored image variants for deleted post {PostId} image {ImageId}", postId, image.StoredImageId.Value);
            }
        }
    }

    private async Task ProcessWithLegacyAsync(
        int postId,
        IReadOnlyList<IFormFile> images,
        IReadOnlyList<IFormFile>? thumbnails,
        List<ProcessedImageResult> results,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < images.Count && i < PostImageUploadValidator.MaxImagesPerPost; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var image = images[i];
            var uploadedFiles = new List<string>();
            try
            {
                await using var stream = image.OpenReadStream();
                var contentType = PostImageUploadValidator.ResolveContentType(image);
                var fileName = PostImageStorageKeys.CreateImageKey(postId, i, contentType);
                var url = await _imageService.UploadAsync(stream, fileName);
                uploadedFiles.Add(fileName);

                string? thumbUrl = null;
                string? thumbFileName = null;
                if (thumbnails != null && i < thumbnails.Count && thumbnails[i] is { Length: > 0 } thumb)
                {
                    await using var thumbStream = thumb.OpenReadStream();
                    var thumbnailContentType = PostImageUploadValidator.ResolveContentType(thumb);
                    thumbFileName = PostImageStorageKeys.CreateThumbnailKey(postId, i, thumbnailContentType);
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
                    fileName,
                    thumbFileName,
                    uploadedFiles));
            }
            catch
            {
                results.Add(new ProcessedImageResult(null, null, null, null, uploadedFiles));
                throw;
            }
        }
    }

    private async Task ProcessWithPipelineAsync(
        int postId,
        IReadOnlyList<IFormFile> images,
        List<ProcessedImageResult> results,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < images.Count && i < PostImageUploadValidator.MaxImagesPerPost; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var image = images[i];
            var contentType = PostImageUploadValidator.ResolveContentType(image);
            var imageId = Guid.NewGuid();
            try
            {
                await using var stream = image.OpenReadStream();
                var variants = await _imageProcessor.ProcessAsync(stream, contentType, cancellationToken);
                var stored = await _imageStore.SaveAsync(ConvertToStorageGuid(postId), imageId, variants, cancellationToken);

                results.Add(new ProcessedImageResult(
                    new PostImageDto
                    {
                        ImageUrl = stored.MaxUrl,
                        PreviewUrl = stored.PreviewUrl,
                        ThumbnailUrl = stored.ThumbUrl,
                        Width = variants.Max.Width,
                        Height = variants.Max.Height
                    },
                    imageId,
                    null,
                    null,
                    Array.Empty<string>()));
            }
            catch
            {
                results.Add(new ProcessedImageResult(null, imageId, null, null, Array.Empty<string>()));
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
                .Include(p => p.HobbyProjectEntry)
                .ThenInclude(entry => entry!.Project)
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

            if (post.HobbyProjectEntry is not null)
            {
                if (_hobbyProjectPostLinker is not null)
                {
                    await _hobbyProjectPostLinker.RollbackNewPostAsync(post);
                }

                _appDbContext.HobbyProjectEntries.Remove(post.HobbyProjectEntry);
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

    private sealed record PendingPostImage(
        PostImageDto Image,
        Guid? StoredImageId,
        string? ImageStorageKey,
        string? ThumbnailStorageKey);

    private sealed record ProcessedImageResult(
        PostImageDto? Image,
        Guid? StoredImageId,
        string? ImageStorageKey,
        string? ThumbnailStorageKey,
        IReadOnlyList<string> UploadedFileNames);

    private sealed record PostImageCleanupTarget(
        Guid? StoredImageId,
        string? ImageStorageKey,
        string? ThumbnailStorageKey)
    {
        public IEnumerable<string> LegacyStorageKeys
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ImageStorageKey))
                {
                    yield return ImageStorageKey;
                }

                if (!string.IsNullOrWhiteSpace(ThumbnailStorageKey)
                    && !string.Equals(ImageStorageKey, ThumbnailStorageKey, StringComparison.Ordinal))
                {
                    yield return ThumbnailStorageKey;
                }
            }
        }
    }

    private sealed class NoopUploadConcurrencyLimiter : IUploadConcurrencyLimiter
    {
        public static readonly NoopUploadConcurrencyLimiter Instance = new();

        public ValueTask<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IAsyncDisposable?>(NoopPermit.Instance);
    }

    private sealed class NoopPermit : IAsyncDisposable
    {
        public static readonly NoopPermit Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
