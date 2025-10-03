// JSHelpers/domHelpers.js

window.domHelpers = window.domHelpers || {};

window.domHelpers.getHeight = (el) => (el ? el.offsetHeight : 0);

/**
 * Initialize a Bootstrap Carousel on a given element.
 * @param {HTMLElement} element - The carousel root element (div.carousel).
 * @param {number} [intervalMs] - Optional interval override from Blazor (ms).
 *                                If omitted, reads data-bs-interval from the element.
 */
window.domHelpers.initCarousel = (element, intervalMs) => {
  const Carousel = window?.bootstrap?.Carousel;
  if (!element || !Carousel) return;

  const ds = element.dataset || {};

  const parsed = Number(intervalMs);
  const interval = Number.isFinite(parsed)
    ? parsed
    : ds.bsInterval === "false"
      ? false
      : (ds.bsInterval && Number.isFinite(Number(ds.bsInterval)))
          ? Number(ds.bsInterval)
          : 5000;

  let pause;
  if (ds.bsPause === undefined) {
    pause = "hover";
  } else if (ds.bsPause === "false") {
    pause = false;
  } else {
    pause = ds.bsPause;
  }

  const ride = ds.bsRide === "false" ? false : (ds.bsRide ?? undefined);
  const touch = ds.bsTouch ? ds.bsTouch !== "false" : true;
  const wrap = ds.bsWrap ? ds.bsWrap !== "false" : true;

  const existing = Carousel.getInstance(element);
  if (existing) {
    existing.pause?.();
    existing.dispose();
  }

  const instance = new Carousel(element, {
    interval,
    pause,
    ride,
    touch,
    wrap
  });

  if (ride === "carousel") {
    instance.cycle();
  }

  return instance;
};
