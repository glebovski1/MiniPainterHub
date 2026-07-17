using MiniPainterHub.Server.Identity;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IEmailConfirmationService
{
    Task<bool> SendConfirmationAsync(ApplicationUser user, string? returnUrl, CancellationToken cancellationToken = default);
    Task<bool> ConfirmAsync(string userId, string encodedCode);
    Task ResendAsync(string email, CancellationToken cancellationToken = default);
}
