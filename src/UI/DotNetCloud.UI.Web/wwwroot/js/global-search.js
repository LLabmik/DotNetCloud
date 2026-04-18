/**
 * Global Search keyboard shortcut and localStorage interop.
 */
(function () {
    const RECENT_SEARCHES_KEY = "DotNetCloudRecentSearches";
    const MAX_RECENT = 10;

    /**
     * Registers Ctrl+K / Cmd+K to invoke Blazor search callback.
     * @param {DotNetObjectReference} dotNetRef
     */
    window.DotNetCloudSearch = {
        registerShortcut: function (dotNetRef) {
            document.addEventListener("keydown", function (e) {
                if ((e.ctrlKey || e.metaKey) && e.key === "k") {
                    e.preventDefault();
                    dotNetRef.invokeMethodAsync("OnSearchShortcut");
                }
            });
        },

        focusElement: function (selector) {
            var el = document.querySelector(selector);
            if (el) {
                el.focus();
            }
        },

        getRecentSearches: function () {
            try {
                var raw = window.localStorage.getItem(RECENT_SEARCHES_KEY);
                return raw ? JSON.parse(raw) : [];
            } catch {
                return [];
            }
        },

        addRecentSearch: function (query) {
            try {
                var searches = this.getRecentSearches();
                // Remove duplicate if exists
                searches = searches.filter(function (s) { return s !== query; });
                searches.unshift(query);
                if (searches.length > MAX_RECENT) {
                    searches = searches.slice(0, MAX_RECENT);
                }
                window.localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(searches));
            } catch {
                // Ignore localStorage errors
            }
        },

        clearRecentSearches: function () {
            try {
                window.localStorage.removeItem(RECENT_SEARCHES_KEY);
            } catch {
                // Ignore
            }
        }
    };
})();
