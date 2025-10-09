using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;

namespace MiniPainterHub.Server.Services.Images;

/// <summary>
/// Local file system implementation for storing processed variants during development.
/// </summary>
public sealed class LocalImageStore : IImageStore
{
    private readonly string _basePath;
    private readonly string _requestPrefix;
    private readonly ImagesOptions _options;

    public LocalImageStore(IWebHostEnvironment env, IConfiguration configuration, IOptions<ImagesOptions> options)
    {
        if (env is null)
        {
            throw new ArgumentNullException(nameof(env));
        }

        var relative = configuration["ImageStorage:LocalPath"] ?? "uploads/images";
        _basePath = Path.Combine(env.WebRootPath, relative);
        Directory.CreateDirectory(_basePath);
        _requestPrefix = "/" + relative.Replace("\\", "/");
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<ImageStoreResult> SaveAsync(int postId, Guid imageId, ImageVariants variants, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(variants);

        var folder = Path.Combine(_basePath, postId.ToString());
        Directory.CreateDirectory(folder);

        var maxUrl = SaveVariant(folder, postId, imageId, "max", variants.Max);
        var previewUrl = SaveVariant(folder, postId, imageId, "preview", variants.Preview);
        var thumbUrl = SaveVariant(folder, postId, imageId, "thumb", variants.Thumb);

        string? originalUrl = null;
        if (_options.KeepOriginal && variants.Original is { } original)
        {
            originalUrl = SaveVariant(folder, postId, imageId, "original", original);
        }

        return Task.FromResult(new ImageStoreResult(maxUrl, previewUrl, thumbUrl, originalUrl));
    }

    private string SaveVariant(string folder, int postId, Guid imageId, string suffix, ImageVariant variant)
    {
        var fileName = $"{imageId:D}_{suffix}.{variant.Extension}";
        var filePath = Path.Combine(folder, fileName);
        File.WriteAllBytes(filePath, variant.Content);

        return $"{_requestPrefix}/{postId}/{Uri.EscapeDataString(fileName)}";
    }
}
