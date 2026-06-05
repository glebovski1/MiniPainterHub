using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Auth;

public interface ITokenStore
{
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    ValueTask SetTokenAsync(string token);

    ValueTask ClearTokenAsync();
}
