using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MiniPainterHub.Server.Services.Images.Models;

namespace MiniPainterHub.Server.Services.Interfaces;

/// <summary>
/// Processes uploaded images into predefined variants.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Processes the incoming image stream into application-specific variants.
    /// </summary>
    /// <param name="input">The input stream.</param>
    /// <param name="contentType">The declared content type, used for validation and metadata.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The processed image variants.</returns>
    Task<ImageVariants> ProcessAsync(Stream input, string? contentType, CancellationToken ct);
}
