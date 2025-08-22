using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class PostService : IPostService
    {
        private AppDbContext _appDbContext;
        public PostService(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;

        }
        public async Task<PostDto> CreateAsync(string userId, CreatePostDto dto)
        {
            var user = await _appDbContext.Users.FindAsync(userId);
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
                foreach (var img in dto.Images.Take(5))
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
                return false;

            post.IsDeleted = true;
            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<PostSummaryDto>> GetAllAsync(int page, int pageSize)
        {
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
            return dto;
        }

        public async Task<bool> UpdateAsync(int postId, string userId, UpdatePostDto dto)
        {
            var post = await _appDbContext.Posts
               .FirstOrDefaultAsync(p => p.Id == postId && p.CreatedById == userId && !p.IsDeleted);

            if (post == null)
                return false;

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
                        ?? throw new KeyNotFoundException("Post not found");

            var existingCount = post.Images.Count;
            foreach (var img in images.Take(5 - existingCount))
            {
                var entity = new PostImage
                {
                    PostId = postId,
                    ImageUrl = img.ImageUrl,
                    ThumbnailUrl = img.ThumbnailUrl
                };
                _appDbContext.PostImages.Add(entity);
                post.Images.Add(entity);
            }

            post.UpdatedUtc = DateTime.UtcNow;
            await _appDbContext.SaveChangesAsync();

            return post.Images.Select(i => new PostImageDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                ThumbnailUrl = i.ThumbnailUrl
            }).ToList();
        }

        public async Task<bool> ExistsAsync(int postId)
        {
            return await _appDbContext.Posts.AnyAsync(post => post.Id == postId && !post.IsDeleted);
        }
    }
}
