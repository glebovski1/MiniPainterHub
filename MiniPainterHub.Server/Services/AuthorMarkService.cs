using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class AuthorMarkService : IAuthorMarkService
    {
        private readonly AppDbContext _appDbContext;

        public AuthorMarkService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<AuthorMarkDto> CreateAsync(int postId, int postImageId, string userId, CreateAuthorMarkDto dto)
        {
            var postImage = await GetAuthorizedPostImageAsync(postId, postImageId, userId);
            var (normalizedX, normalizedY) = ViewerMarkValidation.NormalizeCoordinates(dto.NormalizedX, dto.NormalizedY);
            var (tag, message) = ViewerMarkValidation.NormalizeAuthorContent(dto.Tag, dto.Message);

            var mark = new ImageAuthorMark
            {
                PostImageId = postImage.Id,
                CreatedByUserId = userId,
                NormalizedX = normalizedX,
                NormalizedY = normalizedY,
                Tag = tag,
                Message = message,
                CreatedUtc = System.DateTime.UtcNow,
                UpdatedUtc = System.DateTime.UtcNow
            };

            _appDbContext.ImageAuthorMarks.Add(mark);
            await _appDbContext.SaveChangesAsync();

            return Map(mark);
        }

        public async Task<AuthorMarkDto> UpdateAsync(int markId, string userId, UpdateAuthorMarkDto dto)
        {
            var mark = await _appDbContext.ImageAuthorMarks
                .Include(m => m.PostImage)
                .ThenInclude(i => i.Post)
                .FirstOrDefaultAsync(m => m.Id == markId && !m.PostImage.Post.IsDeleted);

            if (mark is null || mark.PostImage.Post.CreatedById != userId)
            {
                throw new NotFoundException("Author mark not found.");
            }

            var (normalizedX, normalizedY) = ViewerMarkValidation.NormalizeCoordinates(dto.NormalizedX, dto.NormalizedY);
            var (tag, message) = ViewerMarkValidation.NormalizeAuthorContent(dto.Tag, dto.Message);

            mark.NormalizedX = normalizedX;
            mark.NormalizedY = normalizedY;
            mark.Tag = tag;
            mark.Message = message;
            mark.UpdatedUtc = System.DateTime.UtcNow;

            await _appDbContext.SaveChangesAsync();
            return Map(mark);
        }

        public async Task DeleteAsync(int markId, string userId)
        {
            var mark = await _appDbContext.ImageAuthorMarks
                .Include(m => m.PostImage)
                .ThenInclude(i => i.Post)
                .FirstOrDefaultAsync(m => m.Id == markId && !m.PostImage.Post.IsDeleted);

            if (mark is null || mark.PostImage.Post.CreatedById != userId)
            {
                throw new NotFoundException("Author mark not found.");
            }

            _appDbContext.ImageAuthorMarks.Remove(mark);
            await _appDbContext.SaveChangesAsync();
        }

        private async Task<PostImage> GetAuthorizedPostImageAsync(int postId, int postImageId, string userId)
        {
            var postImage = await _appDbContext.PostImages
                .Include(i => i.Post)
                .FirstOrDefaultAsync(i => i.Id == postImageId && i.PostId == postId && !i.Post.IsDeleted);

            if (postImage is null)
            {
                throw new DomainValidationException("Viewer mark data is invalid.", new Dictionary<string, string[]>
                {
                    ["PostImageId"] = new[] { "Image was not found for this post." }
                });
            }

            if (postImage.Post.CreatedById != userId)
            {
                throw new NotFoundException("Author mark not found.");
            }

            return postImage;
        }

        private static AuthorMarkDto Map(ImageAuthorMark mark) =>
            new()
            {
                Id = mark.Id,
                PostImageId = mark.PostImageId,
                NormalizedX = mark.NormalizedX,
                NormalizedY = mark.NormalizedY,
                Tag = mark.Tag,
                Message = mark.Message
            };
    }
}
