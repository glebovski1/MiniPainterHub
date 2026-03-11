using System;
using System.Net;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;

namespace MiniPainterHub.Server.Middleware
{
    public sealed class MaintenanceModeMiddleware
    {
        private const string DefaultMessage = "MiniPainterHub is temporarily unavailable while maintenance is in progress.";
        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<MaintenanceOptions> _options;
        private readonly IMaintenanceBypassService _bypassService;
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ILogger<MaintenanceModeMiddleware> _logger;

        public MaintenanceModeMiddleware(
            RequestDelegate next,
            IOptionsMonitor<MaintenanceOptions> options,
            IMaintenanceBypassService bypassService,
            IProblemDetailsService problemDetailsService,
            ILogger<MaintenanceModeMiddleware> logger)
        {
            _next = next;
            _options = options;
            _bypassService = bypassService;
            _problemDetailsService = problemDetailsService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled || IsExemptPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (options.AllowAdmins && _bypassService.TryValidate(context.Request, out _))
            {
                await _next(context);
                return;
            }

            _logger.LogInformation("Maintenance mode intercepted request for {Path}.", context.Request.Path);

            if (AcceptsHtml(context.Request))
            {
                await WriteHtmlAsync(context, options);
                return;
            }

            await WriteProblemAsync(context, options);
        }

        private static bool IsExemptPath(PathString path) =>
            path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/maintenance-bypass", StringComparison.OrdinalIgnoreCase);

        private static bool AcceptsHtml(HttpRequest request)
        {
            var accept = request.Headers.Accept.ToString();
            return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        private async Task WriteProblemAsync(HttpContext context, MaintenanceOptions options)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            var problemDetails = new ProblemDetails
            {
                Title = "Maintenance mode",
                Detail = string.IsNullOrWhiteSpace(options.Message) ? DefaultMessage : options.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            };

            if (options.PlannedEndUtc.HasValue)
            {
                problemDetails.Extensions["plannedEndUtc"] = options.PlannedEndUtc.Value;
            }

            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails
            });
        }

        private static async Task WriteHtmlAsync(HttpContext context, MaintenanceOptions options)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";

            var message = HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(options.Message) ? DefaultMessage : options.Message);
            var plannedEnd = options.PlannedEndUtc.HasValue
                ? $"<p><strong>Planned end:</strong> {WebUtility.HtmlEncode(options.PlannedEndUtc.Value.ToUniversalTime().ToString("u"))}</p>"
                : string.Empty;
            var adminSection = options.AllowAdmins
                ? """
                <div class="admin-tools">
                    <button id="admin-access" type="button">Admin access</button>
                    <p id="admin-status"></p>
                </div>
                <script>
                (() => {
                    const button = document.getElementById('admin-access');
                    const status = document.getElementById('admin-status');
                    const token = window.localStorage ? window.localStorage.getItem('authToken') : null;
                    if (!token) {
                        button.disabled = true;
                        status.textContent = 'Admin access requires an existing admin session in this browser.';
                        return;
                    }

                    button.addEventListener('click', async () => {
                        button.disabled = true;
                        status.textContent = 'Requesting admin access...';
                        try {
                            const response = await fetch('/api/auth/maintenance-bypass', {
                                method: 'POST',
                                headers: {
                                    'Authorization': `Bearer ${token}`
                                }
                            });

                            if (!response.ok) {
                                status.textContent = 'Admin bypass failed. Verify that this browser already has an admin session.';
                                button.disabled = false;
                                return;
                            }

                            window.location.reload();
                        } catch {
                            status.textContent = 'Admin bypass failed. Check connectivity and try again.';
                            button.disabled = false;
                        }
                    });
                })();
                </script>
                """
                : string.Empty;

            var html =
                "<!DOCTYPE html>" +
                "<html lang=\"en\">" +
                "<head>" +
                "<meta charset=\"utf-8\" />" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />" +
                "<title>Maintenance mode</title>" +
                "<style>" +
                "body { font-family: Segoe UI, sans-serif; margin: 0; background: #f4f6f8; color: #1f2933; }" +
                "main { max-width: 640px; margin: 8vh auto; background: white; padding: 2rem; border-radius: 16px; box-shadow: 0 20px 45px rgba(15, 23, 42, 0.12); }" +
                "h1 { margin-top: 0; font-size: 2rem; }" +
                "p { line-height: 1.6; }" +
                ".admin-tools { margin-top: 2rem; }" +
                "button { padding: 0.75rem 1rem; border: 0; border-radius: 10px; background: #0d6efd; color: white; cursor: pointer; }" +
                "button:disabled { opacity: 0.6; cursor: default; }" +
                "#admin-status { margin-top: 0.75rem; color: #52606d; min-height: 1.25rem; }" +
                "</style>" +
                "</head>" +
                "<body>" +
                "<main>" +
                "<h1>Maintenance mode</h1>" +
                $"<p>{message}</p>" +
                plannedEnd +
                adminSection +
                "</main>" +
                "</body>" +
                "</html>";

            await context.Response.WriteAsync(html);
        }
    }
}
