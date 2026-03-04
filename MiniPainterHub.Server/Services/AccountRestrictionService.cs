using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class AccountRestrictionService : IAccountRestrictionService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountRestrictionService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public Task EnsureCanLoginAsync(ApplicationUser user)
        {
            EnsureNotSuspended(user);
            return Task.CompletedTask;
        }

        public async Task EnsureCanCreatePostAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            EnsureNotSuspended(user);
        }

        public async Task EnsureCanCommentAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User must be authenticated to comment.");
            EnsureNotSuspended(user);
        }

        private static void EnsureNotSuspended(ApplicationUser user)
        {
            if (user.SuspendedUntilUtc.HasValue && user.SuspendedUntilUtc.Value > DateTime.UtcNow)
            {
                throw new ForbiddenException("Your account is suspended.");
            }
        }
    }
}
