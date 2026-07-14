using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Auth;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginOutcome> LoginAsync(LoginDto dto);
        Task<bool> RegisterAsync(RegisterDto dto);
        Task<AuthProvidersDto> GetProvidersAsync();
        Task<ExternalAuthClientResult> ExchangeExternalAsync();
        Task<LoginOutcome> CompleteExternalRegistrationAsync(ExternalAuthRegistrationDto dto);
        Task<ExternalAuthStartDto?> BeginGoogleLinkAsync(string returnUrl = "/account/sign-in-methods");
        Task<SignInMethodsDto?> GetSignInMethodsAsync();
        Task<SignInMethodsDto?> SetPasswordAsync(SetPasswordDto dto);
        Task<SignInMethodsDto?> DisconnectGoogleAsync();
        Task LogoutAsync();
    }
}
