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
[Authorize]
[Route("api/support/tickets")]
public sealed class SupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _supportTicketService;

    public SupportTicketsController(ISupportTicketService supportTicketService)
    {
        _supportTicketService = supportTicketService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupportTicketSummaryDto>>> GetTickets([FromQuery] SupportTicketQueryDto query)
    {
        var result = await _supportTicketService.GetForUserAsync(User.GetUserIdOrThrow(), query);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<SupportUnreadCountDto>> GetUnreadCount()
    {
        var result = await _supportTicketService.GetUnreadCountAsync(User.GetUserIdOrThrow());
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupportTicketDto>> GetTicket(int id)
    {
        var result = await _supportTicketService.GetForUserAsync(User.GetUserIdOrThrow(), id);
        return Ok(result);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<SupportTicketDto>> Create([FromBody] CreateSupportTicketDto request)
    {
        var result = await _supportTicketService.CreateAsync(User.GetUserIdOrThrow(), request);
        return CreatedAtAction(nameof(GetTicket), new { id = result.Id }, result);
    }

    [HttpPost("{id:int}/messages")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<ActionResult<SupportTicketDto>> Reply(int id, [FromBody] CreateSupportTicketMessageDto request)
    {
        var result = await _supportTicketService.ReplyAsUserAsync(User.GetUserIdOrThrow(), id, request);
        return Ok(result);
    }

    [HttpPost("{id:int}/read")]
    [EnableRateLimiting(RateLimitingPolicies.Write)]
    public async Task<IActionResult> MarkRead(int id, [FromBody] MarkSupportTicketReadDto request)
    {
        await _supportTicketService.MarkReadAsync(User.GetUserIdOrThrow(), id, request);
        return NoContent();
    }
}
