using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class AzureCommunicationAccountEmailSender : IAccountEmailSender
{
    private readonly EmailClient _client;
    private readonly EmailConfirmationOptions _options;

    public AzureCommunicationAccountEmailSender(EmailClient client, IOptions<EmailConfirmationOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task SendConfirmationAsync(
        string recipientAddress,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var encodedLink = WebUtility.HtmlEncode(confirmationLink);
        var encodedSenderName = WebUtility.HtmlEncode(_options.SenderDisplayName);
        var content = new EmailContent("Confirm your Roll & Paint email")
        {
            PlainText = $"Confirm your Roll & Paint account by opening this link within 24 hours:\n\n{confirmationLink}\n\nIf you did not create this account, you can ignore this email.\n\n{_options.SenderDisplayName}",
            Html = $"<p>Confirm your Roll &amp; Paint account by opening the link below within 24 hours.</p><p><a href=\"{encodedLink}\">Confirm my email</a></p><p>If you did not create this account, you can ignore this email.</p><p>{encodedSenderName}</p>"
        };
        var message = new EmailMessage(
            _options.SenderAddress!,
            new EmailRecipients([new EmailAddress(recipientAddress)]),
            content);

        await _client.SendAsync(WaitUntil.Started, message, cancellationToken);
    }
}
