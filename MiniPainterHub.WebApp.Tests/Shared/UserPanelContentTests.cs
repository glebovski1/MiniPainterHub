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
        this.AddSupportStub(new StubSupportTicketService
        {
            RefreshUnreadCountHandler = () => Task.FromResult(3)
        });
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
            cut.Markup.Should().Contain("Support");
            cut.Find("[data-testid='my-projects-nav-link']").TextContent.Should().Contain("My projects");
            cut.Find("[data-testid='new-project-quick-action']").TextContent.Should().Contain("New project");
            cut.Markup.Should().Contain(">1<");
            cut.Find("[data-testid='support-nav-link']").TextContent.Should().Contain("3");
            cut.Find("[data-testid='sign-in-methods-nav-link']").TextContent.Should().Contain("Sign-in methods");
            cut.Find(".dashboard-sidebar-content").Should().NotBeNull();
            cut.FindAll(".nav-pills").Should().BeEmpty();
        });
    }
}
