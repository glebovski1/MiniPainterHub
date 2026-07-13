using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public sealed class SupportTicketsControllerTests
{
    [Fact]
    public async Task SupportEndpoints_WhenAnonymous_ReturnUnauthorized()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/support/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/admin/support/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoints_WhenModerator_ReturnForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("moderator", "moderator", "Moderator");

        var response = await client.GetAsync("/api/admin/support/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreatedTicketAndPersistsInitialMessage()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "user-1", "painter");
        using var client = factory.CreateAuthenticatedClient("user-1", "painter");

        var response = await client.PostAsJsonAsync("/api/support/tickets", CreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await response.Content.ReadFromJsonAsync<SupportTicketDto>();
        ticket.Should().NotBeNull();
        ticket!.Messages.Should().ContainSingle();
        response.Headers.Location.Should().NotBeNull();

        await factory.ExecuteDbContextAsync(db =>
        {
            db.SupportTickets.Should().ContainSingle();
            db.SupportTicketMessages.Should().ContainSingle();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task UserEndpoints_WhenTicketBelongsToAnotherUser_ReturnNotFound()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "owner", "owner");
        await SeedUserAsync(factory, "other", "other");
        await SeedTicketAsync(factory, "owner");
        using var client = factory.CreateAuthenticatedClient("other", "other");

        (await client.GetAsync("/api/support/tickets/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/support/tickets/1/messages", new CreateSupportTicketMessageDto { Body = "No access" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/support/tickets/1/read", new MarkSupportTicketReadDto
        {
            LastStaffReplyUtc = DateTime.UtcNow
        }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_WhenObservedCursorIsMissing_ReturnsBadRequest()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "owner", "owner");
        await SeedTicketAsync(factory, "owner");
        using var client = factory.CreateAuthenticatedClient("owner", "owner");

        var response = await client.PostAsJsonAsync("/api/support/tickets/1/read", new MarkSupportTicketReadDto());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TicketFlow_UserCreatesAdminRepliesAndResolvesThenUserReopens()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "user-1", "painter");
        await SeedUserAsync(factory, "admin-1", "admin");
        using var userClient = factory.CreateAuthenticatedClient("user-1", "painter");
        using var adminClient = factory.CreateAuthenticatedClient("admin-1", "admin", "Admin");

        var createResponse = await userClient.PostAsJsonAsync("/api/support/tickets", CreateRequest());
        var created = await createResponse.Content.ReadFromJsonAsync<SupportTicketDto>();

        var adminReplyResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/support/tickets/{created!.Id}/messages",
            new CreateSupportTicketMessageDto { Body = "We are investigating." });
        adminReplyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminReply = await adminReplyResponse.Content.ReadFromJsonAsync<SupportTicketDto>();

        var unread = await userClient.GetFromJsonAsync<SupportUnreadCountDto>("/api/support/tickets/unread-count");
        unread!.Count.Should().Be(1);

        var readResponse = await userClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.Id}/read",
            new MarkSupportTicketReadDto { LastStaffReplyUtc = adminReply!.LastStaffReplyUtc });
        readResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resolveResponse = await adminClient.PutAsJsonAsync(
            $"/api/admin/support/tickets/{created.Id}/status",
            new UpdateSupportTicketStatusDto { Status = "RESOLVED" });
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<SupportTicketDto>();
        resolved!.ResolvedUtc.Should().NotBeNull();

        var reopenResponse = await userClient.PostAsJsonAsync(
            $"/api/support/tickets/{created.Id}/messages",
            new CreateSupportTicketMessageDto { Body = "The issue continues." });
        var reopened = await reopenResponse.Content.ReadFromJsonAsync<SupportTicketDto>();
        reopened!.Status.Should().Be(SupportTicketStatuses.WaitingForAdmin);
        reopened.ResolvedUtc.Should().BeNull();
        reopened.Messages.Should().HaveCount(3);
    }

    [Fact]
    public async Task AdminStatusUpdate_WhenTransitionIsInvalid_ReturnsValidationProblemDetails()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "owner", "owner");
        await SeedUserAsync(factory, "admin-1", "admin");
        await SeedTicketAsync(factory, "owner");
        using var client = factory.CreateAuthenticatedClient("admin-1", "admin", "Admin");

        var response = await client.PutAsJsonAsync("/api/admin/support/tickets/1/status", new UpdateSupportTicketStatusDto
        {
            Status = SupportTicketStatuses.WaitingForUser
        });

        await ProblemDetailsAssertions.AssertAsync(
            response,
            HttpStatusCode.BadRequest,
            "Validation error",
            expectedErrorKeys: new[] { "status" });
    }

    private static Task SeedUserAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        string userName)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test",
                Profile = new Profile
                {
                    UserId = userId,
                    DisplayName = userName
                }
            });
            await db.SaveChangesAsync();
        });
    }

    private static Task SeedTicketAsync(IntegrationTestApplicationFactory factory, string userId)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var utcNow = DateTime.UtcNow;
            db.SupportTickets.Add(new SupportTicket
            {
                Id = 1,
                RequesterUserId = userId,
                Category = SupportTicketCategories.Other,
                Subject = "Existing ticket",
                Status = SupportTicketStatuses.New,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow,
                RequesterReadUtc = utcNow,
                Messages =
                {
                    new SupportTicketMessage
                    {
                        AuthorUserId = userId,
                        Body = "Initial message",
                        SentUtc = utcNow,
                        IsStaffReply = false
                    }
                }
            });
            await db.SaveChangesAsync();
        });
    }

    private static CreateSupportTicketDto CreateRequest() =>
        new()
        {
            Category = SupportTicketCategories.Bug,
            Subject = "Upload problem",
            Message = "My image upload is failing."
        };
}
