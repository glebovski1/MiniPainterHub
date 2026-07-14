using MiniPainterHub.Common.Auth;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IExternalAuthenticationService
{
    Task<string> CreateExchangeAsync(ExternalIdentity identity, string purpose, string? targetUserId, string returnUrl);
    Task<ExternalAuthExchangeResponseDto> ExchangeAsync(string rawHandle);
    Task<AuthResponseDto> RegisterAsync(string rawHandle, ExternalAuthRegistrationDto request);
    Task<SignInMethodsDto> GetSignInMethodsAsync(string userId);
    Task<SignInMethodsDto> SetPasswordAsync(string userId, SetPasswordDto request);
    Task<SignInMethodsDto> DisconnectGoogleAsync(string userId);
}

public sealed record ExternalIdentity(
    string Provider,
    string ProviderSubject,
    string VerifiedEmail,
    string? SuggestedDisplayName);
