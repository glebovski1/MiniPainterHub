using Microsoft.JSInterop;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Interfaces;
using System.Net.Http.Json;

namespace MiniPainterHub.WebApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task<bool> LoginAsync(LoginDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", dto);
            if (!resp.IsSuccessStatusCode) return false;

            var result = await resp.Content.ReadFromJsonAsync<AuthResponseDto>();
            await _js.InvokeVoidAsync("localStorage.setItem", "authToken", result.Token);
            return true;
        }

        public async Task<bool> RegisterAsync(RegisterDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/auth/register", dto);
            return resp.IsSuccessStatusCode;
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "authToken");
        }
    }
}
