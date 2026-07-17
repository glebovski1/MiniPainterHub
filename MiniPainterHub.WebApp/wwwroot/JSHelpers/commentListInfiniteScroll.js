const observers = new Map();

function resolveScrollRoot(sentinel) {
    const candidate = sentinel?.closest?.("[data-infinite-scroll-root]");
    if (!candidate) {
        return null;
    }

    const overflowY = globalThis.getComputedStyle(candidate).overflowY;
    return overflowY === "auto" || overflowY === "scroll" ? candidate : null;
}

export function observeInfiniteScroll(sentinel, dotNetReference, observerId) {
    if (!sentinel || !dotNetReference || !observerId) {
        return;
    }

    disconnectInfiniteScroll(observerId);

    if (!("IntersectionObserver" in globalThis)) {
        void dotNetReference.invokeMethodAsync("LoadMoreAsync");
        return;
    }

    let callbackPending = false;
    const observer = new IntersectionObserver(
        async (entries) => {
            if (callbackPending || !entries.some((entry) => entry.isIntersecting)) {
                return;
            }

            callbackPending = true;
            observer.unobserve(sentinel);

            try {
                await dotNetReference.invokeMethodAsync("LoadMoreAsync");
            } finally {
                callbackPending = false;
            }
        },
        {
            root: resolveScrollRoot(sentinel),
            rootMargin: "0px 0px 240px 0px",
            threshold: 0.01
        }
    );

    observers.set(observerId, observer);
    observer.observe(sentinel);
}

export function disconnectInfiniteScroll(observerId) {
    const observer = observers.get(observerId);
    if (!observer) {
        return;
    }

    observer.disconnect();
    observers.delete(observerId);
}
