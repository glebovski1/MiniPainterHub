namespace MiniPainterHub.Common.DTOs;

public sealed class PostExperienceDto
{
    public PostDto Post { get; set; } = new();

    public PostViewerDto Viewer { get; set; } = new();

    public LikeDto Likes { get; set; } = new();
}
