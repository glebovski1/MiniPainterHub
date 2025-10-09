using System;

namespace MiniPainterHub.Server.Exceptions
{
    /// <summary>
    /// Exception thrown when an uploaded image exceeds the configured size limit.
    /// </summary>
    public sealed class ImageTooLargeException : Exception
    {
        public ImageTooLargeException(string fileName, long length, long maxBytes)
            : base($"Image '{fileName}' exceeds the {maxBytes / (1024 * 1024)} MB limit.")
        {
            FileName = fileName;
            Length = length;
            MaxBytes = maxBytes;
        }

        public string FileName { get; }

        public long Length { get; }

        public long MaxBytes { get; }
    }
}
