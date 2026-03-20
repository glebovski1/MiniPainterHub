const stageObservers = new WeakMap();
const fullscreenListeners = new WeakMap();
const modalStates = new WeakMap();

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
