using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;

namespace MiniPainterHub.Server.Services.Images;

/// <summary>
/// Azure Blob Storage backed implementation of <see cref="IImageStore"/>.
/// </summary>
public sealed class AzureBlobImageStore : IImageStore
{
    private static readonly string CacheControl = "public, max-age=31536000, immutable";
    private readonly BlobContainerClient _container;
    private readonly ImagesOptions _options;

    public AzureBlobImageStore(BlobContainerClient container, IOptions<ImagesOptions> options)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(variants);

        var prefix = $"images/{postId:D}/";

        var maxUrl = await UploadAsync(prefix + BuildName(imageId, "max", variants.Max.Extension), variants.Max, ct);
        var previewUrl = await UploadAsync(prefix + BuildName(imageId, "preview", variants.Preview.Extension), variants.Preview, ct);
        var thumbUrl = await UploadAsync(prefix + BuildName(imageId, "thumb", variants.Thumb.Extension), variants.Thumb, ct);

        string? originalUrl = null;
        if (_options.KeepOriginal && variants.Original is { } original)
        {
            originalUrl = await UploadAsync(prefix + BuildName(imageId, "original", original.Extension), original, ct);
        }

        return new ImageStoreResult(maxUrl, previewUrl, thumbUrl, originalUrl);
    }

    private static string BuildName(Guid imageId, string suffix, string extension)
        => $"{imageId:D}_{suffix}.{extension}";

    private async Task<string> UploadAsync(string name, ImageVariant variant, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(name);
        await using var stream = new MemoryStream(variant.Content);

        var headers = new BlobHttpHeaders
        {
            ContentType = variant.ContentType,
            CacheControl = CacheControl
        };

        await blob.UploadAsync(stream, headers, cancellationToken: ct, overwrite: true);
        return blob.Uri.ToString();
    }
}
