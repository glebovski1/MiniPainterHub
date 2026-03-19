using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
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

            return new PostViewerDto
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
                        PreviewUrl = i.PreviewUrl,
                        ThumbnailUrl = i.ThumbnailUrl,
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
        }

        private static string ResolveDisplayName(string? userName, string? profileDisplayName) =>
            string.IsNullOrWhiteSpace(profileDisplayName) ? (userName ?? string.Empty) : profileDisplayName;
    }
}
