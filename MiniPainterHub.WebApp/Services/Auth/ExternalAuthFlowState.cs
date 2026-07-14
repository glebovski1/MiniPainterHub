namespace MiniPainterHub.WebApp.Services.Auth;

public sealed class ExternalAuthFlowState
{
    public bool RegistrationPending { get; private set; }
    public string? Email { get; private set; }
    public string? SuggestedUserName { get; private set; }
    public string ReturnUrl { get; private set; } = "/";

    public void BeginRegistration(ExternalAuthClientResult result)
    {
        RegistrationPending = true;
        Email = result.Email;
        SuggestedUserName = result.SuggestedUserName;
        ReturnUrl = result.ReturnUrl;
    }

    public void Clear()
    {
        RegistrationPending = false;
        Email = null;
        SuggestedUserName = null;
        ReturnUrl = "/";
    }
}
