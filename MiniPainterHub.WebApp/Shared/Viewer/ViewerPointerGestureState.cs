namespace MiniPainterHub.WebApp.Shared.Viewer;

internal enum ViewerPointerGestureAction
{
    None,
    PlaceMark,
    PreviousImage,
    NextImage
}

internal readonly record struct ViewerPointerGestureDecision(
    ViewerPointerGestureAction Action,
    double ClientX,
    double ClientY);

internal sealed class ViewerPointerGestureState
{
    private const double SwipeThreshold = 60d;
    private const double DragThreshold = 6d;

    private PointerSnapshot? _pointer;

    public void Start(double clientX, double clientY, string? pointerType = null)
    {
        _pointer = new PointerSnapshot(
            clientX,
            clientY,
            clientX,
            clientY,
            pointerType,
            Moved: false);
    }

    public ViewerPointerGestureDecision Complete(double? clientX, double? clientY, bool canPan, bool isPlacementMode)
    {
        if (_pointer is null)
        {
            return new ViewerPointerGestureDecision(ViewerPointerGestureAction.None, 0d, 0d);
        }

        var pointer = _pointer;
        _pointer = null;

        var endX = clientX ?? pointer.LastX;
        var endY = clientY ?? pointer.LastY;
        var totalX = endX - pointer.StartX;
        var totalY = endY - pointer.StartY;
        var moved = pointer.Moved || Math.Abs(totalX) >= DragThreshold || Math.Abs(totalY) >= DragThreshold;

        if (!moved && isPlacementMode)
        {
            return new ViewerPointerGestureDecision(ViewerPointerGestureAction.PlaceMark, endX, endY);
        }

        if (!canPan
            && !isPlacementMode
            && Math.Abs(totalX) >= SwipeThreshold
            && Math.Abs(totalX) > Math.Abs(totalY))
        {
            return new ViewerPointerGestureDecision(
                totalX > 0 ? ViewerPointerGestureAction.PreviousImage : ViewerPointerGestureAction.NextImage,
                endX,
                endY);
        }

        return new ViewerPointerGestureDecision(ViewerPointerGestureAction.None, endX, endY);
    }

    public void Clear()
    {
        _pointer = null;
    }

    private sealed record PointerSnapshot(
        double StartX,
        double StartY,
        double LastX,
        double LastY,
        string? PointerType,
        bool Moved);
}
