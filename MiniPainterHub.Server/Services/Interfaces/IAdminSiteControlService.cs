using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IAdminSiteControlService
    {
        Task<IReadOnlyList<AdminSiteControlDto>> GetControlsAsync();
        Task<AdminSiteControlDto> GetControlAsync(string key);
        Task<AdminSiteControlDto> UpdateControlAsync(string key, UpdateAdminSiteControlRequestDto request, string actorUserId);
        Task<bool> IsEnabledAsync(string key);
    }
}
