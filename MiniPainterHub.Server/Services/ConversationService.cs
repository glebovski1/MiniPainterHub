using Microsoft.EntityFrameworkCore;
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

        public ConversationService(AppDbContext dbContext, IChatNotifier chatNotifier)
        {
            _dbContext = dbContext;
            _chatNotifier = chatNotifier;
        }

        public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(string userId)
        {
            var memberships = await LoadConversationMembershipsAsync(userId);

            return memberships
                .Select(cp => MapSummary(cp, userId))
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
                return MapSummary(existingMembership, userId);
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

            return MapSummary(membership, userId);
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

        private async Task<List<ConversationParticipant>> LoadConversationMembershipsAsync(string userId)
        {
            return await _dbContext.ConversationParticipants
                .Where(cp => cp.UserId == userId)
                .Include(cp => cp.Conversation)
                .ThenInclude(c => c.Participants)
                .ThenInclude(p => p.User)
                .ThenInclude(u => u.Profile)
                .Include(cp => cp.Conversation)
                .ThenInclude(c => c.Messages)
                .ThenInclude(m => m.SenderUser)
                .ThenInclude(u => u.Profile)
                .AsSplitQuery()
                .ToListAsync();
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

        private static ConversationSummaryDto MapSummary(ConversationParticipant membership, string currentUserId)
        {
            var otherParticipant = membership.Conversation.Participants.First(p => p.UserId != currentUserId);
            var latestMessage = membership.Conversation.Messages
                .OrderByDescending(m => m.SentUtc)
                .FirstOrDefault();

            var unreadCount = membership.Conversation.Messages.Count(m =>
                m.SenderUserId != currentUserId
                && (!membership.LastReadMessageUtc.HasValue || m.SentUtc > membership.LastReadMessageUtc.Value));

            return new ConversationSummaryDto
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
                LatestMessagePreview = latestMessage == null ? null : TrimPreview(latestMessage.Body),
                LatestMessageSenderUserId = latestMessage?.SenderUserId,
                LatestMessageSentUtc = latestMessage?.SentUtc,
                UnreadCount = unreadCount
            };
        }

        private static string TrimPreview(string body)
            => body.Length <= 120 ? body : body.Substring(0, 117) + "...";
    }
}
