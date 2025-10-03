const BOOTSTRAP_RETRY_DELAY_MS = 100;
const BOOTSTRAP_MAX_ATTEMPTS = 50;

function parseInterval(element, intervalMs) {
    const ds = element.dataset ?? {};
    const parsed = Number(intervalMs);
    if (Number.isFinite(parsed)) {
        return parsed;
    }

    if (ds.bsInterval === "false") {
        return false;
    }

    const fromData = Number(ds.bsInterval);
    return Number.isFinite(fromData) ? fromData : 5000;
}

function parsePause(element) {
    const pauseAttr = element.dataset?.bsPause;
    if (pauseAttr === undefined) {
        return "hover";
    }

    if (pauseAttr === "false") {
        return false;
    }

    return pauseAttr;
}

function parseRide(element) {
    const rideAttr = element.dataset?.bsRide;
    if (rideAttr === "false") {
        return false;
    }

    return rideAttr ?? undefined;
}

function fallbackInit(element, intervalMs, Carousel) {
    const bootstrapCarousel = Carousel ?? globalThis?.bootstrap?.Carousel;
    if (!element || !bootstrapCarousel) {
        return;
    }

    const touch = element.dataset?.bsTouch ? element.dataset.bsTouch !== "false" : true;
    const wrap = element.dataset?.bsWrap ? element.dataset.bsWrap !== "false" : true;

    const interval = parseInterval(element, intervalMs);
    const pause = parsePause(element);
    const ride = parseRide(element);

    const existing = bootstrapCarousel.getInstance(element);
    if (existing) {
        existing.pause?.();
        existing.dispose();
    }

    const instance = new bootstrapCarousel(element, {
        interval,
        pause,
        ride,
        touch,
        wrap
    });

    if (ride === "carousel") {
        instance.cycle();
    }
}

function waitForBootstrapCarousel(attempt = 0) {
    const Carousel = globalThis?.bootstrap?.Carousel;
    if (Carousel) {
        return Promise.resolve(Carousel);
    }

    if (attempt >= BOOTSTRAP_MAX_ATTEMPTS) {
        console.warn("Bootstrap Carousel was not available in time to initialize.");
        return Promise.resolve(null);
    }

    return new Promise((resolve) => {
        setTimeout(() => {
            waitForBootstrapCarousel(attempt + 1).then(resolve);
        }, BOOTSTRAP_RETRY_DELAY_MS);
    });
}

export async function initCarousel(element, intervalMs) {
    if (!element) {
        return;
    }

    const Carousel = await waitForBootstrapCarousel();
    if (!Carousel) {
        return;
    }

    const init = globalThis?.domHelpers?.initCarousel;
    if (typeof init === "function") {
        init(element, intervalMs);
        return;
    }

    fallbackInit(element, intervalMs, Carousel);
}
