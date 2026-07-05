using System;

namespace MiniPainterHub.Server.Exceptions;

public sealed class UploadConcurrencyLimitExceededException : Exception
{
    public UploadConcurrencyLimitExceededException()
        : base("Too many image uploads are being processed. Please wait a moment and try again.")
    {
    }
}
