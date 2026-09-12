// Global presence activity reporter.
//
// Attaches throttled interaction listeners (pointer/key/wheel/touch/scroll) that report genuine
// user activity back into Blazor so the signed-in user's presence dot stays green while they
// interact with any page. Only REAL interaction is reported (never transport keepalives) — a tab
// left open with no interaction goes yellow after the configured idle threshold, then gray when
// the connection drops.
window.dotnetcloudPresence = {
    _dotNetRef: null,
    _lastReportMs: 0,
    _minReportIntervalMs: 20000,

    // Called by the Blazor component after first render to register the .NET object reference.
    attach: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._lastReportMs = Date.now();

        const report = () => this.report();
        const options = { capture: true, passive: true };
        const events = ['pointerdown', 'keydown', 'wheel', 'touchstart', 'scroll'];
        for (const name of events) {
            document.addEventListener(name, report, options);
        }

        // Returning to the tab after being backgrounded is itself activity.
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) {
                this.report();
            }
        });
    },

    // Called by the Blazor component when its circuit is disposed.
    dispose: function () {
        this._dotNetRef = null;
    },

    // Throttled: at most one report per _minReportIntervalMs.
    report: function () {
        const now = Date.now();
        if (!this._dotNetRef) {
            return;
        }
        if (now - this._lastReportMs < this._minReportIntervalMs) {
            return;
        }
        this._lastReportMs = now;
        this._dotNetRef.invokeMethodAsync('ReportActivityAsync').catch(() => { /* circuit torn down */ });
    }
};
