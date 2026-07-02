using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Guides;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Guides;

public class CreateGuideTests : TestContext
{
    [Fact]
    public void RendersGuideFormWithStepFields()
    {
        this.AddGuideStub();

        var cut = RenderComponent<CreateGuide>();

        cut.Find("[data-testid='guide-create-form']").Should().NotBeNull();
        cut.Find("[data-testid='guide-title']").Should().NotBeNull();
        cut.Find("[data-testid='guide-summary']").Should().NotBeNull();
        cut.Find("[data-testid='guide-step-title']").Should().NotBeNull();
        cut.Find("[data-testid='guide-step-photo']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WithoutPhotos_CreatesGuideAndNavigatesToDetails()
    {
        CreatePaintingGuideDto? captured = null;
        this.AddGuideStub(new StubPaintingGuideService
        {
            CreateHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new PaintingGuideDto
                {
                    Id = 77,
                    Title = dto.Title,
                    Summary = dto.Summary,
                    CreatedById = "user-1",
                    Steps = new List<PaintingGuideStepDto>()
                });
            }
        });

        var cut = RenderComponent<CreateGuide>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='guide-title']").Change("Red cloak guide");
        cut.Find("[data-testid='guide-summary']").Change("Paint a red cloak from basecoat to highlight.");
        cut.Find("[data-testid='guide-materials']").Change("Khorne Red, Evil Sunz Scarlet");
        cut.Find("[data-testid='guide-step-title']").Change("Basecoat");
        cut.Find("[data-testid='guide-step-description']").Change("Apply two thin coats.");
        cut.Find("[data-testid='guide-step-paints']").Change("Khorne Red");
        cut.Find("[data-testid='guide-step-techniques']").Change("Thin layers");

        await cut.Find("[data-testid='guide-create-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/guides/77");
            captured.Should().NotBeNull();
            captured!.Title.Should().Be("Red cloak guide");
            captured.Materials.Should().Be("Khorne Red, Evil Sunz Scarlet");
            captured.Steps.Should().ContainSingle();
            captured.Steps[0].PaintsUsed.Should().Be("Khorne Red");
        });
    }
}
