using System;
using System.Collections.Generic;
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
            var location = LocalImageStoragePaths.Resolve(env, config);
            _basePath = location.PhysicalPath;
            Directory.CreateDirectory(_basePath);
            _requestPrefix = location.RequestPath;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var filePath = ResolveStoragePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var fs = new FileStream(filePath, FileMode.Create);
            await fileStream.CopyToAsync(fs);
            return $"{_requestPrefix}/{Uri.EscapeDataString(fileName)}";
        }

        public Task<Stream> DownloadAsync(string fileName)
        {
            var filePath = ResolveStoragePath(fileName);

            if (!File.Exists(filePath))
            {
                throw new NotFoundException($"Image '{fileName}' not found.");
            }

            Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string fileName)
        {
            var filePath = ResolveStoragePath(fileName);
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

        public Task DeleteAsync(Guid postId, Guid imageId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var folder = Path.Combine(_basePath, postId.ToString("D"));
            if (!Directory.Exists(folder))
            {
                return Task.CompletedTask;
            }

            foreach (var path in Directory.GetFiles(folder, $"{imageId:D}_*.*", SearchOption.TopDirectoryOnly))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            return Task.CompletedTask;
        }

        private string SaveVariant(string folder, Guid postId, Guid imageId, string suffix, ImageVariant variant)
        {
            var fileName = $"{imageId:D}_{suffix}.{variant.Extension}";
            var filePath = Path.Combine(folder, fileName);
            File.WriteAllBytes(filePath, variant.Content);

            return $"{_requestPrefix}/{postId:D}/{Uri.EscapeDataString(fileName)}";
        }

        private string ResolveStoragePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw InvalidStorageKey();
            }

            var normalized = fileName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized)
                || HasWindowsDriveQualifier(normalized)
                || normalized.Contains("..", StringComparison.Ordinal))
            {
                throw InvalidStorageKey();
            }

            var basePath = Path.GetFullPath(_basePath);
            var candidate = Path.GetFullPath(Path.Combine(basePath, normalized));
            var basePrefix = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidStorageKey();
            }

            return candidate;
        }

        private static bool HasWindowsDriveQualifier(string path) =>
            path.Length >= 2
            && path[1] == ':'
            && IsAsciiLetter(path[0]);

        private static bool IsAsciiLetter(char value) =>
            value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

        private static DomainValidationException InvalidStorageKey() =>
            new("Invalid image storage key.", new Dictionary<string, string[]>
            {
                ["fileName"] = new[] { "Image storage keys must be relative paths within the configured image root." }
            });
    }
}
