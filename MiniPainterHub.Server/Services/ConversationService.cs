using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services
{
    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _dbContext;
        private readonly IChatNotifier _chatNotifier;
        private readonly ILogger<ConversationService> _logger;

        public ConversationService(AppDbContext dbContext, IChatNotifier chatNotifier, ILogger<ConversationService> logger)
        {
            _dbContext = dbContext;
            _chatNotifier = chatNotifier;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(string userId)
        {
            var projections = await _dbContext.ConversationParticipants
                .AsNoTracking()
                .Where(cp => cp.UserId == userId)
                .Select(cp => new ConversationSummaryProjection(
                    cp.ConversationId,
                    cp.Conversation.Participants.Count(),
                    cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.UserId)
                        .FirstOrDefault(),
                    cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.UserName ?? string.Empty)
                        .FirstOrDefault(),
                    cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.Profile != null && p.User.Profile.DisplayName != null
                            ? p.User.Profile.DisplayName
                            : (p.User.UserName ?? string.Empty))
                        .FirstOrDefault(),
                    cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.Profile != null
                            ? p.User.Profile.AvatarUrl
                            : p.User.AvatarUrl)
                        .FirstOrDefault(),
                    cp.Conversation.Messages
                        .OrderByDescending(m => m.SentUtc)
                        .Select(m => m.Body)
                        .FirstOrDefault(),
                    cp.Conversation.Messages
                        .OrderByDescending(m => m.SentUtc)
                        .Select(m => m.SenderUserId)
                        .FirstOrDefault(),
                    cp.Conversation.Messages
                        .OrderByDescending(m => m.SentUtc)
                        .Select(m => (DateTime?)m.SentUtc)
                        .FirstOrDefault(),
                    cp.Conversation.Messages.Count(m =>
                        m.SenderUserId != userId
                        && (!cp.LastReadMessageUtc.HasValue || m.SentUtc > cp.LastReadMessageUtc.Value))))
                .ToListAsync();

            var summaries = new List<ConversationSummaryDto>(projections.Count);

            foreach (var projection in projections)
            {
                if (projection.ParticipantCount != 2 || string.IsNullOrWhiteSpace(projection.OtherUserId))
                {
                    _logger.LogWarning(
                        "Skipping malformed conversation {ConversationId} for user {UserId}. Participant count: {ParticipantCount}",
                        projection.ConversationId,
                        userId,
                        projection.ParticipantCount);
                    continue;
                }

                summaries.Add(new ConversationSummaryDto
                {
                    Id = projection.ConversationId,
                    OtherUser = new UserListItemDto
                    {
                        UserId = projection.OtherUserId,
                        UserName = projection.OtherUserName ?? string.Empty,
                        DisplayName = string.IsNullOrWhiteSpace(projection.OtherDisplayName)
                            ? (projection.OtherUserName ?? string.Empty)
                            : projection.OtherDisplayName,
                        AvatarUrl = projection.OtherAvatarUrl
                    },
                    LatestMessagePreview = string.IsNullOrWhiteSpace(projection.LatestMessageBody) ? null : TrimPreview(projection.LatestMessageBody),
                    LatestMessageSenderUserId = projection.LatestMessageSenderUserId,
                    LatestMessageSentUtc = projection.LatestMessageSentUtc,
                    UnreadCount = projection.UnreadCount
                });
            }

            return summaries
                .OrderByDescending(summary => summary.LatestMessageSentUtc ?? DateTime.MinValue)
                .ToList();
        }

        public async Task<ConversationSummaryDto> GetOrCreateDirectConversationAsync(string userId, string otherUserId)
        {
            if (string.Equals(userId, otherUserId, StringComparison.Ordinal))
            {
                throw new DomainValidationException("Invalid conversation request.", new Dictionary<string, string[]>
                {
                    ["userId"] = new[] { "You cannot start a conversation with yourself." }
                });
            }

            var otherUserExists = await _dbContext.Users.AnyAsync(u => u.Id == otherUserId);
            if (!otherUserExists)
            {
                throw new NotFoundException("User not found.");
            }

            var existingConversationId = await _dbContext.Conversations
                .Where(c => c.Participants.Count == 2
                    && c.Participants.Any(p => p.UserId == userId)
                    && c.Participants.Any(p => p.UserId == otherUserId))
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (existingConversationId != 0)
            {
                var existingMembership = await LoadMembershipAsync(userId, existingConversationId)
                    ?? throw new NotFoundException("Conversation not found.");
                return TryMapSummary(existingMembership, userId, out var existingSummary)
                    ? existingSummary
                    : throw new ConflictException("Conversation data is invalid.");
            }

            var utcNow = DateTime.UtcNow;
            var conversation = new Conversation
            {
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow,
                Participants = new List<ConversationParticipant>
                {
                    new() { UserId = userId, JoinedUtc = utcNow, LastReadMessageUtc = null },
                    new() { UserId = otherUserId, JoinedUtc = utcNow, LastReadMessageUtc = null }
                }
            };

            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync();

            var membership = await LoadMembershipAsync(userId, conversation.Id)
                ?? throw new NotFoundException("Conversation not found.");

            return TryMapSummary(membership, userId, out var createdSummary)
                ? createdSummary
                : throw new ConflictException("Conversation data is invalid.");
        }

        public async Task<PagedResult<DirectMessageDto>> GetMessagesAsync(string userId, int conversationId, int? beforeMessageId, int pageSize)
        {
            if (pageSize <= 0)
            {
                throw new DomainValidationException("Pagination parameters are invalid.", new Dictionary<string, string[]>
                {
                    ["pageSize"] = new[] { "Page size must be greater than 0." }
                });
            }

            await EnsureParticipantAsync(userId, conversationId);

            var query = _dbContext.DirectMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId);

            if (beforeMessageId.HasValue)
            {
                query = query.Where(m => m.Id < beforeMessageId.Value);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(m => m.Id)
                .Take(pageSize)
                .Select(m => new DirectMessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderUserId = m.SenderUserId,
                    SenderDisplayName = m.SenderUser.Profile != null && m.SenderUser.Profile.DisplayName != null
                        ? m.SenderUser.Profile.DisplayName
                        : (m.SenderUser.UserName ?? string.Empty),
                    SenderAvatarUrl = m.SenderUser.Profile != null ? m.SenderUser.Profile.AvatarUrl : m.SenderUser.AvatarUrl,
                    Body = m.Body,
                    SentUtc = m.SentUtc,
                    IsMine = m.SenderUserId == userId
                })
                .ToListAsync();

            items.Reverse();

            return new PagedResult<DirectMessageDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = 1,
                PageSize = pageSize
            };
        }

        public async Task<DirectMessageDto> SendMessageAsync(string userId, int conversationId, CreateDirectMessageDto dto)
        {
            var body = dto.Body?.Trim();
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new DomainValidationException("Message data is invalid.", new Dictionary<string, string[]>
                {
                    ["Body"] = new[] { "Message body is required." }
                });
            }

            if (body.Length > 2000)
            {
                throw new DomainValidationException("Message data is invalid.", new Dictionary<string, string[]>
                {
                    ["Body"] = new[] { "Message body must be 2000 characters or fewer." }
                });
            }

            var conversation = await _dbContext.Conversations
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .ThenInclude(u => u.Profile)
                .Include(c => c.Messages)
                .ThenInclude(m => m.SenderUser)
                .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(c => c.Id == conversationId)
                ?? throw new NotFoundException("Conversation not found.");

            var senderMembership = conversation.Participants.FirstOrDefault(p => p.UserId == userId)
                ?? throw new ForbiddenException("You do not have access to this conversation.");

            if (conversation.Participants.Count != 2)
            {
                throw new ConflictException("Only 1-to-1 conversations are supported.");
            }

            var utcNow = DateTime.UtcNow;
            var message = new DirectMessage
            {
                ConversationId = conversationId,
                SenderUserId = userId,
                Body = body,
                SentUtc = utcNow
            };

            conversation.Messages.Add(message);
            conversation.UpdatedUtc = utcNow;
            senderMembership.LastReadMessageUtc = utcNow;

            await _dbContext.SaveChangesAsync();

            var storedMessage = await _dbContext.DirectMessages
                .AsNoTracking()
                .Where(m => m.Id == message.Id)
                .Select(m => new DirectMessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderUserId = m.SenderUserId,
                    SenderDisplayName = m.SenderUser.Profile != null && m.SenderUser.Profile.DisplayName != null
                        ? m.SenderUser.Profile.DisplayName
                        : (m.SenderUser.UserName ?? string.Empty),
                    SenderAvatarUrl = m.SenderUser.Profile != null ? m.SenderUser.Profile.AvatarUrl : m.SenderUser.AvatarUrl,
                    Body = m.Body,
                    SentUtc = m.SentUtc,
                    IsMine = m.SenderUserId == userId
                })
                .FirstAsync();

            await _chatNotifier.NotifyMessageSentAsync(conversationId, storedMessage, conversation.Participants.Select(p => p.UserId));

            return storedMessage;
        }

        public async Task MarkReadAsync(string userId, int conversationId)
        {
            var participant = await _dbContext.ConversationParticipants
                .Include(cp => cp.Conversation)
                .ThenInclude(c => c.Participants)
                .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId)
                ?? throw new ForbiddenException("You do not have access to this conversation.");

            var lastMessageUtc = await _dbContext.DirectMessages
                .Where(m => m.ConversationId == conversationId)
                .Select(m => (DateTime?)m.SentUtc)
                .OrderByDescending(value => value)
                .FirstOrDefaultAsync();

            if (!lastMessageUtc.HasValue)
            {
                return;
            }

            participant.LastReadMessageUtc = lastMessageUtc.Value;
            await _dbContext.SaveChangesAsync();

            await _chatNotifier.NotifyConversationReadAsync(
                conversationId,
                userId,
                lastMessageUtc.Value,
                participant.Conversation.Participants.Select(p => p.UserId));
        }

        public Task<bool> IsParticipantAsync(string userId, int conversationId)
            => _dbContext.ConversationParticipants.AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

        private async Task EnsureParticipantAsync(string userId, int conversationId)
        {
            if (!await IsParticipantAsync(userId, conversationId))
            {
                throw new ForbiddenException("You do not have access to this conversation.");
            }
        }

        private async Task<ConversationParticipant?> LoadMembershipAsync(string userId, int conversationId)
        {
            return await _dbContext.ConversationParticipants
                .Where(cp => cp.UserId == userId && cp.ConversationId == conversationId)
                .Include(cp => cp.Conversation)
                .ThenInclude(c => c.Participants)
                .ThenInclude(p => p.User)
                .ThenInclude(u => u.Profile)
                .Include(cp => cp.Conversation)
                .ThenInclude(c => c.Messages)
                .ThenInclude(m => m.SenderUser)
                .ThenInclude(u => u.Profile)
                .AsSplitQuery()
                .FirstOrDefaultAsync();
        }

        private static bool TryMapSummary(ConversationParticipant membership, string currentUserId, out ConversationSummaryDto summary)
        {
            var otherParticipant = membership.Conversation.Participants.FirstOrDefault(p => p.UserId != currentUserId);
            if (otherParticipant?.User is null)
            {
                summary = default!;
                return false;
            }

            var latestMessage = membership.Conversation.Messages
                .OrderByDescending(m => m.SentUtc)
                .FirstOrDefault();

            var unreadCount = membership.Conversation.Messages.Count(m =>
                m.SenderUserId != currentUserId
                && (!membership.LastReadMessageUtc.HasValue || m.SentUtc > membership.LastReadMessageUtc.Value));

            summary = new ConversationSummaryDto
            {
                Id = membership.ConversationId,
                OtherUser = new UserListItemDto
                {
                    UserId = otherParticipant.UserId,
                    UserName = otherParticipant.User.UserName ?? string.Empty,
                    DisplayName = otherParticipant.User.Profile != null && otherParticipant.User.Profile.DisplayName != null
                        ? otherParticipant.User.Profile.DisplayName
                        : (otherParticipant.User.UserName ?? string.Empty),
                    AvatarUrl = otherParticipant.User.Profile != null
                        ? otherParticipant.User.Profile.AvatarUrl
                        : otherParticipant.User.AvatarUrl
                },
                LatestMessagePreview = string.IsNullOrWhiteSpace(latestMessage?.Body) ? null : TrimPreview(latestMessage.Body),
                LatestMessageSenderUserId = latestMessage?.SenderUserId,
                LatestMessageSentUtc = latestMessage?.SentUtc,
                UnreadCount = unreadCount
            };

            return true;
        }

        private static string TrimPreview(string body)
            => body.Length <= 120 ? body : body.Substring(0, 117) + "...";

        private sealed record ConversationSummaryProjection(
            int ConversationId,
            int ParticipantCount,
            string? OtherUserId,
            string? OtherUserName,
            string? OtherDisplayName,
            string? OtherAvatarUrl,
            string? LatestMessageBody,
            string? LatestMessageSenderUserId,
            DateTime? LatestMessageSentUtc,
            int UnreadCount);
    }
}
