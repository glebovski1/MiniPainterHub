using Microsoft.AspNetCore.Http;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Middleware
{
    public sealed class SiteActivityMiddleware
    {
        private const string SessionCookieName = "mph_activity";
        private readonly RequestDelegate _next;

        public SiteActivityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISiteActivityTracker tracker)
        {
            var started = Stopwatch.GetTimestamp();
            var path = context.Request.Path.ToString();
            var isApi = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
            var isImageUpload = isApi
                && string.Equals(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase)
                && context.Request.Path.StartsWithSegments("/api/posts/with-image", StringComparison.OrdinalIgnoreCase);
            var isPageView = IsPageView(context.Request);
            var sessionKey = ResolveSessionKey(context, isApi || isPageView);

            try
            {
                await _next(context);
            }
            finally
            {
                tracker.RecordRequest(new SiteActivityRequest(
                    TimestampUtc: DateTime.UtcNow,
                    Path: path,
                    Method: context.Request.Method,
                    StatusCode: context.Response.StatusCode,
                    DurationMs: Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    SessionKey: sessionKey,
                    IsApi: isApi,
                    IsPageView: isPageView,
                    IsImageUpload: isImageUpload));
            }
        }

        private static string? ResolveSessionKey(HttpContext context, bool shouldTrackSession)
        {
            if (!shouldTrackSession)
            {
                return null;
            }

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return "user:" + userId;
            }

            if (context.Request.Cookies.TryGetValue(SessionCookieName, out var cookieValue)
                && Guid.TryParse(cookieValue, out _))
            {
                return "anon:" + cookieValue;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            });
            return "anon:" + sessionId;
        }

        private static bool IsPageView(HttpRequest request)
        {
            if (!HttpMethods.IsGet(request.Method))
            {
                return false;
            }

            if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                || request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
                || request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Path.GetExtension(request.Path.Value)))
            {
                return false;
            }

            var accept = request.Headers.Accept.ToString();
            return string.IsNullOrWhiteSpace(accept)
                || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
                || accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
        }
    }
}
