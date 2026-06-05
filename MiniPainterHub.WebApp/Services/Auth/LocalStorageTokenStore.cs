using Microsoft.JSInterop;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.WebApp.Services.Auth;

public sealed class LocalStorageTokenStore : ITokenStore
{
    private const string TokenKey = "authToken";
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageTokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
        _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, TokenKey);

    public ValueTask SetTokenAsync(string token) =>
        _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

    public ValueTask ClearTokenAsync() =>
        _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
}
