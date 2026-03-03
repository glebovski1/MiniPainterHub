using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class UserAccessService : IUserAccessService
    {
        private readonly IUserModerationService _userModeration;
        private readonly IFeatureFlagsService _flags;

        public UserAccessService(IUserModerationService userModeration, IFeatureFlagsService flags)
        {
            _userModeration = userModeration;
            _flags = flags;
        }

        public async Task EnsureCanPostAsync(string userId, bool includesImages)
        {
            if (!await _flags.GetFlagAsync("PostingEnabled", true))
            {
                throw new ForbiddenException("Posting is currently disabled.");
            }

            if (includesImages && !await _flags.GetFlagAsync("ImageUploadEnabled", true))
            {
                throw new ForbiddenException("Image upload is currently disabled.");
            }

            var restriction = await _userModeration.GetOrDefaultAsync(userId);
            if (!restriction.CanPost)
            {
                throw new ForbiddenException("User is restricted from posting.");
            }

            if (includesImages && !restriction.CanPostImages)
            {
                throw new ForbiddenException("User is restricted from image uploads.");
            }
        }

        public async Task EnsureCanCommentAsync(string userId)
        {
            var restriction = await _userModeration.GetOrDefaultAsync(userId);
            if (!restriction.CanComment)
            {
                throw new ForbiddenException("User is restricted from commenting.");
            }
        }

        public async Task EnsureCanLoginAsync(string userId, bool isAdmin)
        {
            if (!isAdmin && !await _flags.GetFlagAsync("LoginEnabled", true))
            {
                throw new ForbiddenException("Login is currently disabled.");
            }

            var restriction = await _userModeration.GetOrDefaultAsync(userId);
            if (restriction.IsSuspended)
            {
                throw new ForbiddenException("User account is suspended.");
            }
        }
    }
}
