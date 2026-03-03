using Microsoft.AspNetCore.Http;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Middleware
{
    public class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IFeatureFlagsService flags)
        {
            var siteOnline = await flags.GetFlagAsync("SiteOnline", true);
            if (siteOnline)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value ?? string.Empty;
            var isAdminPath = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase);
            var isAdminUser = context.User.Identity?.IsAuthenticated == true
                && context.User.IsInRole("Admin");

            if (isAdminPath || isAdminUser)
            {
                await _next(context);
                return;
            }

            var accept = context.Request.Headers.Accept.ToString();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            if (accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<html><body><h1>Maintenance</h1><p>The site is temporarily offline.</p></body></html>");
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Service unavailable", status = 503 }));
        }
    }
}
