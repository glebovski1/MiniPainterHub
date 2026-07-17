using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class DevelopmentAccountEmailSender : IAccountEmailSender
{
    private readonly ILogger<DevelopmentAccountEmailSender> _logger;

    public DevelopmentAccountEmailSender(ILogger<DevelopmentAccountEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationAsync(
        string recipientAddress,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development email confirmation for {RecipientAddress}: {ConfirmationLink}",
            recipientAddress,
            confirmationLink);
        return Task.CompletedTask;
    }
}
