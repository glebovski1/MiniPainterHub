using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IUserAccessService
    {
        Task EnsureCanPostAsync(string userId, bool includesImages);
        Task EnsureCanCommentAsync(string userId);
        Task EnsureCanLoginAsync(string userId, bool isAdmin);
    }
}
