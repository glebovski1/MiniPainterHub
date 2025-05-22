using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using MiniPainterHub.Server.Services.Interfaces;
using System.IO;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class AzureBlobImageService : IImageService
    {
        private readonly BlobContainerClient _container;

        public AzureBlobImageService(IConfiguration config)
        {
            var conn = config["ImageStorage:AzureConnectionString"];
            var containerName = config["ImageStorage:AzureContainer"];
            _container = new BlobContainerClient(conn, containerName);
            _container.CreateIfNotExists();
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
            var result = await blob.DownloadAsync();
            return result.Value.Content;
        }

        public async Task DeleteAsync(string fileName)
        {
            var blob = _container.GetBlobClient(fileName);
            await blob.DeleteIfExistsAsync();
        }
    }
}
