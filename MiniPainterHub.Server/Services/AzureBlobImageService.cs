using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Services
{
    public class AzureBlobImageService : IImageService
    {
        private readonly BlobContainerClient _container;

        public AzureBlobImageService(IConfiguration config)
            : this(CreateContainer(config))
        {
        }

        public AzureBlobImageService(BlobContainerClient container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var blob = _container.GetBlobClient(fileName);
            await blob.UploadAsync(fileStream, overwrite: true);
            return blob.Uri.ToString();
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
    }
}
