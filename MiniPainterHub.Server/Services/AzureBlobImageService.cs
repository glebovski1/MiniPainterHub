using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;

namespace MiniPainterHub.Server.Services
{
    public class AzureBlobImageService : IImageService, IImageStore
    {
        private readonly BlobContainerClient _container;
        private readonly ImagesOptions _options;
        private static readonly string CacheControl = "public, max-age=31536000, immutable";

        public AzureBlobImageService(IConfiguration config, IOptions<ImagesOptions> options)
            : this(CreateContainer(config), options)
        {
        }

        public AzureBlobImageService(BlobContainerClient container, IOptions<ImagesOptions> options)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var blob = _container.GetBlobClient(fileName);
            await blob.UploadAsync(fileStream, overwrite: true);
            return blob.Uri.ToString();
        }

        public async Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(variants);

            var prefix = $"images/{postId:D}/";

            var maxUrl = await UploadVariantAsync(prefix + BuildName(imageId, "max", variants.Max.Extension), variants.Max, ct);
            var previewUrl = await UploadVariantAsync(prefix + BuildName(imageId, "preview", variants.Preview.Extension), variants.Preview, ct);
            var thumbUrl = await UploadVariantAsync(prefix + BuildName(imageId, "thumb", variants.Thumb.Extension), variants.Thumb, ct);

            string? originalUrl = null;
            if (_options.KeepOriginal && variants.Original is { } original)
            {
                originalUrl = await UploadVariantAsync(prefix + BuildName(imageId, "original", original.Extension), original, ct);
            }

            return new ImageStoreResult(maxUrl, previewUrl, thumbUrl, originalUrl);
        }

        public async Task<Stream> DownloadAsync(string fileName)
        {
            var blob = _container.GetBlobClient(fileName);
            var exists = await blob.ExistsAsync();

            if (!exists.Value)
            {
                throw new NotFoundException($"Image '{fileName}' not found.");
            }

            try
            {
                var result = await blob.DownloadAsync();
                return result.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new NotFoundException($"Image '{fileName}' not found.", ex);
            }
        }

        public async Task DeleteAsync(string fileName)
        {
            var blob = _container.GetBlobClient(fileName);
            await blob.DeleteIfExistsAsync();
        }

        private static BlobContainerClient CreateContainer(IConfiguration config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var conn = config["ImageStorage:AzureConnectionString"]
                ?? throw new InvalidOperationException("Azure connection string is not configured.");
            var containerName = config["ImageStorage:AzureContainer"]
                ?? throw new InvalidOperationException("Azure container name is not configured.");

            var container = new BlobContainerClient(conn, containerName);
            container.CreateIfNotExists();
            return container;
        }

        private static string BuildName(Guid imageId, string suffix, string extension)
            => $"{imageId:D}_{suffix}.{extension}";

        private async Task<string> UploadVariantAsync(string name, ImageVariant variant, CancellationToken ct)
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
}
