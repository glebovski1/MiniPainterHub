using MiniPainterHub.Common.Auth;

namespace MiniPainterHub.WebApp.Services.Interfaces
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(LoginDto dto);
        Task<bool> RegisterAsync(RegisterDto dto);
        Task LogoutAsync();
    }
}
