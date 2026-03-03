using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers
{
    [ApiController]
    [Route("api/admin/audit")]
    [Authorize(Policy = "RequireAdmin")]
    public class AdminAuditController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminAuditController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ModerationActionDto>>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _db.ModerationActions.AsNoTracking().OrderByDescending(x => x.Timestamp);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new ModerationActionDto
                {
                    Id = x.Id,
                    ActorUserId = x.ActorUserId,
                    Action = x.Action,
                    TargetType = x.TargetType,
                    TargetId = x.TargetId,
                    Reason = x.Reason,
                    OldValueJson = x.OldValueJson,
                    NewValueJson = x.NewValueJson,
                    Timestamp = x.Timestamp
                }).ToListAsync();
            return Ok(new PagedResult<ModerationActionDto> { Items = items, TotalCount = total, PageNumber = page, PageSize = pageSize });
        }
    }
}
