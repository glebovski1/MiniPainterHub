const stageObservers = new WeakMap();
const fullscreenListeners = new WeakMap();

export function isFullscreenSupported() {
    return Boolean(document.fullscreenEnabled);
}

export function observeStageSize(element, dotNetRef) {
    if (!element || !dotNetRef || stageObservers.has(element)) {
        return;
    }

    const observer = new ResizeObserver(entries => {
        const entry = entries[0];
        if (!entry) {
            return;
        }

        const width = entry.contentRect?.width ?? element.clientWidth ?? 0;
        const height = entry.contentRect?.height ?? element.clientHeight ?? 0;
        dotNetRef.invokeMethodAsync("OnStageResized", width, height);
    });

    observer.observe(element);
    stageObservers.set(element, observer);

    dotNetRef.invokeMethodAsync("OnStageResized", element.clientWidth ?? 0, element.clientHeight ?? 0);
}

export function unobserveStageSize(element) {
    const observer = stageObservers.get(element);
    if (!observer) {
        return;
    }

    observer.disconnect();
    stageObservers.delete(element);
}

export function registerFullscreenChange(element, dotNetRef) {
    if (!element || !dotNetRef || fullscreenListeners.has(element)) {
        return;
    }

    const handler = () => {
        const active = document.fullscreenElement === element;
        dotNetRef.invokeMethodAsync("OnFullscreenChanged", active);
    };

    document.addEventListener("fullscreenchange", handler);
    fullscreenListeners.set(element, handler);
    handler();
}

export function unregisterFullscreenChange(element) {
    const handler = fullscreenListeners.get(element);
    if (!handler) {
        return;
    }

    document.removeEventListener("fullscreenchange", handler);
    fullscreenListeners.delete(element);
}

export async function requestFullscreen(element) {
    if (!element || !document.fullscreenEnabled) {
        return false;
    }

    if (document.fullscreenElement === element) {
        await document.exitFullscreen();
        return false;
    }

    await element.requestFullscreen();
    return true;
}

export function preloadImages(urls) {
    if (!Array.isArray(urls)) {
        return;
    }

    urls
        .filter(url => typeof url === "string" && url.length > 0)
        .forEach(url => {
            const image = new Image();
            image.decoding = "async";
            image.loading = "eager";
            image.src = url;
        });
}

export function getRelativePoint(element, clientX, clientY) {
    if (!element) {
        return { x: 0, y: 0 };
    }

    const rect = element.getBoundingClientRect();
    return {
        x: clientX - rect.left,
        y: clientY - rect.top
    };
}
