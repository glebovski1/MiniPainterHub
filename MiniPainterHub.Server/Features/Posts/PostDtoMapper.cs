using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniPainterHub.Server.Features.Posts;

internal static class PostDtoMapper
{
    public static PostDto ToPostDto(Post post) =>
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
                    ThumbnailUrl = i.ThumbnailUrl,
                    Width = i.Width,
                    Height = i.Height
                })
                .ToList(),
            Tags = MapTags(post.PostTags)
        };

    public static PostSummaryDto ToPostSummaryDto(Post post, int commentCount, int likeCount)
    {
        var primaryImage = post.Images.OrderBy(i => i.Id).FirstOrDefault();

        return new()
        {
            Id = post.Id,
            Title = post.Title,
            Snippet = post.Content.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content,
            ImageUrl = primaryImage?.ImageUrl,
            ThumbnailUrl = ResolveSummaryThumbnailUrl(primaryImage),
            AuthorName = ResolveDisplayName(post.CreatedBy?.UserName, post.CreatedBy?.Profile?.DisplayName),
            AuthorId = post.CreatedById,
            CreatedAt = post.CreatedUtc,
            CommentCount = commentCount,
            LikeCount = likeCount,
            IsDeleted = post.IsDeleted,
            Tags = MapTags(post.PostTags)
        };
    }

    private static string? ResolveSummaryThumbnailUrl(PostImage? image)
    {
        if (image is null)
        {
            return null;
        }

        if (IsUsableVariantUrl(image.ThumbnailUrl, image.ImageUrl))
        {
            return image.ThumbnailUrl;
        }

        if (IsUsableVariantUrl(image.PreviewUrl, image.ImageUrl))
        {
            return image.PreviewUrl;
        }

        return BuildThumbnailEndpointUrl(image.ImageUrl);
    }

    private static bool IsUsableVariantUrl(string? candidateUrl, string? fullImageUrl) =>
        !string.IsNullOrWhiteSpace(candidateUrl)
        && !string.Equals(candidateUrl, fullImageUrl, StringComparison.OrdinalIgnoreCase);

    private static string? BuildThumbnailEndpointUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var path = imageUrl;
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        return path.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase)
            ? "/api/images/thumbnail?url=" + Uri.EscapeDataString(path)
            : null;
    }

    private static List<TagDto> MapTags(IEnumerable<PostTag> postTags) =>
        postTags
            .OrderBy(pt => pt.Tag.DisplayName)
            .Select(pt => new TagDto
            {
                Name = pt.Tag.DisplayName,
                Slug = pt.Tag.Slug
            })
            .ToList();

    public static string ResolveDisplayName(string? userName, string? profileDisplayName) =>
        string.IsNullOrWhiteSpace(profileDisplayName) ? (userName ?? string.Empty) : profileDisplayName;
}
