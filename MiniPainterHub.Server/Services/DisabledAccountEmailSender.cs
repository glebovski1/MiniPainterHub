using MiniPainterHub.Server.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class DisabledAccountEmailSender : IAccountEmailSender
{
    public Task SendConfirmationAsync(
        string recipientAddress,
        string confirmationLink,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
