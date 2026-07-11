using System.Net.Http;
using System.Net;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Auth;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApiClient _api;
        private readonly ITokenStore _tokenStore;
        private readonly JwtAuthenticationStateProvider _authStateProvider;

        public AuthService(ApiClient api, ITokenStore tokenStore, JwtAuthenticationStateProvider authStateProvider)
        {
            _api = api;
            _tokenStore = tokenStore;
            _authStateProvider = authStateProvider;
        }

        public async Task<LoginOutcome> LoginAsync(LoginDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };

            var result = await _api.SendForResultAsync<AuthResponseDto>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });

            if (!result.Success)
            {
                return result.StatusCode switch
                {
                    HttpStatusCode.BadRequest => LoginOutcome.ValidationFailure,
                    HttpStatusCode.Unauthorized => LoginOutcome.InvalidCredentials,
                    HttpStatusCode.Forbidden => LoginOutcome.Forbidden,
                    HttpStatusCode.TooManyRequests => LoginOutcome.RateLimited,
                    _ => LoginOutcome.Unavailable
                };
            }

            if (result.Value?.IsSuccess != true || string.IsNullOrWhiteSpace(result.Value.Token))
            {
                return LoginOutcome.Unavailable;
            }

            await _tokenStore.SetTokenAsync(result.Value.Token);
            _authStateProvider.NotifyUserAuthentication(result.Value.Token);
            return LoginOutcome.Success;
        }

        public async Task<bool> RegisterAsync(RegisterDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/register")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };

            return await _api.SendAsync(request);
        }

        public async Task LogoutAsync()
        {
            await _api.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, "api/auth/maintenance-bypass"),
                new ApiRequestOptions
                {
                    SuppressErrorNotifications = true,
                    SuppressedStatusCodes = new HashSet<HttpStatusCode> { HttpStatusCode.ServiceUnavailable }
                });

            await _tokenStore.ClearTokenAsync();
            _authStateProvider.NotifyUserAuthentication(null);
        }
    }
}
