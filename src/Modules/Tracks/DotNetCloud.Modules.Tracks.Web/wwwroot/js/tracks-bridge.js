// Thin C#-facing wrapper over the SDK frame bridge (js/dncModuleBridge.js).
//
// The bridge MUST be started before Blazor runs: it patches window.fetch so every request the module
// UI makes carries the frame ticket, which is how the core proxy authenticates a sandboxed frame
// (opaque origin => no session cookie). Starting it later would let the first API call escape
// unauthenticated.
(function () {
    'use strict';

    function bridge() {
        return window.dncModuleBridge;
    }

    function theme(payload, fallback) {
        return payload && typeof payload.theme === 'string' ? payload.theme : fallback;
    }

    var api = {
        start: function () {
            var b = bridge();
            if (!b) {
                return false;
            }

            b.start({ title: 'Tracks', role: 'app' });
            return true;
        },

        onTheme: function (dotNetRef) {
            bridge().on('theme', function (payload) {
                dotNetRef.invokeMethodAsync('OnThemeChanged', theme(payload, 'light'), (payload && payload.locale) || '');
            });
        },

        mediaUrl: function (url) {
            return bridge().mediaUrl(url);
        },

        navigate: function (path) {
            bridge().navigate(path);
        },

        setTitle: function (title) {
            bridge().setTitle(title);
        },

        notify: function (message) {
            bridge().notify(message);
        },

        requestConsent: function (scopes) {
            bridge().requestConsent(scopes || []);
        }
    };

    window.tracksBridge = api;
    api.start();
})();
