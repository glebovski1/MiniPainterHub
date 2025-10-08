using System;
using System.Threading;
using System.Threading.Tasks;
using MiniPainterHub.Server.Services.Models;

namespace MiniPainterHub.Server.Services.Interfaces;

/// <summary>
/// Persists processed image variants.
/// </summary>
public interface IImageStore
{
    /// <summary>
    /// Saves the provided image variants for the specified post and image identifiers.
    /// </summary>
    /// <param name="postId">The parent post identifier.</param>
    /// <param name="imageId">The base image identifier.</param>
    /// <param name="variants">The processed variants.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage result including URLs to the saved variants.</returns>
    Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct);
}
