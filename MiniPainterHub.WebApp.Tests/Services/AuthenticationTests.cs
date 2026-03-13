using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Services;

public class AuthenticationTests
{
    [Fact]
    public async Task AuthService_LoginAsync_WhenTokenReturned_PersistsTokenAndRaisesAuthenticatedState()
    {
        var token = CreateJwtToken(new Dictionary<string, object?>
        {
            ["sub"] = "user-1",
            ["name"] = "artist",
            ["role"] = new[] { "Admin" },
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
        });
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var apiClient = CreateApiClient(handler, notifications);
        var js = new RecordingJsRuntime();
        var provider = new JwtAuthenticationStateProvider(js);
        var authStateChanged = CaptureNextAuthState(provider);
        handler.EnqueueJson(HttpStatusCode.OK, $"{{\"isSuccess\":true,\"token\":\"{token}\"}}");
        var service = new AuthService(apiClient, js, provider);

        var success = await service.LoginAsync(new LoginDto { UserName = "artist", Password = "User123!" });

        success.Should().BeTrue();
        js.LocalStorage["authToken"].Should().Be(token);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/auth/login"));
        handler.Requests[0].Body.Should().Contain("\"userName\":\"artist\"");
        var state = await authStateChanged;
        state.User.Identity?.IsAuthenticated.Should().BeTrue();
        state.User.FindFirst("sub")?.Value.Should().Be("user-1");
    }

    [Fact]
    public async Task AuthService_LoginAsync_WhenTokenMissing_ReturnsFalseWithoutPersistingState()
    {
        var handler = new RecordingHttpMessageHandler();
        var apiClient = CreateApiClient(handler, new NotificationRecorder());
        var js = new RecordingJsRuntime();
        var provider = new JwtAuthenticationStateProvider(js);
        handler.EnqueueJson(HttpStatusCode.OK, """{"isSuccess":false}""");
        var service = new AuthService(apiClient, js, provider);

        var success = await service.LoginAsync(new LoginDto { UserName = "artist", Password = "bad-password" });

        success.Should().BeFalse();
        js.LocalStorage.Should().NotContainKey("authToken");
    }

    [Fact]
    public async Task AuthService_RegisterAsync_UsesRegisterEndpoint()
    {
        var handler = new RecordingHttpMessageHandler();
        var apiClient = CreateApiClient(handler, new NotificationRecorder());
        var service = new AuthService(apiClient, new RecordingJsRuntime(), new JwtAuthenticationStateProvider(new RecordingJsRuntime()));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var success = await service.RegisterAsync(new RegisterDto
        {
            UserName = "artist",
            Email = "artist@example.test",
            Password = "User123!"
        });

        success.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/auth/register"));
        handler.Requests[0].Body.Should().Contain("\"email\":\"artist@example.test\"");
    }

    [Fact]
    public async Task AuthService_LogoutAsync_ClearsTokenAndStillSignsOutDuringMaintenance()
    {
        var token = CreateJwtToken(new Dictionary<string, object?>
        {
            ["sub"] = "user-1",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
        });
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var apiClient = CreateApiClient(handler, notifications);
        var js = new RecordingJsRuntime();
        js.LocalStorage["authToken"] = token;
        var provider = new JwtAuthenticationStateProvider(js);
        var authStateChanged = CaptureNextAuthState(provider);
        handler.EnqueueJson(HttpStatusCode.ServiceUnavailable, """{"title":"Maintenance","status":503}""");
        var service = new AuthService(apiClient, js, provider);

        await service.LogoutAsync();

        js.LocalStorage.Should().NotContainKey("authToken");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Delete);
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/auth/maintenance-bypass"));
        notifications.ErrorCalls.Should().BeEmpty();
        var state = await authStateChanged;
        state.User.Identity?.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task JwtAuthenticationStateProvider_WhenTokenHasRoleArray_AddsRoleClaims()
    {
        var js = new RecordingJsRuntime();
        js.LocalStorage["authToken"] = CreateJwtToken(new Dictionary<string, object?>
        {
            ["sub"] = "user-2",
            ["role"] = new[] { "Admin", "Moderator" },
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        });
        var provider = new JwtAuthenticationStateProvider(js);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeTrue();
        state.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Should().BeEquivalentTo("Admin", "Moderator");
        state.User.FindAll("role").Select(claim => claim.Value).Should().BeEquivalentTo("Admin", "Moderator");
    }

    [Fact]
    public async Task JwtAuthenticationStateProvider_WhenTokenIsExpired_ClearsStorageAndReturnsAnonymous()
    {
        var js = new RecordingJsRuntime();
        js.LocalStorage["authToken"] = CreateJwtToken(new Dictionary<string, object?>
        {
            ["sub"] = "user-3",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()
        });
        var provider = new JwtAuthenticationStateProvider(js);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeFalse();
        js.LocalStorage.Should().NotContainKey("authToken");
    }

    [Fact]
    public async Task JwtAuthorizationMessageHandler_WhenTokenExists_AddsBearerHeader()
    {
        var js = new RecordingJsRuntime();
        js.LocalStorage["authToken"] = "header.payload.signature";
        var terminalHandler = new RecordingHttpMessageHandler();
        terminalHandler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));
        var authHandler = new JwtAuthorizationMessageHandler(js)
        {
            InnerHandler = terminalHandler
        };
        var invoker = new HttpMessageInvoker(authHandler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/posts"), default);

        terminalHandler.Requests.Should().ContainSingle();
        terminalHandler.Requests[0].Authorization?.Scheme.Should().Be("Bearer");
        terminalHandler.Requests[0].Authorization?.Parameter.Should().Be("header.payload.signature");
    }

    private static ApiClient CreateApiClient(HttpMessageHandler handler, NotificationRecorder notifications)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/")
        };

        return new ApiClient(httpClient, notifications);
    }

    private static Task<AuthenticationState> CaptureNextAuthState(AuthenticationStateProvider provider)
    {
        var completion = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.AuthenticationStateChanged += async task =>
        {
            completion.TrySetResult(await task);
        };

        return completion.Task;
    }

    private static string CreateJwtToken(IReadOnlyDictionary<string, object?> payload)
    {
        static string Encode(object value)
        {
            var json = JsonSerializer.Serialize(value);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        return $"{Encode(new { alg = "none", typ = "JWT" })}.{Encode(payload)}.signature";
    }
}
