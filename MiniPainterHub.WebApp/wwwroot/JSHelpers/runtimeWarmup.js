(function () {
    const idleTimeoutMs = 1800;
    const maxWarmImages = 4;
    const publicFeedWarmupUrl = "/api/posts?page=2&pageSize=6&includeDeleted=False&deletedOnly=False";

    function scheduleIdle(callback) {
        if ("requestIdleCallback" in window) {
            window.requestIdleCallback(callback, { timeout: idleTimeoutMs });
            return;
        }

        window.setTimeout(callback, idleTimeoutMs);
    }

    function canWarmup() {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        if (connection?.saveData) {
            return false;
        }

        if (/(^|-)2g$/i.test(connection?.effectiveType || "")) {
            return false;
        }

        if (document.visibilityState !== "visible") {
            return false;
        }

        return true;
    }

    function waitForAppContent() {
        return new Promise((resolve) => {
            const isReady = () => {
                const app = document.getElementById("app");
                if (!app || app.querySelector(".loading-progress-shell")) {
                    return false;
                }

                return Boolean(app.querySelector(".post-card"))
                    || Boolean(app.querySelector("[data-testid='post-title']"))
                    || app.textContent.trim().length > 0;
            };

            if (isReady()) {
                resolve();
                return;
            }

            const observer = new MutationObserver(() => {
                if (!isReady()) {
                    return;
                }

                observer.disconnect();
                resolve();
            });

            observer.observe(document.getElementById("app") || document.body, {
                childList: true,
                subtree: true
            });
        });
    }

    function normalizeItems(payload) {
        return payload?.items || payload?.Items || [];
    }

    function resolveAssetUrl(url) {
        if (!url || typeof url !== "string") {
            return null;
        }

        try {
            return new URL(url, window.location.origin).toString();
        } catch {
            return null;
        }
    }

    function prefetchImage(url) {
        const href = resolveAssetUrl(url);
        if (!href) {
            return;
        }

        const link = document.createElement("link");
        link.rel = "prefetch";
        link.as = "image";
        link.href = href;
        document.head.appendChild(link);
    }

    function prefetchRoute(path) {
        if (!path || typeof path !== "string") {
            return;
        }

        const link = document.createElement("link");
        link.rel = "prefetch";
        link.href = path;
        document.head.appendChild(link);
    }

    async function warmPublicFeed() {
        const response = await fetch(publicFeedWarmupUrl, {
            credentials: "omit",
            headers: {
                Accept: "application/json",
                "X-MiniPainterHub-Warmup": "1"
            }
        });

        if (!response.ok) {
            return;
        }

        const payload = await response.json();
        const items = normalizeItems(payload);
        items.slice(0, maxWarmImages).forEach((item) => {
            prefetchImage(item.thumbnailUrl || item.ThumbnailUrl || item.previewUrl || item.PreviewUrl || item.imageUrl || item.ImageUrl);
        });

        const firstPostId = items[0]?.id || items[0]?.Id;
        if (firstPostId) {
            prefetchRoute(`/posts/${firstPostId}`);
        }
    }

    async function runWarmup() {
        if (!canWarmup()) {
            return;
        }

        await waitForAppContent();
        scheduleIdle(() => {
            if (!canWarmup()) {
                return;
            }

            warmPublicFeed().catch(() => { });
        });
    }

    if (document.readyState === "complete") {
        runWarmup().catch(() => { });
    } else {
        window.addEventListener("load", () => {
            runWarmup().catch(() => { });
        }, { once: true });
    }
}());
