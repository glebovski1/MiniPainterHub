using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class PostService : IPostService
    {
        private const int MaxImagesPerPost = 5;
        private AppDbContext _appDbContext;
        public PostService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;

        }
        public async Task<PostDto> CreateAsync(string userId, CreatePostDto dto)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            }

            var user = await _appDbContext.Users.FindAsync(userId);
            if (user is null)
            {
                throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            }
            // 1️⃣ Create the entity and set the FK
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
                        ThumbnailUrl = img.ThumbnailUrl
                    });
                }
            }

            // 2️⃣ Add and save
            _appDbContext.Posts.Add(newPost);
            await _appDbContext.SaveChangesAsync();

            // 3️⃣ Map to DTO and return
            return new PostDto
            {
                Id = newPost.Id,
                CreatedById = userId,
                Title = newPost.Title,
                Content = newPost.Content,
                CreatedAt = newPost.CreatedUtc,
                AuthorName = user.UserName,
                ImageUrl = newPost.Images
                                    .OrderBy(i => i.Id)
                                    .Select(i => i.ImageUrl)
                                    .FirstOrDefault(),
                Images = newPost.Images.Select(i => new PostImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    ThumbnailUrl = i.ThumbnailUrl
                }).ToList()
            };
        }

        public async Task<bool> DeleteAsync(int postId, string userId)
        {
            // find the post only if it belongs to this user
            var post = await _appDbContext.Posts
                .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
                throw new NotFoundException("Post not found.");

            post.IsDeleted = true;
            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize)
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

            var query = _appDbContext.Posts
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedUtc);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostSummaryDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Snippet = p.Content.Length > 100
                                     ? p.Content.Substring(0, 100) + "…"
                                     : p.Content,
                    ImageUrl = p.Images
                                 .OrderBy(i => i.Id)
                                 .Select(i => i.ImageUrl)
                                 .FirstOrDefault(),
                    AuthorName = p.CreatedBy.UserName,     // or p.CreatedBy.Profile.DisplayName
                    AuthorId = p.CreatedById,
                    CreatedAt = p.CreatedUtc,
                    CommentCount = p.Comments.Count,
                    LikeCount = p.Likes.Count
                })
                .ToListAsync();

            return new PagedResult<PostSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<PostDto> GetByIdAsync(int postId)
        {
              var dto = await _appDbContext.Posts
                     .AsNoTracking()
                     .Where(p => p.Id == postId && !p.IsDeleted)
                     .Select(p => new PostDto
                     {
                         Id = p.Id,
                         CreatedById = p.CreatedById,
                         Title = p.Title,
                         Content = p.Content,
                         CreatedAt = p.CreatedUtc,
                         AuthorName = p.CreatedBy.UserName,
                         ImageUrl = p.Images
                                        .OrderBy(i => i.Id)
                                        .Select(i => i.ImageUrl)
                                        .FirstOrDefault(),
                         Images = p.Images.Select(i => new PostImageDto
                         {
                             Id = i.Id,
                             ImageUrl = i.ImageUrl,
                             ThumbnailUrl = i.ThumbnailUrl
                         }).ToList()
                     })
                     .FirstOrDefaultAsync();
            if (dto is null)
            {
                throw new NotFoundException("Post not found.");
            }

            return dto;
        }

        public async Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto)
        {
            var post = await _appDbContext.Posts
               .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
                throw new NotFoundException("Post not found.");

            post.Title = dto.Title;
            post.Content = dto.Content;
            post.UpdatedUtc = DateTime.UtcNow;

            await _appDbContext.SaveChangesAsync();
            return true;
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
                    ThumbnailUrl = i.ThumbnailUrl
                })
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(int postId)
        {
            return await _appDbContext.Posts.AnyAsync(post => post.Id == postId && !post.IsDeleted);
        }
    }
}
