using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Route("api/images")]
public sealed class ImagesController : ControllerBase
{
    private const string UploadRequestPath = "/uploads/images/";
    private readonly IImageService _imageService;
    private readonly IImageProcessor _imageProcessor;

    public ImagesController(IImageService imageService, IImageProcessor imageProcessor)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
    }

    [HttpGet("thumbnail")]
    [AllowAnonymous]
    public async Task<IActionResult> GetThumbnail([FromQuery] string? url, CancellationToken ct)
    {
        return await GetVariant(url, ImageVariantKind.Thumbnail, ct);
    }

    [HttpGet("preview")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPreview([FromQuery] string? url, CancellationToken ct)
    {
        return await GetVariant(url, ImageVariantKind.Preview, ct);
    }

    private async Task<IActionResult> GetVariant(string? url, ImageVariantKind kind, CancellationToken ct)
    {
        if (!TryResolveUploadFileName(url, out var fileName))
        {
            return BadRequest();
        }

        var cacheFileName = BuildCacheFileName(fileName, kind);
        var cached = await TryDownloadAsync(cacheFileName);
        if (cached is not null)
        {
            return VariantFile(cached);
        }

        await using var source = await _imageService.DownloadAsync(fileName);
        var variants = await _imageProcessor.ProcessAsync(source, contentType: null, ct);
        var variant = kind == ImageVariantKind.Preview ? variants.Preview : variants.Thumb;
        await using (var output = new MemoryStream(variant.Content))
        {
            await _imageService.UploadAsync(output, cacheFileName);
        }

        var generated = await _imageService.DownloadAsync(cacheFileName);
        return VariantFile(generated);
    }

    private FileStreamResult VariantFile(Stream stream)
    {
        Response.Headers.CacheControl = "public,max-age=604800";
        return File(stream, "image/webp");
    }

    private async Task<Stream?> TryDownloadAsync(string fileName)
    {
        try
        {
            return await _imageService.DownloadAsync(fileName);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private static bool TryResolveUploadFileName(string? url, out string fileName)
    {
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(url)
            || url.Contains('?', StringComparison.Ordinal)
            || url.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        var path = url.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        if (!path.StartsWith(UploadRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = Uri.UnescapeDataString(path[UploadRequestPath.Length..])
            .Replace('\\', '/')
            .TrimStart('/');

        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        fileName = relativePath;
        return true;
    }

    private static string BuildCacheFileName(string fileName, ImageVariantKind kind)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fileName));
        var prefix = kind == ImageVariantKind.Preview ? "preview-cache" : "thumbnail-cache";
        return $"{prefix}-{Convert.ToHexString(hash).ToLowerInvariant()}.webp";
    }

    private enum ImageVariantKind
    {
        Preview,
        Thumbnail
    }
}
