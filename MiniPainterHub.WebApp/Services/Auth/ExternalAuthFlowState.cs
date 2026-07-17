using MiniPainterHub.Common.Auth;

namespace MiniPainterHub.WebApp.Services.Auth;

public sealed class ExternalAuthFlowState
{
    public bool RegistrationPending { get; private set; }
    public string? Email { get; private set; }
    public string? SuggestedUserName { get; private set; }
    public string ReturnUrl { get; private set; } = "/";
    public string Provider { get; private set; } = ExternalAuthProviderNames.Google;

    public void BeginRegistration(ExternalAuthClientResult result)
    {
        RegistrationPending = true;
        Email = result.Email;
        SuggestedUserName = result.SuggestedUserName;
        ReturnUrl = result.ReturnUrl;
        Provider = NormalizeProvider(result.Provider);
    }

    public void Clear()
    {
        RegistrationPending = false;
        Email = null;
        SuggestedUserName = null;
        ReturnUrl = "/";
        Provider = ExternalAuthProviderNames.Google;
    }

    private static string NormalizeProvider(string? provider) =>
        string.Equals(provider, ExternalAuthProviderNames.Discord, StringComparison.OrdinalIgnoreCase)
            ? ExternalAuthProviderNames.Discord
            : ExternalAuthProviderNames.Google;
}
