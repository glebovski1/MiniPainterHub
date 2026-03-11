using Microsoft.AspNetCore.Http;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IMaintenanceBypassService
    {
        string CookieName { get; }
        void AppendCookie(HttpResponse response, string userId);
        void ClearCookie(HttpResponse response);
        bool TryValidate(HttpRequest request, out string userId);
    }
}
