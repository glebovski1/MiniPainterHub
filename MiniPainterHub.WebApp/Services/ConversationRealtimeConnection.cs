using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace MiniPainterHub.WebApp.Services;

public interface IConversationRealtimeConnection : IAsyncDisposable
{
    HubConnectionState State { get; }

    event Func<string?, Task>? Reconnected;

    IDisposable On<T>(string methodName, Action<T> handler);

    Task StartAsync();

    Task InvokeAsync(string methodName, params object[] args);
}

public interface IConversationRealtimeConnectionFactory
{
    Task<IConversationRealtimeConnection?> CreateAsync();
}

public sealed class SignalRConversationRealtimeConnectionFactory : IConversationRealtimeConnectionFactory
{
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _jsRuntime;

    public SignalRConversationRealtimeConnectionFactory(NavigationManager navigation, IJSRuntime jsRuntime)
    {
        _navigation = navigation;
        _jsRuntime = jsRuntime;
    }

    public async Task<IConversationRealtimeConnection?> CreateAsync()
    {
        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(_navigation.ToAbsoluteUri("/hubs/chat"), options =>
            {
                options.AccessTokenProvider = async () =>
                    await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            })
            .WithAutomaticReconnect()
            .Build();

        return new HubConversationRealtimeConnection(connection);
    }
}

public sealed class HubConversationRealtimeConnection : IConversationRealtimeConnection
{
    private readonly HubConnection _hubConnection;

    public HubConversationRealtimeConnection(HubConnection hubConnection)
    {
        _hubConnection = hubConnection;
    }

    public HubConnectionState State => _hubConnection.State;

    public event Func<string?, Task>? Reconnected
    {
        add => _hubConnection.Reconnected += value;
        remove => _hubConnection.Reconnected -= value;
    }

    public IDisposable On<T>(string methodName, Action<T> handler)
        => _hubConnection.On(methodName, handler);

    public Task StartAsync()
        => _hubConnection.StartAsync();

    public Task InvokeAsync(string methodName, params object[] args)
        => _hubConnection.InvokeAsync(methodName, args);

    public ValueTask DisposeAsync()
        => _hubConnection.DisposeAsync();
}
