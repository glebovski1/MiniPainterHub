using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class MessagesTests : TestContext
{
    [Fact]
    public void RendersConversationAndSendsMessage()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddConversationStub(new StubConversationService
        {
            GetConversationsHandler = _ => Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(new[]
            {
                new ConversationSummaryDto
                {
                    Id = 5,
                    OtherUser = new UserListItemDto
                    {
                        UserId = "other-user",
                        UserName = "other",
                        DisplayName = "Other Painter"
                    },
                    LatestMessagePreview = "Earlier message",
                    UnreadCount = 1
                }
            }),
            GetMessagesHandler = (_, _, _) => Task.FromResult(new PagedResult<DirectMessageDto>
            {
                Items = new[]
                {
                    new DirectMessageDto
                    {
                        Id = 1,
                        ConversationId = 5,
                        SenderUserId = "other-user",
                        SenderDisplayName = "Other Painter",
                        Body = "Earlier message",
                        SentUtc = System.DateTime.UtcNow.AddMinutes(-5),
                        IsMine = false
                    }
                },
                PageNumber = 1,
                PageSize = 50,
                TotalCount = 1
            }),
            SendMessageHandler = (conversationId, dto) => Task.FromResult(new DirectMessageDto
            {
                Id = 2,
                ConversationId = conversationId,
                SenderUserId = "viewer-user",
                SenderDisplayName = "viewer",
                Body = dto.Body,
                SentUtc = System.DateTime.UtcNow,
                IsMine = true
            })
        });

        var cut = RenderComponent<Messages>(parameters => parameters.Add(p => p.ConversationId, 5));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Other Painter"));

        cut.Find("input.form-control").Change("New message");
        cut.Find("button[type='submit']").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("New message"));
    }

    [Fact]
    public void OpeningConversation_JoinsThreadAndMarksItRead()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        var joinedConversationIds = new List<int>();
        var markedReadConversationIds = new List<int>();

        this.AddConversationStub(new StubConversationService
        {
            GetConversationsHandler = _ => Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(new[]
            {
                new ConversationSummaryDto
                {
                    Id = 5,
                    OtherUser = new UserListItemDto
                    {
                        UserId = "other-user",
                        UserName = "other",
                        DisplayName = "Other Painter"
                    }
                }
            }),
            JoinConversationHandler = conversationId =>
            {
                joinedConversationIds.Add(conversationId);
                return Task.CompletedTask;
            },
            MarkReadHandler = conversationId =>
            {
                markedReadConversationIds.Add(conversationId);
                return Task.CompletedTask;
            }
        });

        var cut = RenderComponent<Messages>(parameters => parameters.Add(p => p.ConversationId, 5));

        cut.WaitForAssertion(() =>
        {
            joinedConversationIds.Should().Contain(5);
            markedReadConversationIds.Should().Contain(5);
            cut.Markup.Should().Contain("Other Painter");
        });
    }

    [Fact]
    public void IncomingRealtimeMessage_AppendsToActiveThread()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        var markedReadConversationIds = new List<int>();
        var stub = this.AddConversationStub(new StubConversationService
        {
            GetConversationsHandler = _ => Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(new[]
            {
                new ConversationSummaryDto
                {
                    Id = 5,
                    OtherUser = new UserListItemDto
                    {
                        UserId = "other-user",
                        UserName = "other",
                        DisplayName = "Other Painter"
                    }
                }
            }),
            GetMessagesHandler = (_, _, _) => Task.FromResult(new PagedResult<DirectMessageDto>
            {
                Items = new[]
                {
                    new DirectMessageDto
                    {
                        Id = 1,
                        ConversationId = 5,
                        SenderUserId = "other-user",
                        SenderDisplayName = "Other Painter",
                        Body = "Existing message",
                        SentUtc = System.DateTime.UtcNow.AddMinutes(-2),
                        IsMine = false
                    }
                },
                PageNumber = 1,
                PageSize = 50,
                TotalCount = 1
            }),
            MarkReadHandler = conversationId =>
            {
                markedReadConversationIds.Add(conversationId);
                return Task.CompletedTask;
            }
        });

        var cut = RenderComponent<Messages>(parameters => parameters.Add(p => p.ConversationId, 5));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Existing message"));

        stub.RaiseMessageReceived(new DirectMessageDto
        {
            Id = 2,
            ConversationId = 5,
            SenderUserId = "other-user",
            SenderDisplayName = "Other Painter",
            Body = "Live message",
            SentUtc = System.DateTime.UtcNow,
            IsMine = false
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Live message");
            markedReadConversationIds.Should().Contain(5);
        });
    }
}
