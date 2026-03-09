using Microsoft.AspNetCore.SignalR;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Realtime
{
    public class SignalRChatNotifier : IChatNotifier
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public SignalRChatNotifier(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyMessageSentAsync(int conversationId, DirectMessageDto message, IEnumerable<string> participantUserIds)
        {
            var recipients = participantUserIds.Distinct().ToArray();
            await _hubContext.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("MessageReceived", message);

            foreach (var userId in recipients)
            {
                await _hubContext.Clients.User(userId).SendAsync("ConversationChanged", new ConversationChangedDto
                {
                    ConversationId = conversationId
                });
            }
        }

        public async Task NotifyConversationReadAsync(int conversationId, string readerUserId, System.DateTime lastReadUtc, IEnumerable<string> participantUserIds)
        {
            var recipients = participantUserIds.Distinct().ToArray();
            var payload = new ConversationReadDto
            {
                ConversationId = conversationId,
                ReaderUserId = readerUserId,
                LastReadUtc = lastReadUtc
            };

            await _hubContext.Clients.Group(ChatHub.GroupName(conversationId)).SendAsync("ConversationRead", payload);

            foreach (var userId in recipients)
            {
                await _hubContext.Clients.User(userId).SendAsync("ConversationChanged", new ConversationChangedDto
                {
                    ConversationId = conversationId
                });
            }
        }
    }
}
