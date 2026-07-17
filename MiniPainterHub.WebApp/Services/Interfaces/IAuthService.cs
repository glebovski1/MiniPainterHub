using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Auth;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginOutcome> LoginAsync(LoginDto dto);
        Task<RegistrationOutcome> RegisterAsync(RegisterDto dto);
        Task<EmailConfirmationOutcome> ConfirmEmailAsync(ConfirmEmailDto dto);
        Task<ResendEmailConfirmationOutcome> ResendEmailConfirmationAsync(ResendEmailConfirmationDto dto);
        Task<AuthProvidersDto> GetProvidersAsync();
        Task<ExternalAuthClientResult> ExchangeExternalAsync();
        Task<LoginOutcome> CompleteExternalRegistrationAsync(ExternalAuthRegistrationDto dto);
        Task<ExternalAuthStartDto?> BeginExternalLinkAsync(string provider, string returnUrl = "/account/sign-in-methods");
        Task<SignInMethodsDto?> GetSignInMethodsAsync();
        Task<SignInMethodsDto?> SetPasswordAsync(SetPasswordDto dto);
        Task<SignInMethodsDto?> DisconnectExternalAsync(string provider);
        Task LogoutAsync();
    }
}
