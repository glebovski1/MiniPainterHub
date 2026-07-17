namespace MiniPainterHub.WebApp.Services.Auth;

public enum ExternalAuthClientOutcome
{
    Authenticated,
    RegistrationRequired,
    EmailConflict,
    LinkCompleted,
    Expired,
    Forbidden,
    Unavailable
}

public sealed record ExternalAuthClientResult(
    ExternalAuthClientOutcome Outcome,
    string? Email = null,
    string? SuggestedUserName = null,
    string ReturnUrl = "/",
    string? Provider = null);
