using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Features.Posts;

public interface IPostImageAttachmentService
{
    void ValidateCreateWithImages(CreateImagePostDto dto, CancellationToken cancellationToken);

    Task<List<PostImageDto>> AttachUploadedImagesAsync(int postId, CreateImagePostDto dto, CancellationToken cancellationToken);

    Task<List<PostImageDto>> AddImagesAsync(int postId, IEnumerable<PostImageDto> images);

    Task CleanupDeletedPostImagesAsync(int postId, IEnumerable<PostImage> images);
}
