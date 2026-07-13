using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public sealed class SupportTicketServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsTicketAndInitialMessageTogether()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        user.Profile = TestData.CreateProfile(user.Id, "Studio Painter");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);

        var result = await service.CreateAsync(user.Id, new CreateSupportTicketDto
        {
            Category = " bug ",
            Subject = "  Brush upload failed  ",
            Message = "  The upload stops at 50%.  "
        });

        result.Category.Should().Be(SupportTicketCategories.Bug);
        result.Subject.Should().Be("Brush upload failed");
        result.Status.Should().Be(SupportTicketStatuses.New);
        result.Messages.Should().ContainSingle(message =>
            message.Body == "The upload stops at 50%."
            && !message.IsStaffReply
            && message.IsMine);

        var stored = await context.SupportTickets.Include(ticket => ticket.Messages).SingleAsync();
        stored.Messages.Should().ContainSingle();
        stored.RequesterReadUtc.Should().NotBeNull();
        stored.ResolvedUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("Unknown", "Subject", "Message", "category")]
    [InlineData("Bug", " ", "Message", "Subject")]
    [InlineData("Bug", "Subject", " ", "Message")]
    public async Task CreateAsync_WhenInputIsInvalid_DoesNotPersistPartialTicket(
        string category,
        string subject,
        string message,
        string errorKey)
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);

        var action = () => service.CreateAsync(user.Id, new CreateSupportTicketDto
        {
            Category = category,
            Subject = subject,
            Message = message
        });

        var exception = await action.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey(errorKey);
        context.SupportTickets.Should().BeEmpty();
        context.SupportTicketMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForAdminAsync_FiltersAndOrdersQueueWithPagination()
    {
        await using var context = AppDbContextFactory.Create();
        var firstUser = TestData.CreateUser("user-1", "first");
        var secondUser = TestData.CreateUser("user-2", "second");
        context.Users.AddRange(firstUser, secondUser);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var first = await service.CreateAsync(firstUser.Id, CreateRequest("Bug one", SupportTicketCategories.Bug));
        var second = await service.CreateAsync(secondUser.Id, CreateRequest("Account two", SupportTicketCategories.Account));
        await service.ReplyAsUserAsync(firstUser.Id, first.Id, new CreateSupportTicketMessageDto { Body = "More detail" });

        var result = await service.GetForAdminAsync(new SupportTicketQueryDto
        {
            Status = " waitingforadmin ",
            Category = "BUG",
            Search = "Bug",
            PageNumber = 1,
            PageSize = 1
        });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.Id == first.Id);
        result.Items.Should().NotContain(item => item.Id == second.Id);
    }

    [Fact]
    public async Task GetForAdminAsync_SearchesMessageBody()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, CreateRequest());
        await service.ReplyAsUserAsync(user.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "The preview disappears after cropping."
        });

        var result = await service.GetForAdminAsync(new SupportTicketQueryDto
        {
            Search = "after cropping"
        });

        result.Items.Should().ContainSingle(item => item.Id == ticket.Id);
    }

    [Fact]
    public async Task UserOperations_WhenTicketBelongsToAnotherUser_ReturnNotFound()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = TestData.CreateUser("owner", "owner");
        var other = TestData.CreateUser("other", "other");
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(owner.Id, CreateRequest());

        Func<Task> getTicket = () => service.GetForUserAsync(other.Id, ticket.Id);
        Func<Task> reply = () => service.ReplyAsUserAsync(
            other.Id,
            ticket.Id,
            new CreateSupportTicketMessageDto { Body = "No access" });
        Func<Task> markRead = () => service.MarkReadAsync(other.Id, ticket.Id, new MarkSupportTicketReadDto());

        await getTicket.Should().ThrowAsync<NotFoundException>();
        await reply.Should().ThrowAsync<NotFoundException>();
        await markRead.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AdminReply_SetsWaitingForUserAndUnreadUntilRequesterMarksRead()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        var admin = TestData.CreateUser("admin-1", "admin");
        context.Users.AddRange(user, admin);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, CreateRequest());

        var replied = await service.ReplyAsAdminAsync(admin.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "We found the issue."
        });

        replied.Status.Should().Be(SupportTicketStatuses.WaitingForUser);
        replied.LastStaffReplyUtc.Should().NotBeNull();
        replied.HasUnreadStaffReply.Should().BeTrue();
        replied.Messages.Last().IsStaffReply.Should().BeTrue();
        (await service.GetUnreadCountAsync(user.Id)).Count.Should().Be(1);

        await service.MarkReadAsync(user.Id, ticket.Id, new MarkSupportTicketReadDto
        {
            LastStaffReplyUtc = replied.LastStaffReplyUtc
        });

        (await service.GetUnreadCountAsync(user.Id)).Count.Should().Be(0);
        (await service.GetForUserAsync(user.Id, ticket.Id)).HasUnreadStaffReply.Should().BeFalse();
    }

    [Fact]
    public async Task MarkReadAsync_WhenNewStaffReplyArrivesAfterObservedCursor_LeavesNewReplyUnread()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        var admin = TestData.CreateUser("admin-1", "admin");
        context.Users.AddRange(user, admin);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, CreateRequest());
        var firstReply = await service.ReplyAsAdminAsync(admin.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "First reply"
        });
        var observedCursor = firstReply.LastStaffReplyUtc!.Value;
        var laterReplyUtc = observedCursor.AddMinutes(1);
        var storedTicket = await context.SupportTickets.SingleAsync(item => item.Id == ticket.Id);
        storedTicket.LastStaffReplyUtc = laterReplyUtc;
        storedTicket.UpdatedUtc = laterReplyUtc;
        storedTicket.Messages.Add(new SupportTicketMessage
        {
            AuthorUserId = admin.Id,
            Body = "Reply that arrived after the page loaded",
            SentUtc = laterReplyUtc,
            IsStaffReply = true
        });
        await context.SaveChangesAsync();

        await service.MarkReadAsync(user.Id, ticket.Id, new MarkSupportTicketReadDto
        {
            LastStaffReplyUtc = observedCursor
        });

        var afterOldAcknowledgement = await service.GetForUserAsync(user.Id, ticket.Id);
        afterOldAcknowledgement.RequesterReadUtc.Should().Be(observedCursor);
        afterOldAcknowledgement.HasUnreadStaffReply.Should().BeTrue();
        (await service.GetUnreadCountAsync(user.Id)).Count.Should().Be(1);

        await service.ReplyAsUserAsync(user.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "My reply must not acknowledge a staff message I did not observe."
        });
        (await service.GetUnreadCountAsync(user.Id)).Count.Should().Be(1);

        await service.MarkReadAsync(user.Id, ticket.Id, new MarkSupportTicketReadDto
        {
            LastStaffReplyUtc = laterReplyUtc.AddHours(1)
        });

        var afterClampedAcknowledgement = await service.GetForUserAsync(user.Id, ticket.Id);
        afterClampedAcknowledgement.RequesterReadUtc.Should().Be(laterReplyUtc);
        afterClampedAcknowledgement.HasUnreadStaffReply.Should().BeFalse();
    }

    [Fact]
    public async Task GetForAdminAsync_SearchesMessageHistoryButReturnsOnlyLatestPreview()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, new CreateSupportTicketDto
        {
            Category = SupportTicketCategories.Bug,
            Subject = "Upload issue",
            Message = "The historical needle phrase is in this first message."
        });
        await service.ReplyAsUserAsync(user.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "This is the newest update."
        });

        var result = await service.GetForAdminAsync(new SupportTicketQueryDto
        {
            Search = "historical needle",
            PageNumber = 1,
            PageSize = 20
        });

        result.Items.Should().ContainSingle();
        result.Items.Single().LatestMessagePreview.Should().Be("This is the newest update.");
    }

    [Fact]
    public async Task ResolveThenUserReply_ReopensAndClearsResolvedTimestamp()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        var admin = TestData.CreateUser("admin-1", "admin");
        context.Users.AddRange(user, admin);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, CreateRequest());

        var resolved = await service.UpdateStatusAsAdminAsync(admin.Id, ticket.Id, new UpdateSupportTicketStatusDto
        {
            Status = " resolved "
        });
        resolved.Status.Should().Be(SupportTicketStatuses.Resolved);
        resolved.ResolvedUtc.Should().NotBeNull();

        var reopened = await service.ReplyAsUserAsync(user.Id, ticket.Id, new CreateSupportTicketMessageDto
        {
            Body = "I still need help."
        });

        reopened.Status.Should().Be(SupportTicketStatuses.WaitingForAdmin);
        reopened.ResolvedUtc.Should().BeNull();
    }

    [Fact]
    public async Task AdminCannotReplyToResolvedTicketOrApplyInvalidStatusTransition()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "painter");
        var admin = TestData.CreateUser("admin-1", "admin");
        context.Users.AddRange(user, admin);
        await context.SaveChangesAsync();
        var service = new SupportTicketService(context);
        var ticket = await service.CreateAsync(user.Id, CreateRequest());
        await service.UpdateStatusAsAdminAsync(admin.Id, ticket.Id, new UpdateSupportTicketStatusDto
        {
            Status = SupportTicketStatuses.Resolved
        });

        Func<Task> reply = () => service.ReplyAsAdminAsync(
            admin.Id,
            ticket.Id,
            new CreateSupportTicketMessageDto { Body = "Late reply" });
        Func<Task> transition = () => service.UpdateStatusAsAdminAsync(admin.Id, ticket.Id, new UpdateSupportTicketStatusDto
        {
            Status = SupportTicketStatuses.WaitingForUser
        });

        await reply.Should().ThrowAsync<DomainValidationException>();
        await transition.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task GetForAdminAsync_WhenQueryIsInvalid_ReturnsAllValidationErrors()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new SupportTicketService(context);

        var action = () => service.GetForAdminAsync(new SupportTicketQueryDto
        {
            Status = "invalid",
            Category = "invalid",
            PageNumber = 0,
            PageSize = 101,
            Search = new string('x', 201)
        });

        var exception = await action.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Keys.Should().Contain(new[] { "status", "category", "page", "pageSize", "search" });
    }

    private static CreateSupportTicketDto CreateRequest(
        string subject = "Need help",
        string category = SupportTicketCategories.Other) =>
        new()
        {
            Category = category,
            Subject = subject,
            Message = "Please help with this issue."
        };
}
