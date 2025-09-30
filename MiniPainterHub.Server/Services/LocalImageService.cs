using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Services
{
    public class LocalImageService : IImageService
    {
        private readonly string _basePath;
        private readonly string _requestPrefix;

        public LocalImageService(IWebHostEnvironment env, IConfiguration config)
        {
            var relative = config["ImageStorage:LocalPath"] ?? "uploads/images";
            _basePath = Path.Combine(env.WebRootPath, relative);
            Directory.CreateDirectory(_basePath);
            _requestPrefix = "/" + relative.Replace("\\", "/");
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
    }
}
