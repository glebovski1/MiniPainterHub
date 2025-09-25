window.domHelpers = window.domHelpers || {};

window.domHelpers.getHeight = (el) => (el ? el.offsetHeight : 0);

window.domHelpers.initCarousel = (element) => {
    if (!element || !window?.bootstrap?.Carousel) {
        return;
    }

    const { dataset } = element;
    const intervalValue = dataset?.bsInterval ? Number(dataset.bsInterval) : undefined;
    const config = {
        interval: Number.isFinite(intervalValue) ? intervalValue : undefined,
        pause: dataset?.bsPause ?? "hover",
        ride: dataset?.bsRide ?? undefined,
        touch: dataset?.bsTouch ?? true,
        wrap: dataset?.bsWrap ? dataset.bsWrap !== "false" : undefined,
    };

    const carousel = window.bootstrap.Carousel.getOrCreateInstance(element, config);
    carousel.cycle();
};
