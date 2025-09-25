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
  // Guard: Bootstrap not loaded or element missing
  const Carousel = window?.bootstrap?.Carousel;
  if (!element || !Carousel) return;

  // Prefer explicit C# value; otherwise read data attributes
  const ds = element.dataset || {};
  const parsed = Number(intervalMs);
  const interval =
    Number.isFinite(parsed)
      ? parsed
      : (ds.bsInterval && Number.isFinite(Number(ds.bsInterval)))
          ? Number(ds.bsInterval)
          : 5000; // sensible default

  const pause = ds.bsPause ?? "hover";           // "hover" | "false"
  const ride  = ds.bsRide ?? undefined;          // "carousel" to auto-start
  const touch = ds.bsTouch ? ds.bsTouch !== "false" : true;
  const wrap  = ds.bsWrap  ? ds.bsWrap  !== "false" : true;

  // Create or reuse instance with config
  const instance = Carousel.getOrCreateInstance(element, {
    interval,
    pause,
    ride,
    touch,
    wrap
  });

  // Ensure cycling
  instance.cycle();
  return instance;
};
