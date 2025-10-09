using System;

namespace MiniPainterHub.Server.Exceptions
{
    /// <summary>
    /// Exception thrown when an uploaded image uses an unsupported MIME type.
    /// </summary>
    public sealed class UnsupportedImageContentTypeException : Exception
    {
        public UnsupportedImageContentTypeException(string fileName, string? contentType)
            : base($"Images must be JPEG, PNG, or WebP. '{fileName}' was {contentType}.")
        {
            FileName = fileName;
            ContentType = contentType;
        }

        public string FileName { get; }

        public string? ContentType { get; }
    }
}
