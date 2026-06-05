using FluentAssertions;
using MiniPainterHub.WebApp.Shared.Viewer;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class ViewerPointerGestureStateTests
{
    [Fact]
    public void Complete_WhenLeftSwipeAndNotPannable_ReturnsNextImage()
    {
        var state = new ViewerPointerGestureState();
        state.Start(280d, 180d, "touch");

        var decision = state.Complete(180d, 186d, canPan: false, isPlacementMode: false);

        decision.Action.Should().Be(ViewerPointerGestureAction.NextImage);
    }

    [Fact]
    public void Complete_WhenShortTapInPlacementMode_ReturnsPlacementCoordinates()
    {
        var state = new ViewerPointerGestureState();
        state.Start(280d, 180d, "mouse");

        var decision = state.Complete(282d, 184d, canPan: false, isPlacementMode: true);

        decision.Action.Should().Be(ViewerPointerGestureAction.PlaceMark);
        decision.ClientX.Should().Be(282d);
        decision.ClientY.Should().Be(184d);
    }

    [Fact]
    public void Complete_WhenPannable_IgnoresSwipeNavigation()
    {
        var state = new ViewerPointerGestureState();
        state.Start(280d, 180d, "touch");

        var decision = state.Complete(180d, 186d, canPan: true, isPlacementMode: false);

        decision.Action.Should().Be(ViewerPointerGestureAction.None);
    }
}
