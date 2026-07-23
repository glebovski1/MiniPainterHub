using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Posts;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class PostViewerService : IPostViewerService
    {
        private readonly AppDbContext _appDbContext;

        public PostViewerService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<PostViewerDto> GetAsync(int postId, string? currentUserId)
        {
            var post = await _appDbContext.Posts
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.CreatedBy)
                .ThenInclude(u => u.Profile)
                .Include(p => p.Images)
                .ThenInclude(i => i.AuthorMarks)
                .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted);

            if (post is null)
            {
                throw new NotFoundException("Post not found.");
            }

            return MapViewer(post, currentUserId);
        }

        public async Task<PostExperienceDto> GetExperienceAsync(int postId, string? currentUserId)
        {
            var post = await _appDbContext.Posts
                .AsNoTracking()
                .AsSplitQuery()
                .Include(item => item.CreatedBy)
                    .ThenInclude(user => user.Profile)
                .Include(item => item.Images)
                    .ThenInclude(image => image.AuthorMarks)
                .Include(item => item.PostTags)
                    .ThenInclude(postTag => postTag.Tag)
                .Include(item => item.HobbyProjectEntry!)
                    .ThenInclude(entry => entry.Project)
                .FirstOrDefaultAsync(item => item.Id == postId && !item.IsDeleted);

            if (post is null)
            {
                throw new NotFoundException("Post not found.");
            }

            var likes = await _appDbContext.Posts
                .AsNoTracking()
                .Where(item => item.Id == postId && !item.IsDeleted)
                .Select(item => new LikeDto
                {
                    PostId = item.Id,
                    Count = item.Likes.Count,
                    UserHasLiked = currentUserId != null && item.Likes.Any(like => like.UserId == currentUserId)
                })
                .FirstAsync();

            return new PostExperienceDto
            {
                Post = PostDtoMapper.ToPostDto(post),
                Viewer = MapViewer(post, currentUserId),
                Likes = likes
            };
        }

        private static PostViewerDto MapViewer(Entities.Post post, string? currentUserId) =>
            new()
            {
                PostId = post.Id,
                Title = post.Title,
                CreatedById = post.CreatedById,
                AuthorName = ResolveDisplayName(post.CreatedBy?.UserName, post.CreatedBy?.Profile?.DisplayName),
                CreatedAt = post.CreatedUtc,
                CanManageAuthorMarks = string.Equals(post.CreatedById, currentUserId, StringComparison.Ordinal),
                CanAttachCommentMark = !string.IsNullOrWhiteSpace(currentUserId),
                Images = post.Images
                    .OrderBy(i => i.Id)
                    .Select(i => new PostViewerImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        PreviewUrl = ResolveVariantUrl(i.ImageUrl, i.PreviewUrl, "preview"),
                        ThumbnailUrl = ResolveVariantUrl(i.ImageUrl, i.ThumbnailUrl, "thumbnail"),
                        Width = i.Width,
                        Height = i.Height
                    })
                    .ToList(),
                AuthorMarks = post.Images
                    .SelectMany(i => i.AuthorMarks)
                    .OrderBy(m => m.Id)
                    .Select(m => new AuthorMarkDto
                    {
                        Id = m.Id,
                        PostImageId = m.PostImageId,
                        NormalizedX = m.NormalizedX,
                        NormalizedY = m.NormalizedY,
                        Tag = m.Tag,
                        Message = m.Message
                    })
                    .ToList()
            };

        private static string ResolveDisplayName(string? userName, string? profileDisplayName) =>
            string.IsNullOrWhiteSpace(profileDisplayName) ? (userName ?? string.Empty) : profileDisplayName;

        private static string? ResolveVariantUrl(string imageUrl, string? variantUrl, string variantName)
        {
            if (IsUsableVariantUrl(variantUrl, imageUrl))
            {
                return variantUrl;
            }

            return BuildVariantEndpointUrl(imageUrl, variantName);
        }

        private static bool IsUsableVariantUrl(string? candidateUrl, string fullImageUrl) =>
            !string.IsNullOrWhiteSpace(candidateUrl)
            && !string.Equals(candidateUrl, fullImageUrl, StringComparison.OrdinalIgnoreCase);

        private static string? BuildVariantEndpointUrl(string? imageUrl, string variantName)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            var path = imageUrl.Trim();
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }

            return path.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase)
                ? $"/api/images/{variantName}?url={Uri.EscapeDataString(path)}"
                : null;
        }
    }
}
