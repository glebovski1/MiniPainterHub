using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class ConversationsControllerTests
{
    [Fact]
    public async Task OpenDirectConversation_WhenCalledTwice_ReturnsSameConversation()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("caller-user", "caller");
        await factory.SeedUserAsync("other-user", "other");
        await factory.SeedProfileAsync("other-user", "Other Painter", "Bio");
        using var client = factory.CreateAuthenticatedClient("caller-user", "caller");

        var first = await client.PostAsJsonAsync("/api/conversations/direct/other-user", new { });
        var second = await client.PostAsJsonAsync("/api/conversations/direct/other-user", new { });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstBody = await first.Content.ReadFromJsonAsync<ConversationSummaryDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<ConversationSummaryDto>();
        firstBody!.Id.Should().Be(secondBody!.Id);
    }

    [Fact]
    public async Task GetMessages_WhenUserIsNotParticipant_ReturnsForbiddenProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedConversationAsync(factory, "user-1", "user-2");
        await factory.SeedUserAsync("outsider", "outsider");
        using var client = factory.CreateAuthenticatedClient("outsider", "outsider");

        var response = await client.GetAsync("/api/conversations/1/messages");

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Forbidden,
            "Forbidden",
            "You do not have access to this conversation.");
    }

    [Fact]
    public async Task SendMessage_WhenParticipant_SetsUnreadForOtherParticipant()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedConversationAsync(factory, "sender", "recipient");
        await factory.SeedProfileAsync("recipient", "Recipient", null);

        using var senderClient = factory.CreateAuthenticatedClient("sender", "sender");
        using var recipientClient = factory.CreateAuthenticatedClient("recipient", "recipient");

        var sendResponse = await senderClient.PostAsJsonAsync("/api/conversations/1/messages", new CreateDirectMessageDto
        {
            Body = "Hello there"
        });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summaries = await recipientClient.GetFromJsonAsync<ConversationSummaryDto[]>("/api/conversations") ?? Array.Empty<ConversationSummaryDto>();
        summaries.Single().UnreadCount.Should().Be(1);
        summaries.Single().LatestMessagePreview.Should().Be("Hello there");
    }

    [Fact]
    public async Task MarkRead_UpdatesOnlyCurrentParticipant()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedConversationAsync(factory, "user-1", "user-2");
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.DirectMessages.Add(new DirectMessage
            {
                ConversationId = 1,
                SenderUserId = "user-2",
                Body = "For user 1",
                SentUtc = DateTime.UtcNow.AddMinutes(-2)
            });
            db.DirectMessages.Add(new DirectMessage
            {
                ConversationId = 1,
                SenderUserId = "user-1",
                Body = "For user 2",
                SentUtc = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        });

        using var user1Client = factory.CreateAuthenticatedClient("user-1", "user-1");
        using var user2Client = factory.CreateAuthenticatedClient("user-2", "user-2");

        var readResponse = await user1Client.PostAsync("/api/conversations/1/read", null);

        readResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var user1Summaries = await user1Client.GetFromJsonAsync<ConversationSummaryDto[]>("/api/conversations");
        var user2Summaries = await user2Client.GetFromJsonAsync<ConversationSummaryDto[]>("/api/conversations");

        user1Summaries!.Single().UnreadCount.Should().Be(0);
        user2Summaries!.Single().UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task GetConversations_WhenMultipleMessagesExist_ReturnsOrderedSummariesWithTrimmedPreviewAndUnreadCounts()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("viewer", "viewer");
        await factory.SeedUserAsync("other-1", "other-1");
        await factory.SeedUserAsync("other-2", "other-2");
        await factory.SeedProfileAsync("other-1", "Other One", null);
        await factory.SeedProfileAsync("other-2", "Other Two", null);

        var latestBody = new string('x', 130);
        var now = DateTime.UtcNow;

        await factory.ExecuteDbContextAsync(async db =>
        {
            db.Conversations.Add(new Conversation
            {
                Id = 1,
                CreatedUtc = now.AddMinutes(-10),
                UpdatedUtc = now.AddMinutes(-1),
                Participants =
                {
                    new ConversationParticipant { UserId = "viewer", JoinedUtc = now.AddMinutes(-10), LastReadMessageUtc = now.AddMinutes(-4) },
                    new ConversationParticipant { UserId = "other-1", JoinedUtc = now.AddMinutes(-10) }
                },
                Messages =
                {
                    new DirectMessage { SenderUserId = "other-1", Body = "Older unread", SentUtc = now.AddMinutes(-5) },
                    new DirectMessage { SenderUserId = "other-1", Body = latestBody, SentUtc = now.AddMinutes(-1) }
                }
            });

            db.Conversations.Add(new Conversation
            {
                Id = 2,
                CreatedUtc = now.AddMinutes(-20),
                UpdatedUtc = now.AddMinutes(-2),
                Participants =
                {
                    new ConversationParticipant { UserId = "viewer", JoinedUtc = now.AddMinutes(-20), LastReadMessageUtc = now.AddMinutes(-1) },
                    new ConversationParticipant { UserId = "other-2", JoinedUtc = now.AddMinutes(-20) }
                },
                Messages =
                {
                    new DirectMessage { SenderUserId = "viewer", Body = "Mine earlier", SentUtc = now.AddMinutes(-2) }
                }
            });

            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("viewer", "viewer");

        var response = await client.GetAsync("/api/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await response.Content.ReadFromJsonAsync<ConversationSummaryDto[]>() ?? Array.Empty<ConversationSummaryDto>();
        summaries.Select(summary => summary.Id).Should().Equal(1, 2);

        summaries[0].OtherUser!.DisplayName.Should().Be("Other One");
        summaries[0].LatestMessagePreview.Should().Be(new string('x', 117) + "...");
        summaries[0].LatestMessageSenderUserId.Should().Be("other-1");
        summaries[0].UnreadCount.Should().Be(1);

        summaries[1].OtherUser!.DisplayName.Should().Be("Other Two");
        summaries[1].LatestMessagePreview.Should().Be("Mine earlier");
        summaries[1].LatestMessageSenderUserId.Should().Be("viewer");
        summaries[1].UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task SendMessage_WhenUserIsNotParticipant_ReturnsForbiddenProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedConversationAsync(factory, "user-1", "user-2");
        await factory.SeedUserAsync("outsider", "outsider");
        using var client = factory.CreateAuthenticatedClient("outsider", "outsider");

        var response = await client.PostAsJsonAsync("/api/conversations/1/messages", new CreateDirectMessageDto
        {
            Body = "Blocked"
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.Forbidden,
            "Forbidden",
            "You do not have access to this conversation.");
    }

    private static async Task SeedConversationAsync(IntegrationTestApplicationFactory factory, string user1Id, string user2Id)
    {
        await factory.SeedUserAsync(user1Id, user1Id);
        await factory.SeedUserAsync(user2Id, user2Id);
        await factory.ExecuteDbContextAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.Conversations.Add(new Conversation
            {
                Id = 1,
                CreatedUtc = now,
                UpdatedUtc = now,
                Participants =
                {
                    new ConversationParticipant { UserId = user1Id, JoinedUtc = now },
                    new ConversationParticipant { UserId = user2Id, JoinedUtc = now }
                }
            });
            await db.SaveChangesAsync();
        });
    }
}
