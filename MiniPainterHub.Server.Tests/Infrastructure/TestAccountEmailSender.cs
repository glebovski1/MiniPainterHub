using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Tests.Infrastructure;

public sealed record SentAccountEmail(string RecipientAddress, string ConfirmationLink);

public sealed class TestAccountEmailSender : IAccountEmailSender
{
    private readonly ConcurrentQueue<SentAccountEmail> _messages = new();

    public bool FailSends { get; set; }
    public IReadOnlyCollection<SentAccountEmail> Messages => _messages.ToArray();

    public Task SendConfirmationAsync(
        string recipientAddress,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        if (FailSends)
        {
            throw new InvalidOperationException("Test email delivery failure.");
        }

        _messages.Enqueue(new SentAccountEmail(recipientAddress, confirmationLink));
        return Task.CompletedTask;
    }
}
