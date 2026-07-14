using MiniPainterHub.Server.Identity;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces;

public interface IJwtTokenIssuer
{
    Task<string> IssueAsync(ApplicationUser user);
}
