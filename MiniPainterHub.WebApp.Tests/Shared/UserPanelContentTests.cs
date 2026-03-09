using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class UserPanelContentTests : TestContext
{
    [Fact]
    public void RendersSocialLinksAndUnreadBadge()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddConversationStub(new StubConversationService
        {
            GetConversationsHandler = _ => Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(new[]
            {
                new ConversationSummaryDto
                {
                    Id = 1,
                    OtherUser = new UserListItemDto { UserId = "other-user", UserName = "other", DisplayName = "Other Painter" },
                    UnreadCount = 2
                }
            })
        });

        var cut = RenderComponent<UserPanelContent>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Following feed");
            cut.Markup.Should().Contain("Connections");
            cut.Markup.Should().Contain("Messages");
            cut.Markup.Should().Contain(">1<");
        });
    }
}
