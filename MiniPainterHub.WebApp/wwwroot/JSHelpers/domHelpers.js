window.domHelpers = window.domHelpers || {};

window.domHelpers.getHeight = (el) => (el ? el.offsetHeight : 0);

window.domHelpers.initCarousel = (element) => {
    if (!element || !window.bootstrap || !window.bootstrap.Carousel) {
        return;
    }

    const instance = window.bootstrap.Carousel.getInstance(element)
        || new window.bootstrap.Carousel(element);

    instance.cycle();
};