using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace MiniPainterHub.WebApp.Tests.Infrastructure;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<RecordedRequest> Requests { get; } = new();

    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

    public void EnqueueJson(HttpStatusCode statusCode, string? json = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        _responses.Enqueue(response);
    }

    public void Enqueue(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization,
            body,
            request.Content?.Headers?.ContentType?.MediaType));

        if (_responses.Count > 0)
        {
            return _responses.Dequeue();
        }

        return ResponseFactory?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    AuthenticationHeaderValue? Authorization,
    string? Body,
    string? ContentType);

internal sealed class NotificationRecorder : MiniPainterHub.WebApp.Services.Notifications.INotificationService
{
    public List<NotificationCall> SuccessCalls { get; } = new();
    public List<NotificationCall> InfoCalls { get; } = new();
    public List<NotificationCall> WarningCalls { get; } = new();
    public List<NotificationCall> ErrorCalls { get; } = new();
    public List<IDictionary<string, string[]>> ValidationErrors { get; } = new();

    public ValueTask ShowSuccessAsync(string message, string? header = null)
    {
        SuccessCalls.Add(new NotificationCall(message, header));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowInfoAsync(string message, string? header = null)
    {
        InfoCalls.Add(new NotificationCall(message, header));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowWarningAsync(string message, string? header = null)
    {
        WarningCalls.Add(new NotificationCall(message, header));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowErrorAsync(string message, string? header = null)
    {
        ErrorCalls.Add(new NotificationCall(message, header));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowValidationErrorsAsync(IDictionary<string, string[]> errors)
    {
        ValidationErrors.Add(errors);
        return ValueTask.CompletedTask;
    }
}

internal sealed record NotificationCall(string Message, string? Header);

internal sealed class RecordingJsRuntime : IJSRuntime
{
    public List<JsInvocation> Invocations { get; } = new();

    public Dictionary<string, string?> LocalStorage { get; } = new(StringComparer.Ordinal);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var copiedArgs = args is null ? Array.Empty<object?>() : (object?[])args.Clone();
        Invocations.Add(new JsInvocation(identifier, copiedArgs));

        object? result = identifier switch
        {
            "localStorage.getItem" => copiedArgs.Length > 0 && copiedArgs[0] is string key && LocalStorage.TryGetValue(key, out var value)
                ? value
                : null,
            "localStorage.setItem" => SetItem(copiedArgs),
            "localStorage.removeItem" => RemoveItem(copiedArgs),
            _ => default(TValue)
        };

        return new ValueTask<TValue>((TValue?)result!);
    }

    private object? SetItem(IReadOnlyList<object?> args)
    {
        if (args.Count >= 2 && args[0] is string key)
        {
            LocalStorage[key] = args[1]?.ToString();
        }

        return null;
    }

    private object? RemoveItem(IReadOnlyList<object?> args)
    {
        if (args.Count >= 1 && args[0] is string key)
        {
            LocalStorage.Remove(key);
        }

        return null;
    }
}

internal sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

internal sealed class FakeBrowserFile : IBrowserFile
{
    private readonly byte[] _content;

    public FakeBrowserFile(string name, string contentType, byte[] content)
    {
        Name = name;
        ContentType = contentType;
        _content = content;
        LastModified = DateTimeOffset.UtcNow;
    }

    public string Name { get; }

    public DateTimeOffset LastModified { get; }

    public long Size => _content.Length;

    public string ContentType { get; }

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(_content, writable: false);
}
