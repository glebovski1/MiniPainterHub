const BOOTSTRAP_RETRY_DELAY_MS = 100;
const BOOTSTRAP_MAX_ATTEMPTS = 50;

function fallbackInit(element, intervalMs, Carousel) {
    const bootstrapCarousel = Carousel ?? globalThis?.bootstrap?.Carousel;
    if (!element || !bootstrapCarousel) {
        return;
    }

    const ds = element.dataset ?? {};
    const parsed = Number(intervalMs);
    const interval = Number.isFinite(parsed)
        ? parsed
        : (ds.bsInterval && Number.isFinite(Number(ds.bsInterval)))
            ? Number(ds.bsInterval)
            : 5000;

    const pause = ds.bsPause ?? "hover";
    const ride = ds.bsRide ?? undefined;
    const touch = ds.bsTouch ? ds.bsTouch !== "false" : true;
    const wrap = ds.bsWrap ? ds.bsWrap !== "false" : true;

    const instance = bootstrapCarousel.getOrCreateInstance(element, {
        interval,
        pause,
        ride,
        touch,
        wrap
    });

    instance.cycle();
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
