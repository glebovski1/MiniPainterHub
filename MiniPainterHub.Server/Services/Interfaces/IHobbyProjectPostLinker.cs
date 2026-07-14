using MiniPainterHub.Server.Entities;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IHobbyProjectPostLinker
{
    Task LinkNewPostAsync(string userId, Post post, int projectId, string? milestoneLabel);
    Task RollbackNewPostAsync(Post post);
}
