namespace MiniPainterHub.WebApp.Services.Auth;

public enum RegistrationOutcome
{
    Success,
    ConfirmationSent,
    ConfirmationPendingDelivery,
    ValidationFailure,
    RateLimited,
    Unavailable
}

public enum EmailConfirmationOutcome
{
    Success,
    InvalidOrExpired,
    RateLimited,
    Unavailable
}

public enum ResendEmailConfirmationOutcome
{
    Accepted,
    RateLimited,
    Unavailable
}
