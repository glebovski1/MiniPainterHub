namespace MiniPainterHub.Server.Infrastructure.RateLimiting;

public static class RateLimitingPolicies
{
    public const string Auth = "auth";
    public const string EmailConfirmation = "email-confirmation";
    public const string Search = "search";
    public const string Write = "write";
    public const string Upload = "upload";
    public const string Realtime = "realtime";
}
