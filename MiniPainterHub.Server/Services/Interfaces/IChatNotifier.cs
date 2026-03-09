using MiniPainterHub.Common.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface IChatNotifier
    {
        Task NotifyMessageSentAsync(int conversationId, DirectMessageDto message, IEnumerable<string> participantUserIds);
        Task NotifyConversationReadAsync(int conversationId, string readerUserId, System.DateTime lastReadUtc, IEnumerable<string> participantUserIds);
    }
}
