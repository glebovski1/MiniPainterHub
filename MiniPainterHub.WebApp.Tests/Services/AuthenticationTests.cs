using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using MiniPainterHub.WebApp.Services.Auth;
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
        var tokenStore = new RecordingTokenStore();
        var provider = new JwtAuthenticationStateProvider(tokenStore);
        var authStateChanged = CaptureNextAuthState(provider);
        handler.EnqueueJson(HttpStatusCode.OK, $"{{\"isSuccess\":true,\"token\":\"{token}\"}}");
        var service = new AuthService(apiClient, tokenStore, provider);

        var outcome = await service.LoginAsync(new LoginDto { UserName = "artist", Password = "User123!" });

        outcome.Should().Be(LoginOutcome.Success);
        tokenStore.Token.Should().Be(token);
        tokenStore.SetCalls.Should().Be(1);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.Should().Be(new Uri("https://example.test/api/auth/login"));
        handler.Requests[0].Body.Should().Contain("\"userName\":\"artist\"");
        var state = await authStateChanged;
        state.User.Identity?.IsAuthenticated.Should().BeTrue();
        state.User.FindFirst("sub")?.Value.Should().Be("user-1");
    }

    [Fact]
    public async Task AuthService_LoginAsync_WhenTokenMissing_ReturnsUnavailableWithoutPersistingState()
    {
        var handler = new RecordingHttpMessageHandler();
        var apiClient = CreateApiClient(handler, new NotificationRecorder());
        var tokenStore = new RecordingTokenStore();
        var provider = new JwtAuthenticationStateProvider(tokenStore);
        handler.EnqueueJson(HttpStatusCode.OK, """{"isSuccess":false}""");
        var service = new AuthService(apiClient, tokenStore, provider);

        var outcome = await service.LoginAsync(new LoginDto { UserName = "artist", Password = "bad-password" });

        outcome.Should().Be(LoginOutcome.Unavailable);
        tokenStore.Token.Should().BeNull();
        tokenStore.SetCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, LoginOutcome.ValidationFailure)]
    [InlineData(HttpStatusCode.Unauthorized, LoginOutcome.InvalidCredentials)]
    [InlineData(HttpStatusCode.Forbidden, LoginOutcome.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, LoginOutcome.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, LoginOutcome.Unavailable)]
    public async Task AuthService_LoginAsync_WhenRequestFails_MapsOutcomeAndSuppressesGlobalNotifications(
        HttpStatusCode statusCode,
        LoginOutcome expectedOutcome)
    {
        var handler = new RecordingHttpMessageHandler();
        var notifications = new NotificationRecorder();
        var apiClient = CreateApiClient(handler, notifications);
        var tokenStore = new RecordingTokenStore();
        var service = new AuthService(apiClient, tokenStore, new JwtAuthenticationStateProvider(tokenStore));
        handler.EnqueueJson(
            statusCode,
            """{"title":"Sign-in failed","status":400,"errors":{"UserName":["Invalid"]}}""");

        var outcome = await service.LoginAsync(new LoginDto { UserName = "artist", Password = "User123!" });

        outcome.Should().Be(expectedOutcome);
        tokenStore.SetCalls.Should().Be(0);
        notifications.SuccessCalls.Should().BeEmpty();
        notifications.InfoCalls.Should().BeEmpty();
        notifications.WarningCalls.Should().BeEmpty();
        notifications.ErrorCalls.Should().BeEmpty();
        notifications.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public void LoginDto_DefaultValuesAreSafeAndRequired()
    {
        var dto = new LoginDto();
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), validationResults, validateAllProperties: true);

        dto.UserName.Should().BeEmpty();
        dto.Password.Should().BeEmpty();
        isValid.Should().BeFalse();
        validationResults.SelectMany(result => result.MemberNames)
            .Should().BeEquivalentTo(nameof(LoginDto.UserName), nameof(LoginDto.Password));
    }

    [Fact]
    public async Task AuthService_RegisterAsync_UsesRegisterEndpoint()
    {
        var handler = new RecordingHttpMessageHandler();
        var apiClient = CreateApiClient(handler, new NotificationRecorder());
        var tokenStore = new RecordingTokenStore();
        var service = new AuthService(apiClient, tokenStore, new JwtAuthenticationStateProvider(tokenStore));
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
        var tokenStore = new RecordingTokenStore { Token = token };
        var provider = new JwtAuthenticationStateProvider(tokenStore);
        var authStateChanged = CaptureNextAuthState(provider);
        handler.EnqueueJson(HttpStatusCode.ServiceUnavailable, """{"title":"Maintenance","status":503}""");
        var service = new AuthService(apiClient, tokenStore, provider);

        await service.LogoutAsync();

        tokenStore.Token.Should().BeNull();
        tokenStore.ClearCalls.Should().Be(1);
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
        var tokenStore = new RecordingTokenStore
        {
            Token = CreateJwtToken(new Dictionary<string, object?>
            {
                ["sub"] = "user-2",
                ["role"] = new[] { "Admin", "Moderator" },
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
            })
        };
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeTrue();
        state.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Should().BeEquivalentTo("Admin", "Moderator");
        state.User.FindAll("role").Select(claim => claim.Value).Should().BeEquivalentTo("Admin", "Moderator");
    }

    [Fact]
    public async Task JwtAuthenticationStateProvider_WhenTokenIsExpired_ClearsStorageAndReturnsAnonymous()
    {
        var tokenStore = new RecordingTokenStore
        {
            Token = CreateJwtToken(new Dictionary<string, object?>
            {
                ["sub"] = "user-3",
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()
            })
        };
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeFalse();
        tokenStore.Token.Should().BeNull();
        tokenStore.ClearCalls.Should().Be(1);
    }

    [Fact]
    public async Task JwtAuthenticationStateProvider_WhenTokenIsInvalid_ClearsStoreAndReturnsAnonymous()
    {
        var tokenStore = new RecordingTokenStore { Token = "not-a-valid-jwt" };
        var provider = new JwtAuthenticationStateProvider(tokenStore);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.Identity?.IsAuthenticated.Should().BeFalse();
        tokenStore.Token.Should().BeNull();
        tokenStore.ClearCalls.Should().Be(1);
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
