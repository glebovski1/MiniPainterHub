using System;

namespace MiniPainterHub.Server.Entities;

public sealed class ExternalAuthExchange
{
    public Guid Id { get; set; }
    public string HandleHash { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderSubject { get; set; } = string.Empty;
    public string VerifiedEmail { get; set; } = string.Empty;
    public string? SuggestedDisplayName { get; set; }
    public string? TargetUserId { get; set; }
    public string Purpose { get; set; } = ExternalAuthPurposes.SignIn;
    public string ReturnUrl { get; set; } = "/";
    public DateTime ExpiresUtc { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public static class ExternalAuthPurposes
{
    public const string SignIn = "SignIn";
    public const string Link = "Link";
}
