export function initCarousel(element) {
    if (!element || !window?.bootstrap?.Carousel) {
        return;
    }

    const instance = window.bootstrap.Carousel.getOrCreateInstance(element);
    instance.cycle();
}
