using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IUploadConcurrencyLimiter
{
    ValueTask<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}
