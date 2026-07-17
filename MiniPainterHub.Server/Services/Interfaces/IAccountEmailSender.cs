using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IAccountEmailSender
{
    Task SendConfirmationAsync(string recipientAddress, string confirmationLink, CancellationToken cancellationToken = default);
}
