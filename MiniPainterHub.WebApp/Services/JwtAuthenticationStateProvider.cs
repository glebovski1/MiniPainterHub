using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace MiniPainterHub.WebApp.Services
{
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
        private readonly IJSRuntime _js;

        public JwtAuthenticationStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "authToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return AnonymousState;
            }

            List<Claim> claims;
            try
            {
                claims = ParseClaimsFromJwt(token).ToList();
            }
            catch
            {
                await ClearPersistedTokenAsync();
                return AnonymousState;
            }

            if (IsExpired(claims))
            {
                await ClearPersistedTokenAsync();
                return AnonymousState;
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public void NotifyUserAuthentication(string? token)
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private static bool IsExpired(IEnumerable<Claim> claims)
        {
            var expClaim = claims.FirstOrDefault(c => c.Type.Equals("exp", StringComparison.OrdinalIgnoreCase));
            if (expClaim is null || !long.TryParse(expClaim.Value, out var expSeconds))
            {
                return false;
            }

            var expiration = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            return expiration <= DateTimeOffset.UtcNow;
        }

        private Task ClearPersistedTokenAsync() => _js.InvokeVoidAsync("localStorage.removeItem", "authToken").AsTask();

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes)!;

            return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!));
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
