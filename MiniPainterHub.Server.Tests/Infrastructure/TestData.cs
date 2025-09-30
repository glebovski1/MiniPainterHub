using System;
using System.Collections.Generic;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class TestData
{
    public static ApplicationUser CreateUser(string id, string? userName = null)
        => new()
        {
            Id = id,
            UserName = userName ?? $"user-{id}"
        };

    public static Post CreatePost(int id, string userId, int imageCount = 0, bool isDeleted = false)
    {
        var post = new Post
        {
            Id = id,
            Title = $"Title {id}",
            Content = $"Content {id}",
            CreatedById = userId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            IsDeleted = isDeleted,
            Images = new List<PostImage>()
        };

        for (var i = 0; i < imageCount; i++)
        {
            post.Images.Add(new PostImage
            {
                Id = i + 1,
                PostId = id,
                ImageUrl = $"https://img/{id}/{i}",
                ThumbnailUrl = $"https://thumb/{id}/{i}"
            });
        }

        return post;
    }

    public static Comment CreateComment(int id, int postId, string userId, bool isDeleted = false)
        => new()
        {
            Id = id,
            PostId = postId,
            AuthorId = userId,
            Text = $"Comment {id}",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            IsDeleted = isDeleted
        };

    public static CreatePostDto CreatePostDto(int imageCount = 0)
    {
        var dto = new CreatePostDto
        {
            Title = "New post",
            Content = "Post content",
            Images = imageCount == 0 ? null : new List<PostImageDto>()
        };

        for (var i = 0; i < imageCount; i++)
        {
            dto.Images!.Add(new PostImageDto
            {
                ImageUrl = $"https://img/new/{i}",
                ThumbnailUrl = $"https://thumb/new/{i}"
            });
        }

        return dto;
    }

    public static IEnumerable<PostImageDto> CreateImages(int count, string prefix)
    {
        for (var i = 0; i < count; i++)
        {
            yield return new PostImageDto
            {
                ImageUrl = $"https://img/{prefix}/{i}",
                ThumbnailUrl = $"https://thumb/{prefix}/{i}"
            };
        }
    }
}
