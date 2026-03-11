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

public class ConversationsControllerMalformedDataTests
{
    [Fact]
    public async Task GetConversations_WhenMalformedConversationExists_SkipsItInsteadOfReturningServerError()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await factory.SeedUserAsync("user-1", "user-1");
        await factory.SeedUserAsync("user-2", "user-2");
        await factory.SeedProfileAsync("user-2", "User Two", null);

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
                    new ConversationParticipant { UserId = "user-1", JoinedUtc = now },
                    new ConversationParticipant { UserId = "user-2", JoinedUtc = now }
                }
            });

            db.Conversations.Add(new Conversation
            {
                Id = 2,
                CreatedUtc = now,
                UpdatedUtc = now,
                Participants =
                {
                    new ConversationParticipant { UserId = "user-1", JoinedUtc = now }
                }
            });

            await db.SaveChangesAsync();
        });

        using var client = factory.CreateAuthenticatedClient("user-1", "user-1");

        var response = await client.GetAsync("/api/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await response.Content.ReadFromJsonAsync<ConversationSummaryDto[]>();
        summaries.Should().NotBeNull();
        summaries!.Should().HaveCount(1);
        summaries.Single().OtherUser.UserId.Should().Be("user-2");
    }
}
