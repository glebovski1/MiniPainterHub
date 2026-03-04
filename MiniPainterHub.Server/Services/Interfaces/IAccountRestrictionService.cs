using MiniPainterHub.Server.Identity;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IAccountRestrictionService
    {
        Task EnsureCanLoginAsync(ApplicationUser user);
        Task EnsureCanCreatePostAsync(string userId);
        Task EnsureCanCommentAsync(string userId);
    }
}
