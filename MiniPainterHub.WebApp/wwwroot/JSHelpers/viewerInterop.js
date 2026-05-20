const stageObservers = new WeakMap();
const fullscreenListeners = new WeakMap();
const modalStates = new WeakMap();
const gestureStates = new WeakMap();

function readTransformValue(snapshot, camelName, pascalName, fallback) {
    const value = snapshot?.[camelName] ?? snapshot?.[pascalName];
    return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function readTransformBool(snapshot, camelName, pascalName, fallback) {
    const value = snapshot?.[camelName] ?? snapshot?.[pascalName];
    return typeof value === "boolean" ? value : fallback;
}

function applyTransformSnapshot(state, snapshot) {
    state.zoom = readTransformValue(snapshot, "zoom", "Zoom", state.zoom ?? 1);
    state.panX = readTransformValue(snapshot, "panX", "PanX", state.panX ?? 0);
    state.panY = readTransformValue(snapshot, "panY", "PanY", state.panY ?? 0);
    state.minZoom = readTransformValue(snapshot, "minZoom", "MinZoom", state.minZoom ?? 1);
    state.maxZoom = readTransformValue(snapshot, "maxZoom", "MaxZoom", state.maxZoom ?? 5);
    state.zoomStep = readTransformValue(snapshot, "zoomStep", "ZoomStep", state.zoomStep ?? 0.25);
    state.canPan = readTransformBool(snapshot, "canPan", "CanPan", state.canPan ?? false);
}

function getPanBounds(stageElement, transformElement, zoom) {
    const stageWidth = stageElement?.clientWidth ?? 0;
    const stageHeight = stageElement?.clientHeight ?? 0;
    const baseWidth = transformElement?.offsetWidth ?? 0;
    const baseHeight = transformElement?.offsetHeight ?? 0;

    return {
        maxPanX: Math.max(0, ((baseWidth * zoom) - stageWidth) / 2),
        maxPanY: Math.max(0, ((baseHeight * zoom) - stageHeight) / 2)
    };
}

function clampGestureState(state) {
    state.zoom = Math.min(state.maxZoom, Math.max(state.minZoom, state.zoom));

    const bounds = getPanBounds(state.stageElement, state.transformElement, state.zoom);
    state.panX = Math.min(bounds.maxPanX, Math.max(-bounds.maxPanX, state.panX));
    state.panY = Math.min(bounds.maxPanY, Math.max(-bounds.maxPanY, state.panY));
    state.canPan = bounds.maxPanX > 0.001 || bounds.maxPanY > 0.001;
}

function applyGestureTransform(state) {
    if (!state.transformElement) {
        return;
    }

    state.transformElement.style.transform = `translate(${state.panX.toFixed(2)}px,${state.panY.toFixed(2)}px) scale(${Number(state.zoom.toFixed(3))})`;
}

function scheduleGestureApply(state) {
    if (state.rafId) {
        return;
    }

    state.rafId = requestAnimationFrame(() => {
        state.rafId = 0;
        applyGestureTransform(state);
    });
}

function notifyGestureSettled(state) {
    state.stageElement?.classList.remove("is-gesturing");

    if (!state.dotNetRef) {
        return;
    }

    state.dotNetRef
        .invokeMethodAsync("OnViewerTransformSettled", state.zoom, state.panX, state.panY)
        .catch(() => {
            // Blazor may dispose the viewer while a gesture settles.
        });
}

function scheduleGestureSettled(state, delay = 140) {
    window.clearTimeout(state.settleTimer);
    state.settleTimer = window.setTimeout(() => notifyGestureSettled(state), delay);
}

function canHandlePan(state) {
    return state.canPan
        && !state.stageElement.classList.contains("is-placement-mode");
}

function getGesturePoint(event) {
    return { x: event.clientX, y: event.clientY };
}

function getFirstTwoPointers(state) {
    return Array.from(state.activePointers.values()).slice(0, 2);
}

function getDistance(first, second) {
    return Math.hypot(second.x - first.x, second.y - first.y);
}

function getCenter(first, second) {
    return {
        x: (first.x + second.x) / 2,
        y: (first.y + second.y) / 2
    };
}

function beginPinch(state) {
    const pointers = getFirstTwoPointers(state);
    if (pointers.length < 2) {
        state.pinchStartDistance = 0;
        state.pinchLastCenter = null;
        return;
    }

    state.pinchStartDistance = Math.max(1, getDistance(pointers[0], pointers[1]));
    state.pinchStartZoom = state.zoom;
    state.pinchLastCenter = getCenter(pointers[0], pointers[1]);
}

function applyTouchGesture(state) {
    const pointers = getFirstTwoPointers(state);
    if (pointers.length >= 2) {
        if (!state.pinchStartDistance || !state.pinchLastCenter) {
            beginPinch(state);
        }

        const center = getCenter(pointers[0], pointers[1]);
        const distance = Math.max(1, getDistance(pointers[0], pointers[1]));
        state.panX += center.x - state.pinchLastCenter.x;
        state.panY += center.y - state.pinchLastCenter.y;
        state.zoom = state.pinchStartZoom * (distance / state.pinchStartDistance);
        state.pinchLastCenter = center;
        clampGestureState(state);
        scheduleGestureApply(state);
        scheduleGestureSettled(state);
        return true;
    }

    if (pointers.length === 1 && canHandlePan(state)) {
        const pointer = pointers[0];
        state.panX += pointer.x - state.lastX;
        state.panY += pointer.y - state.lastY;
        state.lastX = pointer.x;
        state.lastY = pointer.y;
        clampGestureState(state);
        scheduleGestureApply(state);
        scheduleGestureSettled(state);
        return true;
    }

    return false;
}

function getFocusableElements(element) {
    if (!element) {
        return [];
    }

    return Array.from(
        element.querySelectorAll(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )
    ).filter(node => !node.hasAttribute("inert") && node.getAttribute("aria-hidden") !== "true");
}

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

export async function exitFullscreenIfOwned(element) {
    if (!element || document.fullscreenElement !== element) {
        return false;
    }

    await document.exitFullscreen();
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

export function activateViewerGestures(stageElement, transformElement, dotNetRef, snapshot) {
    if (!stageElement || !transformElement || !dotNetRef) {
        return;
    }

    let state = gestureStates.get(stageElement);
    if (state && state.listeners.length > 0) {
        state.transformElement = transformElement;
        state.dotNetRef = dotNetRef;
        applyTransformSnapshot(state, snapshot);
        clampGestureState(state);
        applyGestureTransform(state);
        return;
    }

    state = state ?? {
        stageElement,
        transformElement,
        dotNetRef,
        zoom: 1,
        panX: 0,
        panY: 0,
        minZoom: 1,
        maxZoom: 5,
        zoomStep: 0.25,
        canPan: false,
        activePointerId: null,
        activePointers: new Map(),
        lastX: 0,
        lastY: 0,
        pinchStartDistance: 0,
        pinchStartZoom: 1,
        pinchLastCenter: null,
        rafId: 0,
        settleTimer: 0,
        listeners: []
    };
    state.stageElement = stageElement;
    state.transformElement = transformElement;
    state.dotNetRef = dotNetRef;
    state.activePointers ??= new Map();

    applyTransformSnapshot(state, snapshot);
    clampGestureState(state);
    applyGestureTransform(state);

    const onWheel = event => {
        event.preventDefault();
        state.stageElement.classList.add("is-gesturing");
        const direction = event.deltaY < 0 ? 1 : -1;
        state.zoom += direction * state.zoomStep;
        clampGestureState(state);
        scheduleGestureApply(state);
        scheduleGestureSettled(state);
    };

    const onPointerDown = event => {
        if (event.pointerType === "touch") {
            state.activePointers.set(event.pointerId, getGesturePoint(event));
            state.lastX = event.clientX;
            state.lastY = event.clientY;
            stageElement.classList.add("is-gesturing");
            stageElement.setPointerCapture?.(event.pointerId);

            if (state.activePointers.size >= 2) {
                beginPinch(state);
            }

            event.preventDefault();
            return;
        }

        if (event.button !== 0 || !canHandlePan(state)) {
            return;
        }

        state.activePointerId = event.pointerId;
        state.lastX = event.clientX;
        state.lastY = event.clientY;
        stageElement.classList.add("is-gesturing");
        stageElement.setPointerCapture?.(event.pointerId);
        event.preventDefault();
    };

    const onPointerMove = event => {
        if (event.pointerType === "touch" && state.activePointers.has(event.pointerId)) {
            state.activePointers.set(event.pointerId, getGesturePoint(event));
            if (applyTouchGesture(state)) {
                event.preventDefault();
            }

            return;
        }

        if (state.activePointerId !== event.pointerId || !canHandlePan(state)) {
            return;
        }

        state.panX += event.clientX - state.lastX;
        state.panY += event.clientY - state.lastY;
        state.lastX = event.clientX;
        state.lastY = event.clientY;
        clampGestureState(state);
        scheduleGestureApply(state);
        scheduleGestureSettled(state);
        event.preventDefault();
    };

    const endPointer = event => {
        if (event.pointerType === "touch" && state.activePointers.has(event.pointerId)) {
            state.activePointers.delete(event.pointerId);
            stageElement.releasePointerCapture?.(event.pointerId);

            if (state.activePointers.size >= 2) {
                beginPinch(state);
            }
            else if (state.activePointers.size === 1) {
                const pointer = getFirstTwoPointers(state)[0];
                state.lastX = pointer.x;
                state.lastY = pointer.y;
                state.pinchStartDistance = 0;
                state.pinchLastCenter = null;
            }
            else {
                state.pinchStartDistance = 0;
                state.pinchLastCenter = null;
                notifyGestureSettled(state);
            }

            event.preventDefault();
            return;
        }

        if (state.activePointerId !== event.pointerId) {
            return;
        }

        state.activePointerId = null;
        stageElement.releasePointerCapture?.(event.pointerId);
        notifyGestureSettled(state);
    };

    stageElement.addEventListener("wheel", onWheel, { passive: false });
    stageElement.addEventListener("pointerdown", onPointerDown);
    stageElement.addEventListener("pointermove", onPointerMove);
    stageElement.addEventListener("pointerup", endPointer);
    stageElement.addEventListener("pointercancel", endPointer);
    state.listeners.push(
        ["wheel", onWheel],
        ["pointerdown", onPointerDown],
        ["pointermove", onPointerMove],
        ["pointerup", endPointer],
        ["pointercancel", endPointer]);

    gestureStates.set(stageElement, state);
}

export function syncViewerTransform(stageElement, transformElement, snapshot) {
    if (!stageElement || !transformElement) {
        return;
    }

    let state = gestureStates.get(stageElement);
    if (!state) {
        state = {
            stageElement,
            transformElement,
            zoom: 1,
            panX: 0,
            panY: 0,
            minZoom: 1,
            maxZoom: 5,
            zoomStep: 0.25,
            canPan: false,
            rafId: 0,
            settleTimer: 0,
            listeners: []
        };
        gestureStates.set(stageElement, state);
    }

    state.transformElement = transformElement;
    applyTransformSnapshot(state, snapshot);
    clampGestureState(state);
    applyGestureTransform(state);
}

export function deactivateViewerGestures(stageElement) {
    const state = gestureStates.get(stageElement);
    if (!state) {
        return;
    }

    for (const [eventName, handler] of state.listeners) {
        stageElement.removeEventListener(eventName, handler);
    }

    if (state.rafId) {
        cancelAnimationFrame(state.rafId);
    }

    window.clearTimeout(state.settleTimer);
    state.stageElement?.classList.remove("is-gesturing");
    gestureStates.delete(stageElement);
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

export function getRelativeRect(container, target) {
    if (!container || !target) {
        return { left: 0, top: 0, width: 0, height: 0 };
    }

    const containerRect = container.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    return {
        left: targetRect.left - containerRect.left,
        top: targetRect.top - containerRect.top,
        width: targetRect.width,
        height: targetRect.height
    };
}

export function activateModal(element) {
    if (!element || modalStates.has(element)) {
        return;
    }

    const previousOverflow = document.body.style.overflow;
    const previousPaddingRight = document.body.style.paddingRight;
    const previousDocumentOverflow = document.documentElement.style.overflow;
    const scrollbarWidth = Math.max(0, window.innerWidth - document.documentElement.clientWidth);
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    document.body.style.overflow = "hidden";
    document.documentElement.style.overflow = "hidden";
    if (scrollbarWidth > 0) {
        document.body.style.paddingRight = `${scrollbarWidth}px`;
    }

    const keydownHandler = event => {
        if (event.key !== "Tab") {
            return;
        }

        const focusable = getFocusableElements(element);
        if (focusable.length === 0) {
            event.preventDefault();
            element.focus();
            return;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        const active = document.activeElement;

        if (event.shiftKey) {
            if (active === first || active === element) {
                event.preventDefault();
                last.focus();
            }

            return;
        }

        if (active === last) {
            event.preventDefault();
            first.focus();
        }
    };

    const focusInHandler = event => {
        if (element.contains(event.target)) {
            return;
        }

        const focusable = getFocusableElements(element);
        (focusable[0] ?? element).focus();
    };

    document.addEventListener("keydown", keydownHandler, true);
    document.addEventListener("focusin", focusInHandler, true);

    modalStates.set(element, {
        previousOverflow,
        previousPaddingRight,
        previousDocumentOverflow,
        previouslyFocused,
        keydownHandler,
        focusInHandler
    });

    requestAnimationFrame(() => {
        const focusable = getFocusableElements(element);
        (focusable[0] ?? element).focus();
    });
}

export function deactivateModal(element) {
    const state = modalStates.get(element);
    if (!state) {
        return;
    }

    document.removeEventListener("keydown", state.keydownHandler, true);
    document.removeEventListener("focusin", state.focusInHandler, true);
    document.body.style.overflow = state.previousOverflow;
    document.body.style.paddingRight = state.previousPaddingRight;
    document.documentElement.style.overflow = state.previousDocumentOverflow;

    if (state.previouslyFocused && typeof state.previouslyFocused.focus === "function") {
        state.previouslyFocused.focus();
    }

    modalStates.delete(element);
}

export function scrollIntoViewIfNeeded(selector) {
    if (!selector) {
        return;
    }

    const element = document.querySelector(selector);
    if (!element) {
        return;
    }

    element.scrollIntoView({
        block: "nearest",
        inline: "nearest",
        behavior: "auto"
    });
}

export function scrollElementToTop(element) {
    if (!element) {
        return;
    }

    element.scrollTo({
        top: 0,
        behavior: "auto"
    });
}
