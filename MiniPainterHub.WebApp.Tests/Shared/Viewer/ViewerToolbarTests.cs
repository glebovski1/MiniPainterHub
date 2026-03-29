using Bunit;
using FluentAssertions;
using MiniPainterHub.WebApp.Shared.Viewer;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class ViewerToolbarTests : TestContext
{
    [Fact]
    public void RendersGroupedNavigationZoomAndUtilityControls()
    {
        var cut = RenderComponent<ViewerToolbar>(parameters => parameters
            .Add(component => component.Eyebrow, "Rich viewer")
            .Add(component => component.Title, "Moonlit skin experiment")
            .Add(component => component.TitleId, "viewer-title")
            .Add(component => component.CurrentIndex, 2)
            .Add(component => component.TotalCount, 7)
            .Add(component => component.ZoomPercent, 125)
            .Add(component => component.ScaleMode, ViewerScaleMode.Fill)
            .Add(component => component.CanGoPrevious, true)
            .Add(component => component.CanGoNext, true)
            .Add(component => component.CanToggleFullscreen, true)
            .Add(component => component.CanAddAuthorMark, true));

        cut.Find("[data-testid='viewer-rail-header']").TextContent.Should().Contain("Moonlit skin experiment");
        cut.Find("[data-testid='viewer-rail-navigation']").TextContent.Should().Contain("2 of 7");
        cut.Find("[data-testid='viewer-prev']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-next']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("125%");
        cut.Find("[data-testid='viewer-zoom-out']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-zoom-in']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-view-fill']").ClassList.Should().Contain("is-active");
        cut.Find("[data-testid='viewer-reset']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-view-actual']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-rail-utility']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-fullscreen']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-add-note']").Should().NotBeNull();
        cut.Find("[data-testid='viewer-close']").Should().NotBeNull();
    }

    [Fact]
    public void SingleFrameToolbarDropsBrowseSectionAndShowsFooterHints()
    {
        var cut = RenderComponent<ViewerToolbar>(parameters => parameters
            .Add(component => component.Eyebrow, "Rich viewer")
            .Add(component => component.Title, "Moonlit skin experiment")
            .Add(component => component.TitleId, "viewer-title")
            .Add(component => component.CurrentIndex, 1)
            .Add(component => component.TotalCount, 1)
            .Add(component => component.ZoomPercent, 110)
            .Add(component => component.ScaleMode, ViewerScaleMode.Fit)
            .Add(component => component.CanGoPrevious, false)
            .Add(component => component.CanGoNext, false)
            .Add(component => component.CanToggleFullscreen, true));

        cut.FindAll("[data-testid='viewer-rail-navigation']").Should().BeEmpty();
        cut.Find("[data-testid='viewer-rail-hints']").TextContent.Should().Contain("zoom");
        cut.Find("[data-testid='viewer-fullscreen']").Should().NotBeNull();
    }
}
