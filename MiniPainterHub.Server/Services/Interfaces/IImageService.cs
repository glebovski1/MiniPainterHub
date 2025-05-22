using System.IO;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    /// <summary>
    /// Abstraction for image storage operations.
    /// </summary>
    public interface IImageService
    {
        /// <summary>
        /// Uploads an image stream and returns its accessible URL or path.
        /// </summary>
        Task<string> UploadAsync(Stream fileStream, string fileName);

        /// <summary>
        /// Retrieves an image stream by its file name or blob name.
        /// </summary>
        Task<Stream> DownloadAsync(string fileName);

        /// <summary>
        /// Deletes an image by its file name or blob name.
        /// </summary>
        Task DeleteAsync(string fileName);
    }
}
