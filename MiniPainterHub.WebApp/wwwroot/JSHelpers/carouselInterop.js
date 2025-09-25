function fallbackInit(element, intervalMs) {
    const Carousel = globalThis?.bootstrap?.Carousel;
    if (!element || !Carousel) {
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

    const instance = Carousel.getOrCreateInstance(element, {
        interval,
        pause,
        ride,
        touch,
        wrap
    });

    instance.cycle();
}

export function initCarousel(element, intervalMs) {
    if (!element) {
        return;
    }

    const init = globalThis?.domHelpers?.initCarousel ?? fallbackInit;
    init(element, intervalMs);
}
