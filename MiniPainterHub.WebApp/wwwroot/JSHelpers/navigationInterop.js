window.miniPainterHubNavigation = {
    closeMobileNavigation(collapseId) {
        if (!window.bootstrap || !window.matchMedia("(max-width: 991.98px)").matches) {
            return;
        }

        const collapseElement = document.getElementById(collapseId);
        if (!collapseElement) {
            return;
        }

        const dropdownToggles = collapseElement.querySelectorAll('[data-bs-toggle="dropdown"]');
        dropdownToggles.forEach((toggle) => {
            window.bootstrap.Dropdown.getInstance(toggle)?.hide();
        });

        if (!collapseElement.classList.contains("show")) {
            return;
        }

        const toggle = document.querySelector(`[aria-controls="${collapseId}"]`);
        if (toggle) {
            collapseElement.addEventListener("hidden.bs.collapse", () => {
                toggle.focus({ preventScroll: true });
            }, { once: true });
        }

        window.bootstrap.Collapse.getOrCreateInstance(collapseElement, { toggle: false }).hide();
    }
};
