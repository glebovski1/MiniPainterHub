using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApiClient _api;
        private readonly IJSRuntime _js;
        private readonly JwtAuthenticationStateProvider _authStateProvider;

        public AuthService(ApiClient api, IJSRuntime js, JwtAuthenticationStateProvider authStateProvider)
        {
            _api = api;
            _js = js;
            _authStateProvider = authStateProvider;
        }

        public async Task<bool> LoginAsync(LoginDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonContent.Create(dto)
            };

            var response = await _api.SendAsync<AuthResponseDto>(request);
            if (string.IsNullOrWhiteSpace(response?.Token))
            {
                return false;
            }

            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", response.Token);
            _authStateProvider.NotifyUserAuthentication(response.Token);
            return true;
        }

        public async Task<bool> RegisterAsync(RegisterDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/register")
            {
                Content = JsonContent.Create(dto)
            };

            return await _api.SendAsync(request);
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
            _authStateProvider.NotifyUserAuthentication(null);
        }
    }
}
