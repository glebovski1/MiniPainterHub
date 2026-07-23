using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class CreateNewsAnnouncementTests : BunitContext
{
    [Fact]
    public async Task Submit_CreatesAnnouncementAndNavigatesToDetails()
    {
        CreateNewsAnnouncementDto? captured = null;
        this.AddNewsStub(new StubNewsAnnouncementService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new NewsAnnouncementDto
                {
                    Id = 88,
                    Title = dto.Title,
                    Summary = dto.Summary,
                    Body = dto.Body,
                    CreatedById = "admin"
                });
            }
        });

        var cut = Render<CreateNewsAnnouncement>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='news-title']").Change("Challenge week");
        cut.Find("[data-testid='news-summary']").Change("A new painting prompt is live.");
        cut.Find("[data-testid='news-body']").Change("Paint something red and share the recipe.");

        await cut.Find("[data-testid='news-create-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/news/88");
            captured.Should().NotBeNull();
            captured!.Title.Should().Be("Challenge week");
            captured.Body.Should().Contain("Paint something red");
        });
    }
}
