using System;
using System.Security.Claims;

namespace MiniPainterHub.Server.Identity
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserIdOrThrow(this ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("No user ID in token.");
            }

            return userId;
        }
    }
}
