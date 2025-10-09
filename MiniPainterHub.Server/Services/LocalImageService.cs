using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;

namespace MiniPainterHub.Server.Services
{
    public class LocalImageService : IImageService, IImageStore
    {
        private readonly string _basePath;
        private readonly string _requestPrefix;
        private readonly ImagesOptions _options;

        public LocalImageService(IWebHostEnvironment env, IConfiguration config, IOptions<ImagesOptions> options)
        {
            var relative = config["ImageStorage:LocalPath"] ?? "uploads/images";
            _basePath = Path.Combine(env.WebRootPath, relative);
            Directory.CreateDirectory(_basePath);
            _requestPrefix = "/" + relative.Replace("\\", "/");
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);
            using var fs = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(fs);
            return $"{_requestPrefix}/{Uri.EscapeDataString(fileName)}";
        }

        public Task<Stream> DownloadAsync(string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);

            if (!File.Exists(filePath))
            {
                throw new NotFoundException($"Image '{fileName}' not found.");
            }

            Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
            return Task.CompletedTask;
        }

        public Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(variants);

            var folder = Path.Combine(_basePath, postId.ToString("D"));
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

        private string SaveVariant(string folder, Guid postId, Guid imageId, string suffix, ImageVariant variant)
        {
            var fileName = $"{imageId:D}_{suffix}.{variant.Extension}";
            var filePath = Path.Combine(folder, fileName);
            File.WriteAllBytes(filePath, variant.Content);

            return $"{_requestPrefix}/{postId:D}/{Uri.EscapeDataString(fileName)}";
        }
    }
}
