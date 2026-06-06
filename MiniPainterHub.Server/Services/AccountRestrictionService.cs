using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Common.DTOs;
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
        private readonly IAdminSiteControlService? _siteControlService;

        public AccountRestrictionService(
            UserManager<ApplicationUser> userManager,
            IAdminSiteControlService? siteControlService = null)
        {
            _userManager = userManager;
            _siteControlService = siteControlService;
        }

        public Task EnsureCanLoginAsync(ApplicationUser user)
        {
            EnsureNotSuspended(user);
            return Task.CompletedTask;
        }

        public Task EnsureCanRegisterAsync() =>
            EnsureControlEnabledAsync(AdminSiteControlKeys.NewRegistrations, "New registrations are temporarily disabled.");

        public async Task EnsureCanCreatePostAsync(string userId)
        {
            await EnsureControlEnabledAsync(AdminSiteControlKeys.NewPosts, "New posts and uploads are temporarily disabled.");
            var user = await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User must be authenticated to create posts.");
            EnsureNotSuspended(user);
        }

        public async Task EnsureCanCommentAsync(string userId)
        {
            await EnsureControlEnabledAsync(AdminSiteControlKeys.NewComments, "New comments are temporarily disabled.");
            var user = await _userManager.FindByIdAsync(userId) ?? throw new UnauthorizedAccessException("User must be authenticated to comment.");
            EnsureNotSuspended(user);
        }

        private async Task EnsureControlEnabledAsync(string key, string fallbackMessage)
        {
            if (_siteControlService is null)
            {
                return;
            }

            var control = await _siteControlService.GetControlAsync(key);
            if (!control.EffectiveEnabled)
            {
                throw new ForbiddenException(string.IsNullOrWhiteSpace(control.Message) ? fallbackMessage : control.Message);
            }
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
