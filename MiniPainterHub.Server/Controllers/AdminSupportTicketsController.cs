using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Infrastructure.RateLimiting;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/support/tickets")]
public sealed class AdminSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _supportTicketService;

    public AdminSupportTicketsController(ISupportTicketService supportTicketService)
    {
        _supportTicketService = supportTicketService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupportTicketSummaryDto>>> GetTickets([FromQuery] SupportTicketQueryDto query)
    {
        var result = await _supportTicketService.GetForAdminAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupportTicketDto>> GetTicket(int id)
    {
        var result = await _supportTicketService.GetForAdminAsync(User.GetUserIdOrThrow(), id);
        return Ok(result);
    }

    [HttpPost("{id:int}/messages")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<SupportTicketDto>> Reply(int id, [FromBody] CreateSupportTicketMessageDto request)
    {
        var result = await _supportTicketService.ReplyAsAdminAsync(User.GetUserIdOrThrow(), id, request);
        return Ok(result);
    }

    [HttpPut("{id:int}/status")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<SupportTicketDto>> UpdateStatus(int id, [FromBody] UpdateSupportTicketStatusDto request)
    {
        var result = await _supportTicketService.UpdateStatusAsAdminAsync(User.GetUserIdOrThrow(), id, request);
        return Ok(result);
    }
}
