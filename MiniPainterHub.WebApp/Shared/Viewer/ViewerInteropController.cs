using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MiniPainterHub.WebApp.Shared.Viewer;

internal sealed class ViewerInteropController
{
    private readonly IJSRuntime _jsRuntime;

    private IJSObjectReference? _module;
    private Task<IJSObjectReference?>? _moduleImportTask;
    private bool _moduleLoadFailed;

    public ViewerInteropController(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsModalActive { get; private set; }
    public bool IsStageObserved { get; private set; }
    public bool IsGestureInteropActive { get; private set; }
    public bool IsStageControlsActive { get; private set; }
    public bool IsFullscreenSupported { get; private set; }
    public bool IsFullscreen { get; private set; }
    public bool HasActiveLifecycle => IsModalActive || IsStageObserved || IsGestureInteropActive || IsStageControlsActive;

    public async Task WarmModuleAsync()
    {
        try
        {
            await Task.Delay(100);
            await EnsureModuleAsync();
        }
        catch
        {
            // Module warmup is opportunistic; viewer open still handles fallback.
        }
    }

    public async Task EnsureViewerAsync(
        ElementReference rootElement,
        ElementReference stageElement,
        ElementReference stageSurfaceElement,
        ElementReference transformElement,
        DotNetObjectReference<RichImageViewer> selfReference,
        ViewerTransformInteropSnapshot transform)
    {
        var module = await EnsureModuleAsync();
        if (module is null)
        {
            return;
        }

        try
        {
            if (!IsModalActive)
            {
                await module.InvokeVoidAsync("activateModal", rootElement);
                await module.InvokeVoidAsync("registerFullscreenChange", rootElement, selfReference);
                IsFullscreenSupported = await module.InvokeAsync<bool>("isFullscreenSupported");
                IsModalActive = true;
            }

            if (!IsStageObserved)
            {
                await module.InvokeVoidAsync("observeStageSize", stageElement, selfReference);
                IsStageObserved = true;
            }

            if (!IsGestureInteropActive)
            {
                await module.InvokeVoidAsync(
                    "activateViewerGestures",
                    stageElement,
                    transformElement,
                    selfReference,
                    transform);
                IsGestureInteropActive = true;
            }

            if (!IsStageControlsActive)
            {
                await module.InvokeVoidAsync("activateStageControls", stageSurfaceElement);
                IsStageControlsActive = true;
            }
        }
        catch
        {
            IsFullscreenSupported = false;
        }
    }

    public async Task SyncTransformAsync(
        ElementReference stageElement,
        ElementReference transformElement,
        ViewerTransformInteropSnapshot transform)
    {
        var module = await EnsureModuleAsync();
        if (module is null || !IsGestureInteropActive)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync(
                "syncViewerTransform",
                stageElement,
                transformElement,
                transform);
        }
        catch
        {
            // Gesture interop is an enhancement. Blazor-rendered controls remain usable.
        }
    }

    public async Task<bool> RequestFullscreenAsync(ElementReference rootElement)
    {
        var module = await EnsureModuleAsync();
        if (module is null || !IsFullscreenSupported)
        {
            return false;
        }

        try
        {
            IsFullscreen = await module.InvokeAsync<bool>("requestFullscreen", rootElement);
            return IsFullscreen;
        }
        catch
        {
            IsFullscreen = false;
            return false;
        }
    }

    public void SetFullscreen(bool isFullscreen)
    {
        IsFullscreen = isFullscreen;
    }

    public async Task<ViewerRelativePoint?> GetRelativePointAsync(
        ElementReference stageElement,
        bool hasStage,
        double clientX,
        double clientY)
    {
        var module = await EnsureModuleAsync();
        if (module is null || !hasStage)
        {
            return new ViewerRelativePoint { X = clientX, Y = clientY };
        }

        try
        {
            return await module.InvokeAsync<ViewerRelativePoint>("getRelativePoint", stageElement, clientX, clientY);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ViewerRelativeRect?> GetRelativeRectAsync(
        ElementReference stageElement,
        ElementReference imageElement,
        bool hasStage,
        ViewerRelativeRect? fallback)
    {
        var module = await EnsureModuleAsync();
        if (module is null || !hasStage)
        {
            return fallback;
        }

        try
        {
            return await module.InvokeAsync<ViewerRelativeRect>("getRelativeRect", stageElement, imageElement);
        }
        catch
        {
            return fallback;
        }
    }

    public async Task PreloadImagesAsync(IReadOnlyList<string> urls)
    {
        var module = await EnsureModuleAsync();
        if (module is null || urls.Count == 0)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("preloadImages", urls);
        }
        catch
        {
            // Ignore preload failures and preserve the primary viewer interaction.
        }
    }

    public async Task ScrollIntoViewIfNeededAsync(string selector)
    {
        var module = await EnsureModuleAsync();
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("scrollIntoViewIfNeeded", selector);
        }
        catch
        {
            // Ignore scrolling failures while the viewer is updating.
        }
    }

    public async Task ScrollElementToTopAsync(ElementReference element)
    {
        var module = await EnsureModuleAsync();
        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("scrollElementToTop", element);
        }
        catch
        {
            // Ignore panel scroll reset failures during viewer open.
        }
    }

    public async Task DeactivateAsync(
        ElementReference rootElement,
        ElementReference stageElement,
        ElementReference stageSurfaceElement,
        bool hasStage)
    {
        await TeardownAsync(rootElement, stageElement, stageSurfaceElement, hasStage, disposeModule: false);
    }

    public async ValueTask DisposeAsync(
        ElementReference rootElement,
        ElementReference stageElement,
        ElementReference stageSurfaceElement,
        bool hasStage)
    {
        await TeardownAsync(rootElement, stageElement, stageSurfaceElement, hasStage, disposeModule: true);
    }

    private async Task TeardownAsync(
        ElementReference rootElement,
        ElementReference stageElement,
        ElementReference stageSurfaceElement,
        bool hasStage,
        bool disposeModule)
    {
        if (_module is null)
        {
            ClearLifecycleState();
            return;
        }

        try
        {
            if (IsFullscreen && IsFullscreenSupported)
            {
                await _module.InvokeVoidAsync("exitFullscreenIfOwned", rootElement);
            }

            if (IsStageObserved && hasStage)
            {
                await _module.InvokeVoidAsync("unobserveStageSize", stageElement);
            }

            if (IsGestureInteropActive && hasStage)
            {
                await _module.InvokeVoidAsync("deactivateViewerGestures", stageElement);
            }

            if (IsStageControlsActive)
            {
                await _module.InvokeVoidAsync("deactivateStageControls", stageSurfaceElement);
            }

            if (IsModalActive)
            {
                await _module.InvokeVoidAsync("unregisterFullscreenChange", rootElement);
                await _module.InvokeVoidAsync("deactivateModal", rootElement);
            }

            if (disposeModule)
            {
                await _module.DisposeAsync();
            }
        }
        catch
        {
            // Ignore teardown failures while the modal is closing or the app is navigating.
        }
        finally
        {
            ClearLifecycleState();

            if (disposeModule)
            {
                _module = null;
                _moduleImportTask = null;
                _moduleLoadFailed = false;
            }
        }
    }

    private void ClearLifecycleState()
    {
        IsStageObserved = false;
        IsModalActive = false;
        IsGestureInteropActive = false;
        IsStageControlsActive = false;
        IsFullscreen = false;
    }

    private async ValueTask<IJSObjectReference?> EnsureModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        if (_moduleLoadFailed)
        {
            return null;
        }

        _moduleImportTask ??= ImportViewerModuleAsync();
        return await _moduleImportTask;
    }

    private async Task<IJSObjectReference?> ImportViewerModuleAsync()
    {
        try
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/JSHelpers/viewerInterop.js");
            return _module;
        }
        catch
        {
            _moduleLoadFailed = true;
            return null;
        }
    }
}

internal sealed class ViewerRelativePoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

internal sealed class ViewerRelativeRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

internal sealed class ViewerTransformInteropSnapshot
{
    public double Zoom { get; set; }
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double MinZoom { get; set; }
    public double MaxZoom { get; set; }
    public double ZoomStep { get; set; }
    public bool CanPan { get; set; }
}
