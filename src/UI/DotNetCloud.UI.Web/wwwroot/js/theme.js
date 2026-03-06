(function () {
    const storageKey = "DotNetCloudTheme";
    const toggleSelector = "#theme-toggle-button";

    function applyTheme(isDark) {
        document.documentElement.classList.toggle("dark-mode", isDark);
        document.body.classList.toggle("dark-mode", isDark);

        const button = document.querySelector(toggleSelector);
        if (button) {
            button.textContent = isDark ? "☀" : "☾";
            button.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");
            button.setAttribute("title", isDark ? "Switch to light mode" : "Switch to dark mode");
        }
    }

    function getInitialTheme() {
        const savedTheme = window.localStorage.getItem(storageKey);
        if (savedTheme === "dark") {
            return true;
        }

        if (savedTheme === "light") {
            return false;
        }

        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    }

    function setTheme(isDark) {
        window.localStorage.setItem(storageKey, isDark ? "dark" : "light");
        applyTheme(isDark);
        console.info("[theme]", isDark ? "dark" : "light");
    }

    function toggleTheme() {
        const isDark = !document.body.classList.contains("dark-mode");
        setTheme(isDark);
    }

    function initializeTheme() {
        applyTheme(getInitialTheme());
        console.info("[theme] initialized");
    }

    document.addEventListener("click", function (event) {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const toggleButton = target.closest(toggleSelector);
        if (!toggleButton) {
            return;
        }

        event.preventDefault();
        toggleTheme();
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeTheme, { once: true });
    } else {
        initializeTheme();
    }

    window.dotNetCloudTheme = {
        toggle: toggleTheme,
        apply: function (mode) {
            setTheme(mode === "dark");
        }
    };
})();
