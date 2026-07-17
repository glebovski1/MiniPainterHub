using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.Auth;

public sealed class RegistrationResultDto
{
    public bool IsSuccess { get; set; }
    public bool RequiresEmailConfirmation { get; set; }
    public bool ConfirmationEmailSent { get; set; }
}

public sealed class ConfirmEmailDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}

public sealed class ResendEmailConfirmationDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
