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

            if (result.Value?.IsSuccess != true || !await AcceptTokenAsync(result.Value.Token))
            {
                return LoginOutcome.Unavailable;
            }
            return LoginOutcome.Success;
        }

        public async Task<RegistrationOutcome> RegisterAsync(RegisterDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/register")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };

            var result = await _api.SendForResultAsync<RegistrationResultDto>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });
            if (!result.Success || result.Value?.IsSuccess != true)
            {
                return result.StatusCode switch
                {
                    HttpStatusCode.BadRequest => RegistrationOutcome.ValidationFailure,
                    HttpStatusCode.TooManyRequests => RegistrationOutcome.RateLimited,
                    _ => RegistrationOutcome.Unavailable
                };
            }

            if (!result.Value.RequiresEmailConfirmation)
            {
                return RegistrationOutcome.Success;
            }

            return result.Value.ConfirmationEmailSent
                ? RegistrationOutcome.ConfirmationSent
                : RegistrationOutcome.ConfirmationPendingDelivery;
        }

        public async Task<EmailConfirmationOutcome> ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/email/confirm")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };
            var result = await _api.SendForResultAsync<object>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });
            if (result.Success)
            {
                return EmailConfirmationOutcome.Success;
            }

            return result.StatusCode switch
            {
                HttpStatusCode.BadRequest => EmailConfirmationOutcome.InvalidOrExpired,
                HttpStatusCode.TooManyRequests => EmailConfirmationOutcome.RateLimited,
                _ => EmailConfirmationOutcome.Unavailable
            };
        }

        public async Task<ResendEmailConfirmationOutcome> ResendEmailConfirmationAsync(ResendEmailConfirmationDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/email/resend")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };
            var result = await _api.SendForResultAsync<object>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });
            if (result.Success)
            {
                return ResendEmailConfirmationOutcome.Accepted;
            }

            return result.StatusCode == HttpStatusCode.TooManyRequests
                ? ResendEmailConfirmationOutcome.RateLimited
                : ResendEmailConfirmationOutcome.Unavailable;
        }

        public async Task<AuthProvidersDto> GetProvidersAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/providers");
            var result = await _api.SendForResultAsync<AuthProvidersDto>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });

            return result.Success && result.Value is not null ? result.Value : new AuthProvidersDto();
        }

        public async Task<ExternalAuthClientResult> ExchangeExternalAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/external/exchange");
            var result = await _api.SendForResultAsync<ExternalAuthExchangeResponseDto>(
                request,
                new ApiRequestOptions { SuppressErrorNotifications = true });

            if (!result.Success || result.Value is null)
            {
                return result.StatusCode switch
                {
                    HttpStatusCode.Gone => new ExternalAuthClientResult(ExternalAuthClientOutcome.Expired),
                    HttpStatusCode.Forbidden => new ExternalAuthClientResult(ExternalAuthClientOutcome.Forbidden),
                    _ => new ExternalAuthClientResult(ExternalAuthClientOutcome.Unavailable)
                };
            }

            var value = result.Value;
            var outcome = value.Outcome switch
            {
                ExternalAuthOutcomes.Authenticated => ExternalAuthClientOutcome.Authenticated,
                ExternalAuthOutcomes.RegistrationRequired => ExternalAuthClientOutcome.RegistrationRequired,
                ExternalAuthOutcomes.EmailConflict => ExternalAuthClientOutcome.EmailConflict,
                ExternalAuthOutcomes.LinkCompleted => ExternalAuthClientOutcome.LinkCompleted,
                _ => ExternalAuthClientOutcome.Unavailable
            };

            if (outcome == ExternalAuthClientOutcome.Authenticated && !await AcceptTokenAsync(value.Token))
            {
                outcome = ExternalAuthClientOutcome.Unavailable;
            }

            return new ExternalAuthClientResult(
                outcome,
                value.Email,
                value.SuggestedUserName,
                value.ReturnUrl,
                value.Provider);
        }

        public async Task<LoginOutcome> CompleteExternalRegistrationAsync(ExternalAuthRegistrationDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/external/register")
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
                    HttpStatusCode.Forbidden => LoginOutcome.Forbidden,
                    HttpStatusCode.TooManyRequests => LoginOutcome.RateLimited,
                    _ => LoginOutcome.Unavailable
                };
            }

            return result.Value?.IsSuccess == true && await AcceptTokenAsync(result.Value.Token)
                ? LoginOutcome.Success
                : LoginOutcome.Unavailable;
        }

        public async Task<ExternalAuthStartDto?> BeginExternalLinkAsync(string provider, string returnUrl = "/account/sign-in-methods")
        {
            var providerSlug = NormalizeProviderSlug(provider);
            if (providerSlug is null)
            {
                return null;
            }

            var path = $"api/auth/{providerSlug}/link-intent?returnUrl={Uri.EscapeDataString(returnUrl)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            var result = await _api.SendForResultAsync<ExternalAuthStartDto>(request);
            return result.Success ? result.Value : null;
        }

        public async Task<SignInMethodsDto?> GetSignInMethodsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/sign-in-methods");
            return await _api.SendAsync<SignInMethodsDto>(request);
        }

        public async Task<SignInMethodsDto?> SetPasswordAsync(SetPasswordDto dto)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/password/set")
            {
                Content = ApiClient.CreateJsonContent(dto)
            };
            return await _api.SendAsync<SignInMethodsDto>(request);
        }

        public async Task<SignInMethodsDto?> DisconnectExternalAsync(string provider)
        {
            var providerSlug = NormalizeProviderSlug(provider);
            if (providerSlug is null)
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/auth/{providerSlug}");
            return await _api.SendAsync<SignInMethodsDto>(request);
        }

        private static string? NormalizeProviderSlug(string provider)
        {
            if (string.Equals(provider, ExternalAuthProviderNames.Google, StringComparison.OrdinalIgnoreCase))
            {
                return "google";
            }

            return string.Equals(provider, ExternalAuthProviderNames.Discord, StringComparison.OrdinalIgnoreCase)
                ? "discord"
                : null;
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

        private async Task<bool> AcceptTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            await _tokenStore.SetTokenAsync(token);
            _authStateProvider.NotifyUserAuthentication(token);
            return true;
        }
    }
}
