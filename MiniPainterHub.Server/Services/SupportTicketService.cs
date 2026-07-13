using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Features.Pagination;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services;

public sealed class SupportTicketService : ISupportTicketService
{
    private const int MaxSearchLength = 200;
    private const int PreviewLength = 120;
    private readonly AppDbContext _dbContext;

    public SupportTicketService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PagedResult<SupportTicketSummaryDto>> GetForUserAsync(string userId, SupportTicketQueryDto query) =>
        GetTicketsAsync(query, userId);

    public Task<PagedResult<SupportTicketSummaryDto>> GetForAdminAsync(SupportTicketQueryDto query) =>
        GetTicketsAsync(query, requesterUserId: null);

    public async Task<SupportTicketDto> GetForUserAsync(string userId, int ticketId)
    {
        var ticket = await LoadTicketAsync(ticketId, userId)
            ?? throw new NotFoundException("Support ticket not found.");

        return MapDetails(ticket, userId);
    }

    public async Task<SupportTicketDto> GetForAdminAsync(string currentUserId, int ticketId)
    {
        var ticket = await LoadTicketAsync(ticketId)
            ?? throw new NotFoundException("Support ticket not found.");

        return MapDetails(ticket, currentUserId);
    }

    public async Task<SupportTicketDto> CreateAsync(string userId, CreateSupportTicketDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = NormalizeCategory(request.Category, required: true)!;
        var subject = NormalizeRequiredText(request.Subject, nameof(request.Subject), SupportTicketRules.MaxSubjectLength);
        var body = NormalizeRequiredText(request.Message, nameof(request.Message), SupportTicketRules.MaxMessageLength);

        if (!await _dbContext.Users.AnyAsync(user => user.Id == userId))
        {
            throw new NotFoundException("User not found.");
        }

        var utcNow = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            RequesterUserId = userId,
            Category = category,
            Subject = subject,
            Status = SupportTicketStatuses.New,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            RequesterReadUtc = utcNow,
            Messages = new List<SupportTicketMessage>
            {
                new()
                {
                    AuthorUserId = userId,
                    Body = body,
                    SentUtc = utcNow,
                    IsStaffReply = false
                }
            }
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        return await GetForUserAsync(userId, ticket.Id);
    }

    public async Task<SupportTicketDto> ReplyAsUserAsync(
        string userId,
        int ticketId,
        CreateSupportTicketMessageDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = NormalizeRequiredText(request.Body, nameof(request.Body), SupportTicketRules.MaxMessageLength);
        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == ticketId && item.RequesterUserId == userId)
            ?? throw new NotFoundException("Support ticket not found.");

        var utcNow = DateTime.UtcNow;
        ticket.Messages.Add(new SupportTicketMessage
        {
            AuthorUserId = userId,
            Body = body,
            SentUtc = utcNow,
            IsStaffReply = false
        });
        ticket.Status = SupportTicketStatuses.WaitingForAdmin;
        ticket.UpdatedUtc = utcNow;
        ticket.ResolvedUtc = null;

        await _dbContext.SaveChangesAsync();
        return await GetForUserAsync(userId, ticketId);
    }

    public async Task<SupportTicketDto> ReplyAsAdminAsync(
        string adminUserId,
        int ticketId,
        CreateSupportTicketMessageDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = NormalizeRequiredText(request.Body, nameof(request.Body), SupportTicketRules.MaxMessageLength);
        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(item => item.Id == ticketId)
            ?? throw new NotFoundException("Support ticket not found.");

        if (string.Equals(ticket.Status, SupportTicketStatuses.Resolved, StringComparison.Ordinal))
        {
            throw Validation("status", "Resolved tickets can only be reopened by the requester.");
        }

        if (!await _dbContext.Users.AnyAsync(user => user.Id == adminUserId))
        {
            throw new NotFoundException("Admin user not found.");
        }

        var utcNow = DateTime.UtcNow;
        ticket.Messages.Add(new SupportTicketMessage
        {
            AuthorUserId = adminUserId,
            Body = body,
            SentUtc = utcNow,
            IsStaffReply = true
        });
        ticket.Status = SupportTicketStatuses.WaitingForUser;
        ticket.UpdatedUtc = utcNow;
        ticket.LastStaffReplyUtc = utcNow;

        await _dbContext.SaveChangesAsync();
        return await GetForAdminAsync(adminUserId, ticketId);
    }

    public async Task<SupportTicketDto> UpdateStatusAsAdminAsync(
        string adminUserId,
        int ticketId,
        UpdateSupportTicketStatusDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = NormalizeStatus(request.Status, required: true)!;
        if (!string.Equals(status, SupportTicketStatuses.Resolved, StringComparison.Ordinal))
        {
            throw Validation("status", "Admins can only resolve an active support ticket.");
        }

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(item => item.Id == ticketId)
            ?? throw new NotFoundException("Support ticket not found.");
        if (string.Equals(ticket.Status, SupportTicketStatuses.Resolved, StringComparison.Ordinal))
        {
            throw Validation("status", "The support ticket is already resolved.");
        }

        var utcNow = DateTime.UtcNow;
        ticket.Status = SupportTicketStatuses.Resolved;
        ticket.ResolvedUtc = utcNow;
        ticket.UpdatedUtc = utcNow;
        await _dbContext.SaveChangesAsync();

        return await GetForAdminAsync(adminUserId, ticketId);
    }

    public async Task MarkReadAsync(string userId, int ticketId, MarkSupportTicketReadDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticket = await _dbContext.SupportTickets
            .FirstOrDefaultAsync(item => item.Id == ticketId && item.RequesterUserId == userId)
            ?? throw new NotFoundException("Support ticket not found.");

        if (!request.LastStaffReplyUtc.HasValue)
        {
            throw Validation(nameof(request.LastStaffReplyUtc), "The observed staff reply timestamp is required.");
        }

        if (!ticket.LastStaffReplyUtc.HasValue)
        {
            return;
        }

        var acknowledgedUtc = request.LastStaffReplyUtc.Value <= ticket.LastStaffReplyUtc.Value
            ? request.LastStaffReplyUtc.Value
            : ticket.LastStaffReplyUtc.Value;
        if (!ticket.RequesterReadUtc.HasValue || acknowledgedUtc > ticket.RequesterReadUtc.Value)
        {
            ticket.RequesterReadUtc = acknowledgedUtc;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<SupportUnreadCountDto> GetUnreadCountAsync(string userId)
    {
        var count = await _dbContext.SupportTickets.CountAsync(ticket =>
            ticket.RequesterUserId == userId
            && ticket.LastStaffReplyUtc.HasValue
            && (!ticket.RequesterReadUtc.HasValue || ticket.LastStaffReplyUtc > ticket.RequesterReadUtc));

        return new SupportUnreadCountDto { Count = count };
    }

    private async Task<PagedResult<SupportTicketSummaryDto>> GetTicketsAsync(
        SupportTicketQueryDto query,
        string? requesterUserId)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateQuery(query, out var status, out var category, out var search);

        IQueryable<SupportTicket> tickets = _dbContext.SupportTickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUserId))
        {
            tickets = tickets.Where(ticket => ticket.RequesterUserId == requesterUserId);
        }

        if (status is not null)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (category is not null)
        {
            tickets = tickets.Where(ticket => ticket.Category == category);
        }

        if (search is not null)
        {
            tickets = tickets.Where(ticket =>
                ticket.Subject.Contains(search)
                || ticket.Messages.Any(message => message.Body.Contains(search))
                || (ticket.RequesterUser.UserName != null && ticket.RequesterUser.UserName.Contains(search))
                || (ticket.RequesterUser.DisplayName != null && ticket.RequesterUser.DisplayName.Contains(search))
                || (ticket.RequesterUser.Profile != null
                    && ticket.RequesterUser.Profile.DisplayName != null
                    && ticket.RequesterUser.Profile.DisplayName.Contains(search)));
        }

        var totalCount = await tickets.CountAsync();
        var pageItems = await tickets
            .OrderByDescending(ticket => ticket.UpdatedUtc)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ticket => new SupportTicketSummaryProjection(
                ticket.Id,
                ticket.Category,
                ticket.Subject,
                ticket.Status,
                ticket.CreatedUtc,
                ticket.UpdatedUtc,
                ticket.LastStaffReplyUtc,
                ticket.RequesterReadUtc,
                ticket.ResolvedUtc,
                ticket.Messages
                    .OrderByDescending(message => message.SentUtc)
                    .ThenByDescending(message => message.Id)
                    .Select(message => message.Body)
                    .FirstOrDefault(),
                ticket.RequesterUserId,
                ticket.RequesterUser.UserName,
                ticket.RequesterUser.Profile != null && ticket.RequesterUser.Profile.DisplayName != null
                    ? ticket.RequesterUser.Profile.DisplayName
                    : (ticket.RequesterUser.DisplayName ?? ticket.RequesterUser.UserName),
                ticket.RequesterUser.Profile != null && ticket.RequesterUser.Profile.AvatarUrl != null
                    ? ticket.RequesterUser.Profile.AvatarUrl
                    : ticket.RequesterUser.AvatarUrl))
            .ToListAsync();

        return new PagedResult<SupportTicketSummaryDto>
        {
            Items = pageItems.Select(MapSummary).ToList(),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    private Task<SupportTicket?> LoadTicketAsync(int ticketId, string? requesterUserId = null)
    {
        var tickets = _dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);

        if (!string.IsNullOrWhiteSpace(requesterUserId))
        {
            tickets = tickets.Where(ticket => ticket.RequesterUserId == requesterUserId);
        }

        return tickets
            .Include(ticket => ticket.RequesterUser)
            .ThenInclude(user => user.Profile)
            .Include(ticket => ticket.Messages)
            .ThenInclude(message => message.AuthorUser)
            .ThenInclude(user => user.Profile)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    private static SupportTicketDto MapDetails(SupportTicket ticket, string currentUserId)
    {
        var summary = MapSummary(ticket);
        return new SupportTicketDto
        {
            Id = summary.Id,
            Category = summary.Category,
            Subject = summary.Subject,
            Status = summary.Status,
            CreatedUtc = summary.CreatedUtc,
            UpdatedUtc = summary.UpdatedUtc,
            LastStaffReplyUtc = summary.LastStaffReplyUtc,
            RequesterReadUtc = summary.RequesterReadUtc,
            ResolvedUtc = summary.ResolvedUtc,
            HasUnreadStaffReply = summary.HasUnreadStaffReply,
            LatestMessagePreview = summary.LatestMessagePreview,
            RequesterUserId = summary.RequesterUserId,
            RequesterUserName = summary.RequesterUserName,
            RequesterDisplayName = summary.RequesterDisplayName,
            RequesterAvatarUrl = summary.RequesterAvatarUrl,
            Messages = ticket.Messages
                .OrderBy(message => message.SentUtc)
                .ThenBy(message => message.Id)
                .Select(message => MapMessage(message, currentUserId))
                .ToList()
        };
    }

    private static SupportTicketSummaryDto MapSummary(SupportTicket ticket)
    {
        var latestMessage = ticket.Messages
            .OrderByDescending(message => message.SentUtc)
            .ThenByDescending(message => message.Id)
            .First();

        return new SupportTicketSummaryDto
        {
            Id = ticket.Id,
            Category = ticket.Category,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedUtc = ticket.CreatedUtc,
            UpdatedUtc = ticket.UpdatedUtc,
            LastStaffReplyUtc = ticket.LastStaffReplyUtc,
            RequesterReadUtc = ticket.RequesterReadUtc,
            ResolvedUtc = ticket.ResolvedUtc,
            HasUnreadStaffReply = ticket.LastStaffReplyUtc.HasValue
                && (!ticket.RequesterReadUtc.HasValue || ticket.LastStaffReplyUtc > ticket.RequesterReadUtc),
            LatestMessagePreview = TrimPreview(latestMessage.Body),
            RequesterUserId = ticket.RequesterUserId,
            RequesterUserName = ticket.RequesterUser.UserName ?? string.Empty,
            RequesterDisplayName = ResolveDisplayName(ticket.RequesterUser),
            RequesterAvatarUrl = ticket.RequesterUser.Profile?.AvatarUrl ?? ticket.RequesterUser.AvatarUrl
        };
    }

    private static SupportTicketSummaryDto MapSummary(SupportTicketSummaryProjection ticket) =>
        new()
        {
            Id = ticket.Id,
            Category = ticket.Category,
            Subject = ticket.Subject,
            Status = ticket.Status,
            CreatedUtc = ticket.CreatedUtc,
            UpdatedUtc = ticket.UpdatedUtc,
            LastStaffReplyUtc = ticket.LastStaffReplyUtc,
            RequesterReadUtc = ticket.RequesterReadUtc,
            ResolvedUtc = ticket.ResolvedUtc,
            HasUnreadStaffReply = ticket.LastStaffReplyUtc.HasValue
                && (!ticket.RequesterReadUtc.HasValue || ticket.LastStaffReplyUtc > ticket.RequesterReadUtc),
            LatestMessagePreview = TrimPreview(ticket.LatestMessageBody ?? string.Empty),
            RequesterUserId = ticket.RequesterUserId,
            RequesterUserName = ticket.RequesterUserName ?? string.Empty,
            RequesterDisplayName = ticket.RequesterDisplayName
                ?? ticket.RequesterUserName
                ?? ticket.RequesterUserId,
            RequesterAvatarUrl = ticket.RequesterAvatarUrl
        };

    private static SupportTicketMessageDto MapMessage(SupportTicketMessage message, string currentUserId) =>
        new()
        {
            Id = message.Id,
            TicketId = message.TicketId,
            AuthorUserId = message.AuthorUserId,
            AuthorUserName = message.AuthorUser.UserName ?? string.Empty,
            AuthorDisplayName = ResolveDisplayName(message.AuthorUser),
            AuthorAvatarUrl = message.AuthorUser.Profile?.AvatarUrl ?? message.AuthorUser.AvatarUrl,
            Body = message.Body,
            SentUtc = message.SentUtc,
            IsStaffReply = message.IsStaffReply,
            IsMine = string.Equals(message.AuthorUserId, currentUserId, StringComparison.Ordinal)
        };

    private static string ResolveDisplayName(ApplicationUser user) =>
        user.Profile?.DisplayName
        ?? user.DisplayName
        ?? user.UserName
        ?? user.Id;

    private static string TrimPreview(string body) =>
        body.Length <= PreviewLength ? body : body[..(PreviewLength - 3)] + "...";

    private static void ValidateQuery(
        SupportTicketQueryDto query,
        out string? status,
        out string? category,
        out string? search)
    {
        var errors = new Dictionary<string, string[]>();
        PaginationGuard.AddErrors(errors, query.PageNumber, query.PageSize);

        status = NormalizeStatus(query.Status, required: false, errors);
        category = NormalizeCategory(query.Category, required: false, errors);
        search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search?.Length > MaxSearchLength)
        {
            errors["search"] = new[] { $"Search must be {MaxSearchLength} characters or fewer." };
        }

        PaginationGuard.ThrowIfInvalid(errors, "Support ticket query is invalid.");
    }

    private static string? NormalizeCategory(
        string? value,
        bool required,
        IDictionary<string, string[]>? errors = null)
    {
        return NormalizeChoice(
            value,
            SupportTicketCategories.All,
            "category",
            "Provide a valid support ticket category.",
            required,
            errors);
    }

    private static string? NormalizeStatus(
        string? value,
        bool required,
        IDictionary<string, string[]>? errors = null)
    {
        return NormalizeChoice(
            value,
            SupportTicketStatuses.All,
            "status",
            "Provide a valid support ticket status.",
            required,
            errors);
    }

    private static string? NormalizeChoice(
        string? value,
        IReadOnlyList<string> choices,
        string key,
        string message,
        bool required,
        IDictionary<string, string[]>? errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            if (required)
            {
                if (errors is not null)
                {
                    errors[key] = new[] { message };
                    return null;
                }

                throw Validation(key, message);
            }

            return null;
        }

        var canonical = choices.FirstOrDefault(choice =>
            string.Equals(choice, trimmed, StringComparison.OrdinalIgnoreCase));
        if (canonical is not null)
        {
            return canonical;
        }

        if (errors is not null)
        {
            errors[key] = new[] { message };
            return null;
        }

        throw Validation(key, message);
    }

    private static string NormalizeRequiredText(string? value, string key, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw Validation(key, $"{key} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw Validation(key, $"{key} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static DomainValidationException Validation(string key, string message) =>
        new("Support ticket data is invalid.", new Dictionary<string, string[]>
        {
            [key] = new[] { message }
        });

    private sealed record SupportTicketSummaryProjection(
        int Id,
        string Category,
        string Subject,
        string Status,
        DateTime CreatedUtc,
        DateTime UpdatedUtc,
        DateTime? LastStaffReplyUtc,
        DateTime? RequesterReadUtc,
        DateTime? ResolvedUtc,
        string? LatestMessageBody,
        string RequesterUserId,
        string? RequesterUserName,
        string? RequesterDisplayName,
        string? RequesterAvatarUrl);
}
