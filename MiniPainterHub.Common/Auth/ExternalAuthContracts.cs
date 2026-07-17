using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.Auth;

public static class ExternalAuthOutcomes
{
    public const string Authenticated = "Authenticated";
    public const string RegistrationRequired = "RegistrationRequired";
    public const string EmailConflict = "EmailConflict";
    public const string LinkCompleted = "LinkCompleted";
}

public static class ExternalAuthRules
{
    public const int MinUserNameLength = 2;
    public const int MaxUserNameLength = 80;
}

public static class ExternalAuthProviderNames
{
    public const string Google = "Google";
    public const string Discord = "Discord";
}

public sealed class AuthProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class AuthProvidersDto
{
    public AuthProviderDto Google { get; set; } = new();
    public AuthProviderDto Discord { get; set; } = new();
    public string? SupportEmail { get; set; }
}

public sealed class ExternalAuthExchangeResponseDto
{
    public string Outcome { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Token { get; set; }
    public string? Email { get; set; }
    public string? SuggestedUserName { get; set; }
    public string ReturnUrl { get; set; } = "/";
}

public sealed class ExternalAuthRegistrationDto
{
    [Required]
    [StringLength(ExternalAuthRules.MaxUserNameLength, MinimumLength = ExternalAuthRules.MinUserNameLength)]
    public string UserName { get; set; } = string.Empty;
}

public sealed class ExternalAuthStartDto
{
    public string StartUrl { get; set; } = string.Empty;
}

public sealed class SignInMethodsDto
{
    public bool HasPassword { get; set; }
    public bool GoogleConnected { get; set; }
    public bool CanDisconnectGoogle { get; set; }
    public bool DiscordConnected { get; set; }
    public bool CanDisconnectDiscord { get; set; }
}

public sealed class SetPasswordDto
{
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
