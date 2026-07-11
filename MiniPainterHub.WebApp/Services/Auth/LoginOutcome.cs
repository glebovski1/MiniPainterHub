namespace MiniPainterHub.WebApp.Services.Auth;

public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    ValidationFailure,
    Forbidden,
    RateLimited,
    Unavailable
}
