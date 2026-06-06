using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Realtime
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IConversationService _conversationService;
        private readonly ISiteActivityTracker _activityTracker;

        public ChatHub(IConversationService conversationService, ISiteActivityTracker activityTracker)
        {
            _conversationService = conversationService;
            _activityTracker = activityTracker;
        }

        public override Task OnConnectedAsync()
        {
            _activityTracker.RecordSignalRConnected();
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
            _activityTracker.RecordSignalRDisconnected();
            return base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversation(int conversationId)
        {
            var userId = Context.User?.GetUserIdOrThrow();
            if (userId == null || !await _conversationService.IsParticipantAsync(userId, conversationId))
            {
                throw new HubException("Forbidden");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
        }

        public Task LeaveConversation(int conversationId)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));

        public static string GroupName(int conversationId) => $"conversation-{conversationId}";
    }
}
