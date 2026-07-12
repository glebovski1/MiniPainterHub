const stageObservers = new WeakMap();
const fullscreenListeners = new WeakMap();
const modalStates = new WeakMap();
const gestureStates = new WeakMap();
const stageControlStates = new WeakMap();
const preloadQueue = new Set();
const decodedPreloadImages = new Map();
const maxDecodedPreloadImages = 12;
const preloadBatchSize = 3;
const stageControlsHideDelayMs = 1600;
const stageControlsLeaveDelayMs = 180;
let preloadIdleHandle = 0;

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

function refreshPanMetrics(state) {
    state.panMetrics = {
        stageWidth: state.stageElement?.clientWidth ?? 0,
        stageHeight: state.stageElement?.clientHeight ?? 0,
        baseWidth: state.transformElement?.offsetWidth ?? 0,
        baseHeight: state.transformElement?.offsetHeight ?? 0
    };
}

function getPanBounds(state) {
    if (!state.panMetrics) {
        refreshPanMetrics(state);
    }

    const { stageWidth, stageHeight, baseWidth, baseHeight } = state.panMetrics;

    return {
        maxPanX: Math.max(0, ((baseWidth * state.zoom) - stageWidth) / 2),
        maxPanY: Math.max(0, ((baseHeight * state.zoom) - stageHeight) / 2)
    };
}

function clampGestureState(state, refreshMetrics = false) {
    state.zoom = Math.min(state.maxZoom, Math.max(state.minZoom, state.zoom));

    if (refreshMetrics) {
        refreshPanMetrics(state);
    }

    const bounds = getPanBounds(state);
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

function scheduleGestureSettled(state, delay = 260) {
    window.clearTimeout(state.settleTimer);
    state.settleTimer = window.setTimeout(() => notifyGestureSettled(state), delay);
}

function canHandlePan(state) {
    return state.canPan
        && !state.stageElement.classList.contains("is-placement-mode");
}

function canOwnTouchGesture(state) {
    return !state.stageElement.classList.contains("is-placement-mode");
}

function shouldClaimTouchGesture(state) {
    return state.activePointers.size >= 2
        || state.pinchStartDistance > 0
        || canHandlePan(state);
}

function claimGestureEvent(event) {
    event.preventDefault();
    event.stopImmediatePropagation();
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

function addGestureListener(state, eventName, handler, options) {
    state.stageElement.addEventListener(eventName, handler, options);
    state.listeners.push([eventName, handler, options]);
}

export function isFullscreenSupported() {
    return Boolean(document.fullscreenEnabled);
}

export function observeStageSize(element, dotNetRef) {
    if (!element || !dotNetRef || stageObservers.has(element)) {
        return;
    }

    const state = {
        observer: null,
        rafId: 0,
        settleTimer: 0,
        width: element.clientWidth ?? 0,
        height: element.clientHeight ?? 0
    };

    const notify = (width, height) => {
        state.width = width;
        state.height = height;

        if (state.rafId) {
            return;
        }

        state.rafId = requestAnimationFrame(() => {
            state.rafId = 0;
            dotNetRef.invokeMethodAsync("OnStageResized", state.width, state.height);

            window.clearTimeout(state.settleTimer);
            state.settleTimer = window.setTimeout(() => {
                dotNetRef.invokeMethodAsync(
                    "OnStageResized",
                    element.clientWidth ?? state.width,
                    element.clientHeight ?? state.height);
            }, 90);
        });
    };

    const observer = new ResizeObserver(entries => {
        const entry = entries[0];
        if (!entry) {
            return;
        }

        const width = entry.contentRect?.width ?? element.clientWidth ?? 0;
        const height = entry.contentRect?.height ?? element.clientHeight ?? 0;
        notify(width, height);
    });

    state.observer = observer;
    observer.observe(element);
    stageObservers.set(element, state);

    notify(element.clientWidth ?? 0, element.clientHeight ?? 0);
}

export function unobserveStageSize(element) {
    const state = stageObservers.get(element);
    if (!state) {
        return;
    }

    state.observer?.disconnect();
    window.cancelAnimationFrame(state.rafId);
    window.clearTimeout(state.settleTimer);
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
        .filter(url => typeof url === "string" && url.length > 0 && !decodedPreloadImages.has(url))
        .forEach(url => preloadQueue.add(url));

    schedulePreloadFlush();
}

function rememberDecodedPreload(url, image) {
    if (decodedPreloadImages.has(url)) {
        decodedPreloadImages.delete(url);
    }

    decodedPreloadImages.set(url, image);

    while (decodedPreloadImages.size > maxDecodedPreloadImages) {
        const oldestUrl = decodedPreloadImages.keys().next().value;
        decodedPreloadImages.delete(oldestUrl);
    }
}

function forgetDecodedPreload(url, image) {
    if (decodedPreloadImages.get(url) === image) {
        decodedPreloadImages.delete(url);
    }
}

function preloadAndDecodeImage(url) {
    if (decodedPreloadImages.has(url)) {
        return;
    }

    const image = new Image();
    image.decoding = "async";

    if ("loading" in image) {
        image.loading = "eager";
    }

    if ("fetchPriority" in image) {
        image.fetchPriority = "low";
    }

    rememberDecodedPreload(url, image);
    image.addEventListener("error", () => forgetDecodedPreload(url, image), { once: true });
    image.src = url;

    if (typeof image.decode === "function") {
        image.decode().catch(() => {
            if (!image.complete || image.naturalWidth === 0) {
                forgetDecodedPreload(url, image);
            }
        });
    }
}

function schedulePreloadFlush() {
    if (preloadIdleHandle || preloadQueue.size === 0) {
        return;
    }

    const flush = () => {
        preloadIdleHandle = 0;
        const nextUrls = Array.from(preloadQueue).slice(0, preloadBatchSize);

        nextUrls.forEach(url => {
            preloadQueue.delete(url);
            preloadAndDecodeImage(url);
        });

        if (preloadQueue.size > 0) {
            schedulePreloadFlush();
        }
    };

    if ("requestIdleCallback" in window) {
        preloadIdleHandle = window.requestIdleCallback(flush, { timeout: 420 });
        return;
    }

    preloadIdleHandle = window.setTimeout(flush, 160);
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
        clampGestureState(state, true);
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
        panMetrics: null,
        listeners: []
    };
    state.stageElement = stageElement;
    state.transformElement = transformElement;
    state.dotNetRef = dotNetRef;
    state.activePointers ??= new Map();

    applyTransformSnapshot(state, snapshot);
    clampGestureState(state, true);
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
            if (!canOwnTouchGesture(state)) {
                return;
            }

            const shouldClaim = state.activePointers.size > 0 || canHandlePan(state);
            if (shouldClaim) {
                claimGestureEvent(event);
            }

            state.activePointers.set(event.pointerId, getGesturePoint(event));
            state.lastX = event.clientX;
            state.lastY = event.clientY;
            stageElement.classList.add("is-gesturing");
            stageElement.setPointerCapture?.(event.pointerId);

            if (state.activePointers.size >= 2) {
                beginPinch(state);
            }

            return;
        }

        if (event.button !== 0 || !canHandlePan(state)) {
            return;
        }

        claimGestureEvent(event);
        state.activePointerId = event.pointerId;
        state.lastX = event.clientX;
        state.lastY = event.clientY;
        stageElement.classList.add("is-gesturing");
        stageElement.setPointerCapture?.(event.pointerId);
    };

    const onPointerMove = event => {
        if (event.pointerType === "touch" && state.activePointers.has(event.pointerId)) {
            state.activePointers.set(event.pointerId, getGesturePoint(event));
            if (shouldClaimTouchGesture(state)) {
                claimGestureEvent(event);
                applyTouchGesture(state);
            }

            return;
        }

        if (state.activePointerId !== event.pointerId || !canHandlePan(state)) {
            return;
        }

        claimGestureEvent(event);
        state.panX += event.clientX - state.lastX;
        state.panY += event.clientY - state.lastY;
        state.lastX = event.clientX;
        state.lastY = event.clientY;
        clampGestureState(state);
        scheduleGestureApply(state);
        scheduleGestureSettled(state);
    };

    const endPointer = event => {
        if (event.pointerType === "touch" && state.activePointers.has(event.pointerId)) {
            const shouldClaim = shouldClaimTouchGesture(state);
            if (shouldClaim) {
                claimGestureEvent(event);
            }

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
                if (shouldClaim) {
                    notifyGestureSettled(state);
                }
                else {
                    state.stageElement?.classList.remove("is-gesturing");
                }
            }

            return;
        }

        if (state.activePointerId !== event.pointerId) {
            return;
        }

        claimGestureEvent(event);
        state.activePointerId = null;
        stageElement.releasePointerCapture?.(event.pointerId);
        notifyGestureSettled(state);
    };

    stageElement.addEventListener("wheel", onWheel, { passive: false });
    state.listeners.push(["wheel", onWheel, { passive: false }]);
    addGestureListener(state, "pointerdown", onPointerDown, { capture: true, passive: false });
    addGestureListener(state, "pointermove", onPointerMove, { capture: true, passive: false });
    addGestureListener(state, "pointerup", endPointer, { capture: true, passive: false });
    addGestureListener(state, "pointercancel", endPointer, { capture: true, passive: false });

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
            panMetrics: null,
            listeners: []
        };
        gestureStates.set(stageElement, state);
    }

    state.transformElement = transformElement;
    applyTransformSnapshot(state, snapshot);
    clampGestureState(state, true);
    applyGestureTransform(state);
}

export function deactivateViewerGestures(stageElement) {
    const state = gestureStates.get(stageElement);
    if (!state) {
        return;
    }

    for (const [eventName, handler, options] of state.listeners) {
        stageElement.removeEventListener(eventName, handler, options);
    }

    if (state.rafId) {
        cancelAnimationFrame(state.rafId);
    }

    window.clearTimeout(state.settleTimer);
    state.stageElement?.classList.remove("is-gesturing");
    gestureStates.delete(stageElement);
}

export function activateStageControls(element) {
    if (!element || stageControlStates.has(element)) {
        return;
    }

    const state = {
        hideTimer: 0,
        hideDeadline: 0,
        isFocusHeld: false,
        isKeyboardModality: false,
        isVisible: false,
        keyboardHandler: null,
        listeners: []
    };

    const showNow = () => {
        if (state.isVisible) {
            return;
        }

        element.classList.add("is-controls-visible");
        state.isVisible = true;
    };

    const hideNow = () => {
        element.classList.remove("is-controls-visible");
        state.hideDeadline = 0;
        state.hideTimer = 0;
        state.isVisible = false;
    };

    const runHideTimer = () => {
        const remainingMs = state.hideDeadline - performance.now();
        if (remainingMs > 8) {
            state.hideTimer = window.setTimeout(runHideTimer, remainingMs);
            return;
        }

        hideNow();
    };

    const scheduleHide = delayMs => {
        state.hideDeadline = performance.now() + delayMs;

        if (!state.hideTimer) {
            state.hideTimer = window.setTimeout(runHideTimer, delayMs);
        }
    };

    const showControls = () => {
        showNow();

        if (!state.isFocusHeld) {
            scheduleHide(stageControlsHideDelayMs);
        }
    };

    const updateFocusVisibility = event => {
        const focusedElement = event?.target;
        const isKeyboardFocusVisible = state.isKeyboardModality
            && focusedElement instanceof Element
            && focusedElement.matches(":focus-visible");

        state.isFocusHeld = isKeyboardFocusVisible;
        showNow();

        if (isKeyboardFocusVisible) {
            window.clearTimeout(state.hideTimer);
            state.hideTimer = 0;
            state.hideDeadline = 0;
            return;
        }

        scheduleHide(stageControlsHideDelayMs);
    };

    const releasePointerFocus = () => {
        state.isKeyboardModality = false;
        state.isFocusHeld = false;
        showControls();
    };

    const handleKeyboardActivity = event => {
        if (event.key !== "Tab" && !element.contains(document.activeElement)) {
            return;
        }

        state.isKeyboardModality = true;
        state.isFocusHeld = true;
        showNow();
        window.clearTimeout(state.hideTimer);
        state.hideTimer = 0;
        state.hideDeadline = 0;
    };

    const hideControlsSoon = event => {
        if (event?.relatedTarget instanceof Node && element.contains(event.relatedTarget)) {
            return;
        }

        state.isFocusHeld = false;
        scheduleHide(stageControlsLeaveDelayMs);
    };

    state.listeners = [
        ["pointermove", showControls, { passive: true }],
        ["pointerdown", releasePointerFocus, { passive: true }],
        ["pointerleave", hideControlsSoon, { passive: true }],
        ["focusin", updateFocusVisibility, false],
        ["focusout", hideControlsSoon, false]
    ];

    for (const [eventName, handler, options] of state.listeners) {
        element.addEventListener(eventName, handler, options);
    }

    state.keyboardHandler = handleKeyboardActivity;
    document.addEventListener("keydown", handleKeyboardActivity, true);

    stageControlStates.set(element, state);
}

export function deactivateStageControls(element) {
    const state = stageControlStates.get(element);
    if (!state) {
        return;
    }

    for (const [eventName, handler, options] of state.listeners) {
        element.removeEventListener(eventName, handler, options);
    }

    if (state.keyboardHandler) {
        document.removeEventListener("keydown", state.keyboardHandler, true);
    }

    window.clearTimeout(state.hideTimer);
    element.classList.remove("is-controls-visible");
    stageControlStates.delete(element);
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
