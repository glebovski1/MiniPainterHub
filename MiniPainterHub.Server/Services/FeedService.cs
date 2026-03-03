using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class FeedService : IFeedService
    {
        private readonly AppDbContext _db;

        public FeedService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResult<FeedItemDto>> GetFeedAsync(int page, int pageSize)
        {
            var policy = await _db.FeedPolicies.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive)
                ?? throw new InvalidOperationException("No active feed policy configured.");

            var posts = await _db.Posts.AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Content,
                    p.CreatedById,
                    p.CreatedUtc,
                    p.IsPinned,
                    p.PinPriority,
                    Likes = p.Likes.Count,
                    Comments = p.Comments.Count
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var scoredPosts = posts.Select(p => new
            {
                Item = new FeedItemDto
                {
                    Type = "Post",
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    PinPriority = p.PinPriority,
                    Score = Score(policy, now, p.CreatedUtc, p.Likes, p.Comments, 0)
                },
                p.CreatedById,
                p.IsPinned
            });

            var news = await _db.NewsItems.AsNoTracking()
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.BodyMarkdown,
                    n.IsPinned,
                    n.PinPriority,
                    n.PublishAt
                })
                .ToListAsync();

            var pinned = scoredPosts.Where(x => x.IsPinned).Select(x => x.Item)
                .Concat(news.Where(x => x.IsPinned).Select(n => new FeedItemDto
                {
                    Type = "News",
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.BodyMarkdown,
                    PinPriority = n.PinPriority,
                    Score = 0
                }))
                .OrderByDescending(x => x.PinPriority)
                .ThenBy(x => x.Id)
                .ToList();

            var remaining = scoredPosts.Where(x => !x.IsPinned)
                .OrderByDescending(x => x.Item.Score)
                .ThenByDescending(x => x.Item.Id)
                .ToList();

            if (policy.DiversityByAuthor)
            {
                var perAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                remaining = remaining.Where(x =>
                {
                    if (!perAuthor.ContainsKey(x.CreatedById)) perAuthor[x.CreatedById] = 0;
                    if (perAuthor[x.CreatedById] >= policy.MaxPerAuthorPerPage) return false;
                    perAuthor[x.CreatedById]++;
                    return true;
                }).ToList();
            }

            var regularNews = news.Where(x => !x.IsPinned)
                .OrderByDescending(x => x.PublishAt)
                .ThenByDescending(x => x.Id)
                .Select(n => new FeedItemDto
                {
                    Type = "News",
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.BodyMarkdown,
                    PinPriority = 0,
                    Score = 0
                });

            var all = pinned.Concat(remaining.Select(x => x.Item)).Concat(regularNews).ToList();
            var total = all.Count;
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<FeedItemDto> { Items = items, TotalCount = total, PageNumber = page, PageSize = pageSize };
        }

        private static double Score(Entities.FeedPolicy policy, DateTime now, DateTime createdUtc, int likes, int comments, int reports)
        {
            var ageHours = Math.Max(0, (now - createdUtc).TotalHours);
            var decay = Math.Exp(-Math.Log(2) * ageHours / Math.Max(0.1, policy.HalfLifeHours));
            return policy.WRecency * decay + policy.WLikes * Math.Log(1 + likes) + policy.WComments * Math.Log(1 + comments) - policy.WReportsPenalty * Math.Log(1 + reports);
        }
    }
}
