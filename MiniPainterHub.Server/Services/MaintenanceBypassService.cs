using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Extensions;
using Microsoft.AspNetCore.Http;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Services
{
    public sealed class MaintenanceBypassService : IMaintenanceBypassService
    {
        private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(15);
        private readonly ITimeLimitedDataProtector _protector;

        public MaintenanceBypassService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider
                .CreateProtector("MiniPainterHub", "MaintenanceBypass")
                .ToTimeLimitedDataProtector();
        }

        public string CookieName => "mph-maint-bypass";

        public void AppendCookie(HttpResponse response, string userId)
        {
            var token = _protector.Protect(userId, CookieLifetime);
            response.Cookies.Append(CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = response.HttpContext.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.Add(CookieLifetime),
                Path = "/"
            });
        }

        public void ClearCookie(HttpResponse response)
        {
            response.Cookies.Delete(CookieName, new CookieOptions
            {
                Path = "/"
            });
        }

        public bool TryValidate(HttpRequest request, out string userId)
        {
            userId = string.Empty;
            if (!request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                userId = _protector.Unprotect(token);
                return !string.IsNullOrWhiteSpace(userId);
            }
            catch
            {
                return false;
            }
        }
    }
}
