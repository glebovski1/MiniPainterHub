using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Services.Auth;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginOutcome> LoginAsync(LoginDto dto);
        Task<bool> RegisterAsync(RegisterDto dto);
        Task LogoutAsync();
    }
}
